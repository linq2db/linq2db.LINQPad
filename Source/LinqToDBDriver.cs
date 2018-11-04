using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

using JetBrains.Annotations;

using LinqToDB.Data;
using LinqToDB.DataProvider;
using LinqToDB.Mapping;

using LINQPad.Extensibility.DataContext;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Net.NetworkInformation;

namespace LinqToDB.LINQPad
{
	[UsedImplicitly]
	public class LinqToDBDriver : DynamicDataContextDriver
	{
		public override string Name   => "LINQ to DB";
		public override string Author => "Igor Tkachev";

		public override string GetConnectionDescription(IConnectionInfo cxInfo)
		{
			var providerName = (string)cxInfo.DriverData.Element("providerName");
			var dbInfo       = cxInfo.DatabaseInfo;

			return $"[{providerName}] {dbInfo.Server}\\{dbInfo.Database} (v.{dbInfo.DbVersion})";
		}

		public override DateTime? GetLastSchemaUpdate(IConnectionInfo cxInfo)
		{
			var providerName = (string)cxInfo.DriverData.Element("providerName");

			if (providerName == ProviderName.SqlServer)
				using (var db = new LINQPadDataConnection(cxInfo))
					return db.Query<DateTime?>("select max(modify_date) from sys.objects").FirstOrDefault();

			return null;
		}

		public override bool ShowConnectionDialog(IConnectionInfo cxInfo, bool isNewConnection)
		{
			return DriverHelper.ShowConnectionDialog(this, cxInfo, isNewConnection, true);
		}

		public override List<ExplorerItem> GetSchemaAndBuildAssembly(
			IConnectionInfo cxInfo, AssemblyName assemblyToBuild, ref string nameSpace, ref string typeName)
		{
			try
			{
				var gen        = new SchemaAndCodeGenerator(cxInfo);
				var items      = gen.GetItemsAndCode(nameSpace, typeName).ToList();
				var text       = gen.Code.ToString();
				var syntaxTree = CSharpSyntaxTree.ParseText(text);

				var references = new List<MetadataReference>
				{
					MetadataReference.CreateFromFile(typeof(object).               Assembly.Location),
					MetadataReference.CreateFromFile(typeof(Enumerable).           Assembly.Location),
					MetadataReference.CreateFromFile(typeof(IDbConnection).        Assembly.Location),
					MetadataReference.CreateFromFile(typeof(DataConnection).       Assembly.Location),
					MetadataReference.CreateFromFile(typeof(LINQPadDataConnection).Assembly.Location),
					MetadataReference.CreateFromFile(typeof(PhysicalAddress)      .Assembly.Location),
				};

				references.AddRange(gen.References.Select(r => MetadataReference.CreateFromFile(r)));

				var compilation = CSharpCompilation.Create(
					assemblyToBuild.Name,
					syntaxTrees : new[] { syntaxTree },
					references  : references,
					options     : new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

				using (var stream = new FileStream(assemblyToBuild.CodeBase, FileMode.Create))
				{
					var result = compilation.Emit(stream);

					if (!result.Success)
					{
						var failures = result.Diagnostics.Where(diagnostic =>
							diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

						foreach (var diagnostic in failures)
							throw new Exception(diagnostic.ToString());
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
				new ParameterDescriptor("connectionString", typeof(string).FullName),
			};
		}

		public override object[] GetContextConstructorArguments(IConnectionInfo cxInfo)
		{
			return new object[]
			{
				(string)cxInfo.DriverData.Element("providerName"),
				cxInfo.DatabaseInfo.CustomCxString,
			};
		}

		public override IEnumerable<string> GetAssembliesToAdd(IConnectionInfo cxInfo)
		{
			yield return typeof(IDbConnection).        Assembly.Location;
			yield return typeof(DataConnection).       Assembly.Location;
			yield return typeof(LINQPadDataConnection).Assembly.Location;

			var providerName = (string)cxInfo.DriverData.Element("providerName");
			var providerInfo = ProviderHelper.GetProvider(providerName);

			foreach (var location in providerInfo.GetAssemblyLocation(cxInfo.DatabaseInfo.CustomCxString))
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
				"LinqToDB.Mapping",
			};
		}

		public override void ClearConnectionPools(IConnectionInfo cxInfo)
		{
			using (var db = new LINQPadDataConnection(cxInfo))
				if (db.Connection is SqlConnection connection)
					SqlConnection.ClearPool(connection);
		}

		IDataProvider _dataProvider;
		MappingSchema _mappingSchema;
		bool          _useCustomFormatter;
		bool          _optimizeJoins;
		bool          _allowMultipleQuery;

		public override void InitializeContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager)
		{
			var conn = (DataConnection)context;

			_dataProvider       = conn.DataProvider;
			_mappingSchema      = conn.MappingSchema;
			_useCustomFormatter = cxInfo.DriverData.Element("useCustomFormatter")?.Value.ToLower() == "true";

			_allowMultipleQuery = cxInfo.DriverData.Element("allowMultipleQuery") == null || cxInfo.DriverData.Element("allowMultipleQuery")?.Value.ToLower() == "true";
			_optimizeJoins      = cxInfo.DriverData.Element("optimizeJoins")      == null || cxInfo.DriverData.Element("optimizeJoins")     ?.Value.ToLower() == "true";

			Common.Configuration.Linq.OptimizeJoins      = _optimizeJoins;
			Common.Configuration.Linq.AllowMultipleQuery = _allowMultipleQuery;

			conn.OnTraceConnection = DriverHelper.GetOnTraceConnection(executionManager);
		}

		public override void TearDownContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager, object[] constructorArguments)
		{
			((DataConnection)context).Dispose();
		}

		public override IDbConnection GetIDbConnection(IConnectionInfo cxInfo)
		{
			using (var conn = new LINQPadDataConnection(cxInfo))
			{
				return conn.DataProvider.CreateConnection(conn.ConnectionString);
			}
		}

		public override void ExecuteESqlQuery(IConnectionInfo cxInfo, string query)
		{
			throw new Exception ("ESQL queries are not supported for this type of connection");
		}

		public override void PreprocessObjectToWrite (ref object objectToWrite, ObjectGraphInfo info)
		{
			objectToWrite = _useCustomFormatter
				? XmlFormatter.Format(_mappingSchema, objectToWrite)
				: XmlFormatter.FormatValue(objectToWrite, info);
		}
	}
}
