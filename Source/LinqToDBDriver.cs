using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using LINQPad.Extensibility.DataContext;
using LinqToDB.Data;
using LinqToDB.Mapping;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.SqlServer.Types;
#if !NETCORE
using System.Net.NetworkInformation;
using System.Numerics;
#else
using System.Text.RegularExpressions;
#endif

namespace LinqToDB.LINQPad;

sealed class LinqToDBDriver : DynamicDataContextDriver
{
#if NETCORE
	private static readonly Regex _runtimeTokenExtractor = new (@"^.+\\(?<token>[^\\]+)\\[^\\]+$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
#endif

	public override string Name   => "LINQ to DB";
	public override string Author => DriverHelper.Author;

	static LinqToDBDriver() => DriverHelper.Init();

	public override string GetConnectionDescription(IConnectionInfo cxInfo) => DriverHelper.GetConnectionDescription(cxInfo);

	public override DateTime? GetLastSchemaUpdate(IConnectionInfo cxInfo)
	{
		var providerName = (string?)cxInfo.DriverData.Element(CX.ProviderName);

		if (providerName == ProviderName.SqlServer)
			using (var db = new LINQPadDataConnection(cxInfo))
				return db.Query<DateTime?>("select max(modify_date) from sys.objects").FirstOrDefault();

		return null;
	}

	[Obsolete("base method obsoleted")]
	public override bool ShowConnectionDialog(IConnectionInfo cxInfo, bool isNewConnection) => DriverHelper.ShowConnectionDialog(cxInfo, isNewConnection, true);

#if NETCORE
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

	public override List<ExplorerItem> GetSchemaAndBuildAssembly(
		IConnectionInfo cxInfo, AssemblyName assemblyToBuild, ref string? nameSpace, ref string typeName)
	{
		try
		{
			var (items, text, referenceAssemblies) = ModelGenerator.GetModel(cxInfo, ref nameSpace, ref typeName);
			var syntaxTree = CSharpSyntaxTree.ParseText(text);

			var references = new List<MetadataReference>()
			{
#if !NETCORE
				MetadataReference.CreateFromFile(typeof(object).               Assembly.Location),
				MetadataReference.CreateFromFile(typeof(Enumerable).           Assembly.Location),
				MetadataReference.CreateFromFile(typeof(IDbConnection).        Assembly.Location),
				MetadataReference.CreateFromFile(typeof(PhysicalAddress)      .Assembly.Location),
				MetadataReference.CreateFromFile(typeof(BigInteger)           .Assembly.Location),
				MetadataReference.CreateFromFile(typeof(IAsyncDisposable)     .Assembly.Location),
#endif
				MetadataReference.CreateFromFile(typeof(DataConnection).       Assembly.Location),
				MetadataReference.CreateFromFile(typeof(LINQPadDataConnection).Assembly.Location),
				MetadataReference.CreateFromFile(typeof(SqlHierarchyId)       .Assembly.Location),
			};

#if NETCORE
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
					var failures = result.Diagnostics.Where(diagnostic =>
						diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

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

	public override ParameterDescriptor[] GetContextConstructorParameters(IConnectionInfo cxInfo)
	{
		return new[]
		{
			new ParameterDescriptor("provider",         typeof(string).FullName),
			new ParameterDescriptor("providerPath",     typeof(string).FullName),
			new ParameterDescriptor("connectionString", typeof(string).FullName),
		};
	}

	public override object?[] GetContextConstructorArguments(IConnectionInfo cxInfo)
	{
		return new object?[]
		{
			(string?)cxInfo.DriverData.Element(CX.ProviderName),
			(string?)cxInfo.DriverData.Element(CX.ProviderPath),
			cxInfo.DatabaseInfo.CustomCxString,
		};
	}

	public override IEnumerable<string> GetAssembliesToAdd(IConnectionInfo cxInfo)
	{
		yield return typeof(DataConnection).       Assembly.Location;
		yield return typeof(LINQPadDataConnection).Assembly.Location;

		var providerName = (string?)cxInfo.DriverData.Element(CX.ProviderName);
		var providerPath = (string?)cxInfo.DriverData.Element(CX.ProviderPath);

		foreach (var location in ProviderHelper.GetProvider(providerName, providerPath).GetAssemblyLocation(cxInfo.DatabaseInfo.CustomCxString))
		{
			yield return location;
		}
	}

	public override IEnumerable<string> GetNamespacesToAdd(IConnectionInfo cxInfo)
	{
		return new[]
		{
			"LinqToDB",
			"LinqToDB.Data",
			"LinqToDB.Mapping"
		};
	}

	public override void ClearConnectionPools(IConnectionInfo cxInfo) => DriverHelper.ClearConnectionPools(cxInfo);

	MappingSchema? _mappingSchema;
	bool           _useCustomFormatter;
	bool           _optimizeJoins;

	public override void InitializeContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager)
	{
		var conn = (DataConnection)context;

		_mappingSchema      = conn.MappingSchema;
		_useCustomFormatter = cxInfo.DriverData.Element(CX.UseCustomFormatter)?.Value.ToLower() == "true";

		_optimizeJoins      = cxInfo.DriverData.Element(CX.OptimizeJoins)      == null || cxInfo.DriverData.Element(CX.OptimizeJoins)     ?.Value.ToLower() == "true";

		Common.Configuration.Linq.OptimizeJoins      = _optimizeJoins;

		conn.OnTraceConnection = DriverHelper.GetOnTraceConnection(executionManager);
	}

	public override void TearDownContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager, object[] constructorArguments)
	{
		((DataConnection)context).Dispose();
	}

	public override IDbConnection? GetIDbConnection(IConnectionInfo cxInfo)
	{
		using var conn = new LINQPadDataConnection(cxInfo);
		if (conn.ConnectionString == null)
			return null;

		return conn.DataProvider.CreateConnection(conn.ConnectionString);
	}

	public override void PreprocessObjectToWrite (ref object? objectToWrite, ObjectGraphInfo info)
	{
		objectToWrite = _useCustomFormatter
			? XmlFormatter.Format(_mappingSchema!, objectToWrite)
			: XmlFormatter.FormatValue(objectToWrite);
	}
}
