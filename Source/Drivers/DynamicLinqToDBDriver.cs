using System.Data;
using System.Data.Common;
using System.IO;
using LINQPad.Extensibility.DataContext;
using LinqToDB.Data;
using LinqToDB.Mapping;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
#if NETFRAMEWORK
using System.Net.NetworkInformation;
using System.Numerics;
#else
using System.Text.RegularExpressions;
#endif

namespace LinqToDB.LINQPad;

// IMPORTANT:
// 1. driver must be public or it will be missing from create connection dialog (existing connections will work)
// 2. don't rename class or namespace as it is used by LINQPad as driver identifier. If renamed, old connections will disappear from UI
/// <summary>
/// Implements LINQPad driver for synamic (scaffolded from DB schema) model.
/// </summary>
public sealed class LinqToDBDriver : DynamicDataContextDriver
{
	private MappingSchema? _mappingSchema;

	/// <inheritdoc/>
	public override string Name    => DriverHelper.Name;
	/// <inheritdoc/>
	public override string Author  => DriverHelper.Author;

	static LinqToDBDriver() => DriverHelper.Init();

	/// <inheritdoc/>
	public override string GetConnectionDescription(IConnectionInfo cxInfo) => DriverHelper.GetConnectionDescription(cxInfo);

	/// <inheritdoc/>
	public override DateTime? GetLastSchemaUpdate(IConnectionInfo cxInfo)
	{
		try
		{
			var settings = ConnectionSettings.Load(cxInfo);
			return DatabaseProviders.GetProvider(settings.Connection.Database).GetLastSchemaUpdate(settings);
		}
		catch (Exception ex)
		{
			DriverHelper.HandleException(ex, nameof(GetLastSchemaUpdate));
			return null;
		}
	}

	/// <inheritdoc/>
	public override bool ShowConnectionDialog(IConnectionInfo cxInfo, ConnectionDialogOptions dialogOptions) => DriverHelper.ShowConnectionDialog(cxInfo, dialogOptions, true);

#if !NETFRAMEWORK
	// TODO: switch to generator
	private static readonly Regex _runtimeTokenExtractor = new (@"^.+\\(?<token>[^\\]+)\\[^\\]+$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

	private static IEnumerable<string> GetFallbackTokens(string forToken)
	{
		switch (forToken)
		{
			case "net8.0":
				yield return "net8.0";
				goto case "net7.0";
			case "net7.0":
				yield return "net7.0";
				goto case "net6.0";
			case "net6.0":
				yield return "net6.0";
				goto case "net5.0";
			case "net5.0":
				yield return "net5.0";
				goto case "netcoreapp3.1";
			case "netcoreapp3.1":
				yield return "netcoreapp3.1";
				goto case "netstandard2.1";
			case "netstandard2.1":
				yield return "netstandard2.1";
				yield return "netstandard2.0";
				yield break;
		}

		yield return forToken;
	}

	private PortableExecutableReference MakeReferenceByRuntime(string runtimeToken, string reference)
	{
		var token = _runtimeTokenExtractor.Match(reference).Groups["token"].Value;

		foreach (var fallback in GetFallbackTokens(runtimeToken))
		{
			if (token == fallback)
				return MetadataReference.CreateFromFile(reference);

			var newReference = reference.Replace($"\\{token}\\", $"\\{fallback}\\", StringComparison.Ordinal);

			if (File.Exists(newReference))
				return MetadataReference.CreateFromFile(newReference);
		}

		return MetadataReference.CreateFromFile(reference);
	}
#endif

	/// <inheritdoc/>
	public override List<ExplorerItem> GetSchemaAndBuildAssembly(IConnectionInfo cxInfo, AssemblyName assemblyToBuild, ref string? nameSpace, ref string typeName)
	{
		try
		{
			var settings = ConnectionSettings.Load(cxInfo);
			var (items, text, providerAssemblyLocation) = DynamicSchemaGenerator.GetModel(settings, ref nameSpace, ref typeName);
			var syntaxTree                              = CSharpSyntaxTree.ParseText(text);

#if !NETFRAMEWORK
			// TODO: find better way to do it
			// hack to overwrite provider assembly references that target wrong runtime
			// e.g. referenceAssemblies contains path to net5 MySqlConnector
			// but GetCoreFxReferenceAssemblies returns netcoreapp3.1 runtime references
			var coreAssemblies = GetCoreFxReferenceAssemblies(cxInfo);
			var runtimeToken   = _runtimeTokenExtractor.Match(coreAssemblies[0]).Groups["token"].Value;
#endif
			var references = new List<MetadataReference>()
			{
#if NETFRAMEWORK
				MetadataReference.CreateFromFile(typeof(object).               Assembly.Location),
				MetadataReference.CreateFromFile(typeof(Enumerable).           Assembly.Location),
#pragma warning disable RS0030 // Do not use banned APIs
				MetadataReference.CreateFromFile(typeof(IDbConnection).        Assembly.Location),
#pragma warning restore RS0030 // Do not use banned APIs
				MetadataReference.CreateFromFile(typeof(PhysicalAddress)      .Assembly.Location),
				MetadataReference.CreateFromFile(typeof(BigInteger)           .Assembly.Location),
				MetadataReference.CreateFromFile(typeof(IAsyncDisposable)     .Assembly.Location),
				MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(string).Assembly.Location), "netstandard.dll")),
#endif
				MetadataReference.CreateFromFile(typeof(DataConnection).       Assembly.Location),
				MetadataReference.CreateFromFile(typeof(LINQPadDataConnection).Assembly.Location),
			};

			foreach (var assembly in DatabaseProviders.GetProvider(settings.Connection.Database).GetAdditionalReferences(settings.Connection.Provider!))
				references.Add(MetadataReference.CreateFromFile(assembly.Location));

#if !NETFRAMEWORK
			references.Add(MakeReferenceByRuntime(runtimeToken, providerAssemblyLocation));
			references.AddRange(coreAssemblies.Select(static path => MetadataReference.CreateFromFile(path)));
#else
			references.Add(MetadataReference.CreateFromFile(providerAssemblyLocation));
#endif

			var compilation = CSharpCompilation.Create(
				assemblyToBuild.Name!,
				syntaxTrees : [syntaxTree],
				references  : references,
				options     : new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

			using (var stream = new FileStream(assemblyToBuild.CodeBase!, FileMode.Create))
			{
				var result = compilation.Emit(stream);

				if (!result.Success)
				{
					var failures = result.Diagnostics.Where(static diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

					foreach (var diagnostic in failures)
						throw new LinqToDBLinqPadException(diagnostic.ToString());
				}
			}

			return items;
		}
		catch (Exception ex)
		{
			Notification.Error($"{ex}\n{ex.StackTrace}", "Schema Build Error");
			throw;
		}
	}

	private static readonly ParameterDescriptor[] _contextParameters =
	[
		new ParameterDescriptor("provider",         typeof(string).FullName),
		new ParameterDescriptor("providerPath",     typeof(string).FullName),
		new ParameterDescriptor("connectionString", typeof(string).FullName),
	];

	/// <inheritdoc/>
	public override ParameterDescriptor[] GetContextConstructorParameters(IConnectionInfo cxInfo) => _contextParameters;

	/// <inheritdoc/>
	public override object?[] GetContextConstructorArguments(IConnectionInfo cxInfo)
	{
		try
		{
			var settings = ConnectionSettings.Load(cxInfo);

			return
			[
				settings.Connection.Provider,
				settings.Connection.ProviderPath,
				settings.Connection.ConnectionString
			];
		}
		catch (Exception ex)
		{
			DriverHelper.HandleException(ex, nameof(GetContextConstructorArguments));
			return new object[3];
		}
	}

	/// <inheritdoc/>
	public override IEnumerable<string> GetAssembliesToAdd(IConnectionInfo cxInfo) => DriverHelper.GetAssembliesToAdd(cxInfo);

	/// <inheritdoc/>
	public override IEnumerable<string> GetNamespacesToAdd(IConnectionInfo cxInfo) => DriverHelper.DefaultImports;

	/// <inheritdoc/>
	public override void ClearConnectionPools(IConnectionInfo cxInfo) => DriverHelper.ClearConnectionPools(cxInfo);

	/// <inheritdoc/>
	public override void InitializeContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager)
	{
		_mappingSchema = DriverHelper.InitializeContext(cxInfo, (DataConnection)context, executionManager);
	}

	/// <inheritdoc/>
	public override void TearDownContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager, object[] constructorArguments)
	{
		try
		{
			((DataConnection)context).Dispose();
		}
		catch (Exception ex)
		{
			DriverHelper.HandleException(ex, nameof(TearDownContext));
		}
	}

	/// <inheritdoc/>
	public override IDbConnection GetIDbConnection(IConnectionInfo cxInfo)
	{
		try
		{
			return DatabaseProviders.CreateConnection(ConnectionSettings.Load(cxInfo));
		}
		catch (Exception ex)
		{
			DriverHelper.HandleException(ex, nameof(GetIDbConnection));
			throw;
		}
	}

	/// <inheritdoc/>
	public override void PreprocessObjectToWrite(ref object objectToWrite, ObjectGraphInfo info)
	{
		try
		{
			objectToWrite = ValueFormatter.Format(objectToWrite);
		}
		catch (Exception ex)
		{
			DriverHelper.HandleException(ex, nameof(PreprocessObjectToWrite));
		}
	}

	/// <inheritdoc/>
	public override DbProviderFactory GetProviderFactory(IConnectionInfo cxInfo)
	{
		try
		{
			return DatabaseProviders.GetProviderFactory(ConnectionSettings.Load(cxInfo));
		}
		catch (Exception ex)
		{
			DriverHelper.HandleException(ex, nameof(GetProviderFactory));
			throw;
		}
	}
}
