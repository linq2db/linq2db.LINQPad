using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using LINQPad.Extensibility.DataContext;
using LinqToDB.Data;
using LinqToDB.Mapping;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
#if !LPX6
using System.Net.NetworkInformation;
using System.Numerics;
#else
using System.Text.RegularExpressions;
#endif

namespace LinqToDB.LINQPad;

internal sealed class DynamicLinqToDBDriver : DynamicDataContextDriver
{
	private MappingSchema? _mappingSchema;
	private bool           _useCustomFormatter;

	public override string Name    => DriverHelper.Name;
	public override string Author  => DriverHelper.Author;

	static DynamicLinqToDBDriver() => DriverHelper.Init();

	public override string GetConnectionDescription(IConnectionInfo cxInfo) => DriverHelper.GetConnectionDescription(new (cxInfo));

	public override DateTime? GetLastSchemaUpdate(IConnectionInfo cxInfo)
	{
		var settings = new Settings(cxInfo);
		return settings.GetProvider()?.GetLastSchemaUpdate(settings);
	}

	public override bool ShowConnectionDialog(IConnectionInfo cxInfo, ConnectionDialogOptions dialogOptions) => DriverHelper.ShowConnectionDialog(new (cxInfo), dialogOptions, true);

#if LPX6
	// TODO: switch to generator
	private static readonly Regex _runtimeTokenExtractor = new (@"^.+\\(?<token>[^\\]+)\\[^\\]+$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

	public IEnumerable<string> GetFallbackTokens(string forToken)
	{
		switch (forToken)
		{
			case "net7.0":
				yield return "net7.0";
				yield return "net6.0";
				yield return "net5.0";
				yield return "netcoreapp3.1";
				yield return "netstandard2.1";
				yield return "netstandard2.0";
				yield break;
			case "net6.0":
				yield return "net6.0";
				yield return "net5.0";
				yield return "netcoreapp3.1";
				yield return "netstandard2.1";
				yield return "netstandard2.0";
				yield break;
			case "net5.0":
				yield return "net5.0";
				yield return "netcoreapp3.1";
				yield return "netstandard2.1";
				yield return "netstandard2.0";
				yield break;
			case "netcoreapp3.1":
				yield return "netcoreapp3.1";
				yield return "netstandard2.1";
				yield return "netstandard2.0";
				yield break;
			case "netstandard2.1":
				yield return "netstandard2.1";
				yield return "netstandard2.0";
				yield break;
		}

		yield return forToken;
	}
#endif

	public override List<ExplorerItem> GetSchemaAndBuildAssembly(IConnectionInfo cxInfo, AssemblyName assemblyToBuild, ref string? nameSpace, ref string typeName)
	{
		var settings = new Settings(cxInfo);
		var provider = settings.GetProvider();
		try
		{
			var (items, text, referenceAssemblies) = DynamicSchemaGenerator.GetModel(settings, ref nameSpace, ref typeName);
			var syntaxTree                         = CSharpSyntaxTree.ParseText(text);

			var references = new List<MetadataReference>()
			{
#if !LPX6
				MetadataReference.CreateFromFile(typeof(object).               Assembly.Location),
				MetadataReference.CreateFromFile(typeof(Enumerable).           Assembly.Location),
				MetadataReference.CreateFromFile(typeof(IDbConnection).        Assembly.Location),
				MetadataReference.CreateFromFile(typeof(PhysicalAddress)      .Assembly.Location),
				MetadataReference.CreateFromFile(typeof(BigInteger)           .Assembly.Location),
				MetadataReference.CreateFromFile(typeof(IAsyncDisposable)     .Assembly.Location),
#endif
				MetadataReference.CreateFromFile(typeof(DataConnection).       Assembly.Location),
				MetadataReference.CreateFromFile(typeof(LINQPadDataConnection).Assembly.Location),
			};

			if (provider != null)
				foreach (var assembly in provider.GetAdditionalReferences())
					MetadataReference.CreateFromFile(assembly.Location);

#if LPX6
			// TODO: find better way to do it
			// hack to overwrite provider assembly references that target wrong runtime
			// e.g. referenceAssemblies contains path to net5 MySqlConnector
			// but GetCoreFxReferenceAssemblies returns netcoreapp3.1 runtime references
			var coreAssemblies = GetCoreFxReferenceAssemblies(cxInfo);
			var runtimeToken   = _runtimeTokenExtractor.Match(coreAssemblies[0]).Groups["token"].Value;

			references.AddRange(coreAssemblies.Select(path => MetadataReference.CreateFromFile(path)));

			foreach (var reference in referenceAssemblies)
			{
				var found = false;
				var token = _runtimeTokenExtractor.Match(reference).Groups["token"].Value;

				foreach (var fallback in GetFallbackTokens(runtimeToken))
				{
					if (token == fallback)
					{
						found = true;
						references.Add(MetadataReference.CreateFromFile(reference));
						break;
					}

					var newReference = reference.Replace($"\\{token}\\", $"\\{fallback}\\");

					if (File.Exists(newReference))
					{
						references.Add(MetadataReference.CreateFromFile(newReference));
						found = true;
						break;
					}
				}

				if (!found)
					references.Add(MetadataReference.CreateFromFile(reference));
			}
#else
			references.AddRange(referenceAssemblies.Select(r => MetadataReference.CreateFromFile(r)));
#endif

			var compilation = CSharpCompilation.Create(
				assemblyToBuild.Name!,
				syntaxTrees : new[] { syntaxTree },
				references  : references,
				options     : new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

			using (var stream = new FileStream(assemblyToBuild.CodeBase!, FileMode.Create))
			{
				var result = compilation.Emit(stream);

				if (!result.Success)
				{
					var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

					foreach (var diagnostic in failures)
						throw new LinqToDBLinqPadException(diagnostic.ToString());
				}
			}

			return items;
		}
		catch (Exception ex)
		{
			MessageBox.Show($"{ex}\n{ex.StackTrace}", "Schema Build Error", MessageBoxButton.OK, MessageBoxImage.Error);
			Debug.WriteLine($"{ex}\n{ex.StackTrace}");
			throw;
		}
	}

	private static readonly ParameterDescriptor[] _contextParameters = new[]
	{
		new ParameterDescriptor("provider",         typeof(string).FullName),
		new ParameterDescriptor("providerPath",     typeof(string).FullName),
		new ParameterDescriptor("connectionString", typeof(string).FullName),
	};

	public override ParameterDescriptor[] GetContextConstructorParameters(IConnectionInfo cxInfo) => _contextParameters;

	public override object?[] GetContextConstructorArguments(IConnectionInfo cxInfo)
	{
		var settings = new Settings(cxInfo);

		return new object?[]
		{
			settings.Provider,
			settings.ProviderPath,
			cxInfo.DatabaseInfo.CustomCxString
		};
	}

	public override IEnumerable<string> GetAssembliesToAdd(IConnectionInfo cxInfo)
	{
		yield return typeof(DataConnection).       Assembly.Location;
		yield return typeof(LINQPadDataConnection).Assembly.Location;

		var settings = new Settings(cxInfo);

		foreach (var location in ProviderHelper.GetProvider(settings.Provider, settings.ProviderPath).GetAssemblyLocation(cxInfo.DatabaseInfo.CustomCxString))
			yield return location;
	}

	public override IEnumerable<string> GetNamespacesToAdd(IConnectionInfo cxInfo) => DriverHelper.DefaultImports;

	public override void ClearConnectionPools(IConnectionInfo cxInfo) => DriverHelper.ClearConnectionPools(new (cxInfo));

	public override void InitializeContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager)
	{
		(_mappingSchema, _useCustomFormatter) = DriverHelper.InitializeContext(new (cxInfo), (DataConnection)context, executionManager);
	}

	public override void TearDownContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager, object[] constructorArguments)
	{
		((DataConnection)context).Dispose();
	}

	public override IDbConnection? GetIDbConnection(IConnectionInfo cxInfo)
	{
		var settings = new Settings(cxInfo);

		using var conn = new LINQPadDataConnection(settings);

		if (conn.ConnectionString == null)
			return null;

		return conn.DataProvider.CreateConnection(conn.ConnectionString);
	}

	public override void PreprocessObjectToWrite(ref object? objectToWrite, ObjectGraphInfo info)
	{
		objectToWrite = _useCustomFormatter
			? XmlFormatter.Format(_mappingSchema!, objectToWrite)
			: XmlFormatter.FormatValue(objectToWrite);
	}
}
