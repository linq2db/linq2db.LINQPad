using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using CodeJam;

using JetBrains.Annotations;

using LinqToDB.Common;
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

		#region ShowConnectionDialog

		public override bool ShowConnectionDialog(IConnectionInfo cxInfo, bool isNewConnection)
		{
			var model        = new ConnectionViewModel();
			var providerName = isNewConnection
				? ProviderName.SqlServer
				: (string)cxInfo.DriverData.Element("providerName");

			if (providerName != null)
				model.SelectedProvider = model.Providers.FirstOrDefault(p => p.Name == providerName);

			model.Name                     = cxInfo.DisplayName;
			model.Persist                  = cxInfo.Persist;
			model.IsProduction             = cxInfo.IsProduction;
			model.EncryptConnectionString  = cxInfo.DatabaseInfo.EncryptCustomCxString;
			model.Pluralize                = !cxInfo.DynamicSchemaOptions.NoPluralization;
			model.Capitalize               = !cxInfo.DynamicSchemaOptions.NoCapitalization;
			model.IncludeRoutines          = !cxInfo.DynamicSchemaOptions.ExcludeRoutines;
			model.ConnectionString         = cxInfo.DatabaseInfo.CustomCxString.IsNullOrWhiteSpace() ? (string)cxInfo.DriverData.Element("connectionString") : cxInfo.DatabaseInfo.CustomCxString;
			model.IncludeSchemas           = cxInfo.DriverData.Element("includeSchemas")          ?.Value;
			model.ExcludeSchemas           = cxInfo.DriverData.Element("excludeSchemas")          ?.Value;
			model.UseProviderSpecificTypes = cxInfo.DriverData.Element("useProviderSpecificTypes")?.Value.ToLower() == "true";
			model.UseCustomFormatter       = cxInfo.DriverData.Element("useCustomFormatter")      ?.Value.ToLower() == "true";

			_cxInfo = cxInfo;

			if (ConnectionDialog.Show(this, model))
			{
				providerName = model.SelectedProvider?.Name;

				cxInfo.DriverData.SetElementValue("providerName",             providerName);
				cxInfo.DriverData.SetElementValue("connectionString",         null);
				cxInfo.DriverData.SetElementValue("includeSchemas",           model.IncludeSchemas.IsNullOrWhiteSpace() ? null : model.IncludeSchemas);
				cxInfo.DriverData.SetElementValue("excludeSchemas",           model.ExcludeSchemas.IsNullOrWhiteSpace() ? null : model.ExcludeSchemas);
				cxInfo.DriverData.SetElementValue("useProviderSpecificTypes", model.UseProviderSpecificTypes ? "true" : null);
				cxInfo.DriverData.SetElementValue("useCustomFormatter",       model.UseCustomFormatter       ? "true" : null);

				switch (providerName)
				{
					case ProviderName.Access       : cxInfo.DatabaseInfo.Provider = typeof(AccessType).       Namespace; break;
					case ProviderName.DB2          :
					case ProviderName.DB2LUW       :
					case ProviderName.DB2zOS       : cxInfo.DatabaseInfo.Provider = typeof(DB2Type).          Namespace; break;
					case ProviderName.Informix     : cxInfo.DatabaseInfo.Provider = typeof(InformixType).     Namespace; break;
					case ProviderName.Firebird     : cxInfo.DatabaseInfo.Provider = typeof(FirebirdType).     Namespace; break;
					case ProviderName.PostgreSQL   : cxInfo.DatabaseInfo.Provider = typeof(PostgreSQLType).   Namespace; break;
					case ProviderName.OracleNative : cxInfo.DatabaseInfo.Provider = typeof(OracleNativeType). Namespace; break;
					case ProviderName.OracleManaged: cxInfo.DatabaseInfo.Provider = typeof(OracleManagedType).Namespace; break;
					case ProviderName.MySql        : cxInfo.DatabaseInfo.Provider = typeof(MySqlType).        Namespace; break;
					case ProviderName.SqlCe        : cxInfo.DatabaseInfo.Provider = typeof(SqlCeType).        Namespace; break;
					case ProviderName.SQLite       : cxInfo.DatabaseInfo.Provider = typeof(SQLiteType).       Namespace; break;
					case ProviderName.SqlServer    : cxInfo.DatabaseInfo.Provider = typeof(SqlServerType).    Namespace; break;
					case ProviderName.Sybase       : cxInfo.DatabaseInfo.Provider = typeof(SybaseType).       Namespace; break;
					case ProviderName.SapHana      : cxInfo.DatabaseInfo.Provider = typeof(SapHanaType).      Namespace; break;
				}

				try
				{
					using (var db = new LINQPadDataConnection(providerName, model.ConnectionString))
					{
						cxInfo.DatabaseInfo.Provider  = db.Connection.GetType().Namespace;
						cxInfo.DatabaseInfo.Server    = ((DbConnection)db.Connection).DataSource;
						cxInfo.DatabaseInfo.Database  = db.Connection.Database;
						cxInfo.DatabaseInfo.DbVersion = ((DbConnection)db.Connection).ServerVersion;
					}
				}
				catch
				{
				}

				cxInfo.DatabaseInfo.CustomCxString           =  model.ConnectionString;
				cxInfo.DatabaseInfo.EncryptCustomCxString    =  model.EncryptConnectionString;
				cxInfo.DynamicSchemaOptions.NoPluralization  = !model.Pluralize;
				cxInfo.DynamicSchemaOptions.NoCapitalization = !model.Capitalize;
				cxInfo.DynamicSchemaOptions.ExcludeRoutines  = !model.IncludeRoutines;
				cxInfo.Persist                               =  model.Persist;
				cxInfo.IsProduction                          =  model.IsProduction;
				cxInfo.DisplayName                           =  model.Name.IsNullOrWhiteSpace() ? null : model.Name;

				return true;
			}

			return false;
		}

		IConnectionInfo _cxInfo;

		public Exception TestConnection(ConnectionViewModel model)
		{
			if (model == null)
				return null;

//			var providerName = model.Providers[model.SelectedProvider];
//
//			switch (providerName)
//			{
//				case ProviderName.SQLite:
//					_cxInfo.DatabaseInfo.Provider = SQLiteTools.AssemblyName;
//
//					base.GetProviderFactory(_cxInfo);
//					
//					break;
//			}

			try
			{
				if (model.SelectedProvider != null)
				{
					using (var db = new DataConnection(model.SelectedProvider?.Name, model.ConnectionString))
					{
						var conn = db.Connection;
						return null;
					}
				}

				throw new InvalidOperationException();
			}
			catch (Exception ex)
			{
				return ex;
			}
		}

		#endregion

		public override List<ExplorerItem> GetSchemaAndBuildAssembly(
			IConnectionInfo cxInfo, AssemblyName assemblyToBuild, ref string nameSpace, ref string typeName)
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

			conn.OnTraceConnection = info =>
			{
				if (info.BeforeExecute)
				{
					executionManager.SqlTranslationWriter.WriteLine(info.SqlText);
				}
				else if (info.TraceLevel == TraceLevel.Error)
				{
					var sb = new StringBuilder();

					for (var ex = info.Exception; ex != null; ex = ex.InnerException)
					{
						sb
							.AppendLine()
							.AppendLine("/*")
							.AppendFormat("Exception: {0}", ex.GetType())
							.AppendLine()
							.AppendFormat("Message  : {0}", ex.Message)
							.AppendLine()
							.AppendLine(ex.StackTrace)
							.AppendLine("*/")
							;
					}

					executionManager.SqlTranslationWriter.WriteLine(sb.ToString());
				}
				else if (info.RecordsAffected != null)
				{
					executionManager.SqlTranslationWriter.WriteLine("-- Execution time: {0}. Records affected: {1}.\r\n".Args(info.ExecutionTime, info.RecordsAffected));
				}
				else
				{
					executionManager.SqlTranslationWriter.WriteLine("-- Execution time: {0}\r\n".Args(info.ExecutionTime));
				}
			};
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
