using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
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

using AccessType        = System.Data.OleDb.OleDbConnection;
using DB2Type           = IBM.Data.DB2.DB2Connection;
using InformixType      = IBM.Data.Informix.IfxConnection;
using FirebirdType      = FirebirdSql.Data.FirebirdClient.FbConnection;
using PostgreSQLType    = Npgsql.NpgsqlConnection;
using OracleNativeType  = Oracle.DataAccess.Client.OracleConnection;
using OracleManagedType = Oracle.ManagedDataAccess.Client.OracleConnection;
using MySqlType         = MySql.Data.MySqlClient.MySqlConnection;
using SqlCeType         = System.Data.SqlServerCe.SqlCeConnection;
using SQLiteType        = System.Data.SQLite.SQLiteConnection;
using SqlServerType     = System.Data.SqlClient.SqlConnection;
using SqlTypesType      = Microsoft.SqlServer.Types.SqlHierarchyId;
using SybaseType        = Sybase.Data.AseClient.AseConnection;
using SapHanaType       = Sap.Data.Hana.HanaConnection;

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
				MessageBox.Show($"{ex}\n{ex.StackTrace}");
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
				(string)(cxInfo.DriverData.Element("providerVersion") ?? cxInfo.DriverData.Element("providerName")),
				cxInfo.DatabaseInfo.CustomCxString,
			};
		}

		public override IEnumerable<string> GetAssembliesToAdd(IConnectionInfo cxInfo)
		{
			yield return typeof(IDbConnection).        Assembly.Location;
			yield return typeof(DataConnection).       Assembly.Location;
			yield return typeof(LINQPadDataConnection).Assembly.Location;

			var providerName = (string)cxInfo.DriverData.Element("providerName");

			switch (providerName)
			{
				case ProviderName.Access       : yield return typeof(AccessType).       Assembly.Location; break;
				case ProviderName.DB2          :
				case ProviderName.DB2LUW       :
				case ProviderName.DB2zOS       : yield return typeof(DB2Type).          Assembly.Location; break;
				case ProviderName.Informix     : yield return typeof(InformixType).     Assembly.Location; break;
				case ProviderName.Firebird     : yield return typeof(FirebirdType).     Assembly.Location; break;
				case ProviderName.PostgreSQL   : yield return typeof(PostgreSQLType).   Assembly.Location; break;
				case ProviderName.OracleNative : yield return typeof(OracleNativeType). Assembly.Location; break;
				case ProviderName.OracleManaged: yield return typeof(OracleManagedType).Assembly.Location; break;
				case ProviderName.MySql        : yield return typeof(MySqlType).        Assembly.Location; break;
				case ProviderName.SqlCe        : yield return typeof(SqlCeType).        Assembly.Location; break;
				case ProviderName.SQLite       : yield return typeof(SQLiteType).       Assembly.Location; break;
				case ProviderName.Sybase       : yield return typeof(SybaseType).       Assembly.Location; break;
				case ProviderName.SapHana      : yield return typeof(SapHanaType).      Assembly.Location; break;
				case ProviderName.SqlServer    :
					yield return typeof(SqlServerType).Assembly.Location;
					yield return typeof(SqlTypesType). Assembly.Location;
					break;
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
			{
				var connection = db.Connection as SqlConnection;
				if (connection != null)
					SqlConnection.ClearPool(connection);
			}
		}

		IDataProvider _dataProvider;
		MappingSchema _mappingSchema;
		bool          _useCustomFormatter;

		public override void InitializeContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager)
		{
			var conn = (DataConnection)context;

			_dataProvider       = conn.DataProvider;
			_mappingSchema      = conn.MappingSchema;
			_useCustomFormatter = cxInfo.DriverData.Element("useCustomFormatter")?.Value.ToLower() == "true";

			conn.OnTraceConnection = DriverHelper.GetOnTraceConnection(executionManager);
		}

		public override void TearDownContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager, object[] constructorArguments)
		{
			((DataConnection)context).Dispose();
		}

		public override IDbConnection GetIDbConnection(IConnectionInfo cxInfo)
		{
			using (var conn = new LINQPadDataConnection(cxInfo))
				return conn.DataProvider.CreateConnection(conn.ConnectionString);
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
