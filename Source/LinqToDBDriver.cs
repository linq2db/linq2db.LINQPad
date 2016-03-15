using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Data.SqlServerCe;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using LinqToDB.Common;
using LinqToDB.Data;
using LinqToDB.DataProvider;
using LinqToDB.Mapping;

using LINQPad.Extensibility.DataContext;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LinqToDB.LINQPad
{
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
			model.ConnectionString         = string.IsNullOrWhiteSpace(cxInfo.DatabaseInfo.CustomCxString) ? (string)cxInfo.DriverData.Element("connectionString") : cxInfo.DatabaseInfo.CustomCxString;
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
				cxInfo.DriverData.SetElementValue("includeSchemas",           string.IsNullOrWhiteSpace(model.IncludeSchemas) ? null : model.IncludeSchemas);
				cxInfo.DriverData.SetElementValue("excludeSchemas",           string.IsNullOrWhiteSpace(model.ExcludeSchemas) ? null : model.ExcludeSchemas);
				cxInfo.DriverData.SetElementValue("useProviderSpecificTypes", model.UseProviderSpecificTypes ? "true" : null);
				cxInfo.DriverData.SetElementValue("useCustomFormatter",       model.UseCustomFormatter       ? "true" : null);

				switch (providerName)
				{
					case ProviderName.Access   : cxInfo.DatabaseInfo.Provider = typeof(OleDbConnection). Namespace; break;
					case ProviderName.SqlCe    : cxInfo.DatabaseInfo.Provider = typeof(SqlCeConnection). Namespace; break;
					case ProviderName.SQLite   : cxInfo.DatabaseInfo.Provider = typeof(SQLiteConnection).Namespace; break;
					case ProviderName.SqlServer: cxInfo.DatabaseInfo.Provider = typeof(SqlConnection).   Namespace; break;
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
				cxInfo.DisplayName                           = string.IsNullOrWhiteSpace(model.Name) ? null : model.Name;

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
				using (var db = new DataConnection(model.SelectedProvider?.Name, model.ConnectionString))
				{
					var conn = db.Connection;
					return null;
				}
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
			//using (var stream = new MemoryStream())
			{
				var result = compilation.Emit(stream);

				if (!result.Success)
				{
					var failures = result.Diagnostics.Where(diagnostic =>
						diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

					foreach (var diagnostic in failures)
						throw new Exception(diagnostic.ToString());
				}
				//else
				//{
				//	stream.Seek(0, SeekOrigin.Begin);
				//
				//	var assembly = Assembly.Load(stream.ToArray());
				//	var type     = assembly.GetType($"{nameSpace}.{typeName}");
				//	var obj      = Activator.CreateInstance(type);
				//}
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
			return new[]
			{
				typeof(IDbConnection).        Assembly.Location,
				typeof(DataConnection).       Assembly.Location,
				typeof(LINQPadDataConnection).Assembly.Location,
			};
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
				if (db.Connection is SqlConnection)
					SqlConnection.ClearPool((SqlConnection)db.Connection);
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
			if (_useCustomFormatter)
				objectToWrite = XmlFormatter.Format(_mappingSchema, objectToWrite);
			else
				objectToWrite = XmlFormatter.FormatValue(objectToWrite, info);
		}
	}
}
