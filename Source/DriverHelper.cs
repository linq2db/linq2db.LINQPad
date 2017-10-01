using System;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;

using CodeJam.Strings;
using CodeJam.Xml;

using LinqToDB.Common;
using LinqToDB.Data;

using LINQPad.Extensibility.DataContext;

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
using SybaseType        = Sybase.Data.AseClient.AseConnection;
using SapHanaType       = Sap.Data.Hana.HanaConnection;

namespace LinqToDB.LINQPad
{
	static class DriverHelper
	{
		#region ShowConnectionDialog

		public static bool ShowConnectionDialog(DataContextDriver driver, IConnectionInfo cxInfo, bool isNewConnection, bool isDynamic)
		{
			var model        = new ConnectionViewModel();
			var providerName = isNewConnection
				? ProviderName.SqlServer
				: (string)cxInfo.DriverData.Element("providerName");

			if (providerName != null)
				model.SelectedProvider = model.Providers.FirstOrDefault(p => p.Name == providerName);

			model.Name                     = cxInfo.DisplayName;
			model.IsDynamic                = isDynamic;
			model.CustomAssemblyPath       = cxInfo.CustomTypeInfo.CustomAssemblyPath;
			model.CustomTypeName           = cxInfo.CustomTypeInfo.CustomTypeName;
			model.AppConfigPath            = cxInfo.AppConfigPath;
			model.CustomConfiguration      = cxInfo.DriverData.Element("customConfiguration")?.Value;
			model.Persist                  = cxInfo.Persist;
			model.IsProduction             = cxInfo.IsProduction;
			model.EncryptConnectionString  = cxInfo.DatabaseInfo.EncryptCustomCxString;
			model.Pluralize                = !cxInfo.DynamicSchemaOptions.NoPluralization;
			model.Capitalize               = !cxInfo.DynamicSchemaOptions.NoCapitalization;
			model.IncludeRoutines          = !isNewConnection && !cxInfo.DynamicSchemaOptions.ExcludeRoutines;
			model.ConnectionString         = cxInfo.DatabaseInfo.CustomCxString.IsNullOrWhiteSpace() ? (string)cxInfo.DriverData.Element("connectionString") : cxInfo.DatabaseInfo.CustomCxString;
			model.IncludeSchemas           = cxInfo.DriverData.Element("includeSchemas")          ?.Value;
			model.ExcludeSchemas           = cxInfo.DriverData.Element("excludeSchemas")          ?.Value;
			model.IncludeCatalogs          = cxInfo.DriverData.Element("includeCatalogs")         ?.Value;
			model.ExcludeCatalogs          = cxInfo.DriverData.Element("excludeCatalogs")         ?.Value;
			//model.NormalizeNames           = cxInfo.DriverData.Element("normalizeNames")          ?.Value.ToLower() == "true";
			model.AllowMultipleQuery       = cxInfo.DriverData.Element("allowMultipleQuery")      ?.Value.ToLower() == "true";
			model.UseProviderSpecificTypes = cxInfo.DriverData.Element("useProviderSpecificTypes")?.Value.ToLower() == "true";
			model.UseCustomFormatter       = cxInfo.DriverData.Element("useCustomFormatter")      ?.Value.ToLower() == "true";
			model.CommandTimeout           = cxInfo.DriverData.ElementValueOrDefault("commandTimeout", str => str.ToInt32() ?? 0, 0);

			model.OptimizeJoins            = cxInfo.DriverData.Element("optimizeJoins") == null || cxInfo.DriverData.Element("optimizeJoins")?.Value.ToLower() == "true";

			if (ConnectionDialog.Show(model, isDynamic ? (Func<ConnectionViewModel,Exception>)TestConnection : null))
			{
				providerName = model.SelectedProvider?.Name;

				cxInfo.DriverData.SetElementValue("providerName",             providerName);
				cxInfo.DriverData.SetElementValue("connectionString",         null);
				cxInfo.DriverData.SetElementValue("includeSchemas",           model.IncludeSchemas. IsNullOrWhiteSpace() ? null : model.IncludeSchemas);
				cxInfo.DriverData.SetElementValue("excludeSchemas",           model.ExcludeSchemas. IsNullOrWhiteSpace() ? null : model.ExcludeSchemas);
				cxInfo.DriverData.SetElementValue("includeCatalogs",          model.IncludeCatalogs.IsNullOrWhiteSpace() ? null : model.IncludeSchemas);
				cxInfo.DriverData.SetElementValue("excludeCatalogs",          model.ExcludeCatalogs.IsNullOrWhiteSpace() ? null : model.ExcludeSchemas);
				cxInfo.DriverData.SetElementValue("optimizeJoins",            model.OptimizeJoins            ? "true" : "false");
				cxInfo.DriverData.SetElementValue("allowMultipleQuery",       model.AllowMultipleQuery       ? "true" : "false");
				//cxInfo.DriverData.SetElementValue("normalizeNames",           model.NormalizeNames           ? "true" : null);
				cxInfo.DriverData.SetElementValue("useProviderSpecificTypes", model.UseProviderSpecificTypes ? "true" : null);
				cxInfo.DriverData.SetElementValue("useCustomFormatter",       model.UseCustomFormatter       ? "true" : null);
				cxInfo.DriverData.SetElementValue("commandTimeout",           model.CommandTimeout.ToString());

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

				string providerVersion = null;

				try
				{
					using (var db = new LINQPadDataConnection(providerName, model.ConnectionString))
					{
						db.CommandTimeout = model.CommandTimeout;

						cxInfo.DatabaseInfo.Provider  = db.Connection.GetType().Namespace;
						cxInfo.DatabaseInfo.Server    = ((DbConnection)db.Connection).DataSource;
						cxInfo.DatabaseInfo.Database  = db.Connection.Database;
						cxInfo.DatabaseInfo.DbVersion = ((DbConnection)db.Connection).ServerVersion;

						if (providerName == ProviderName.SqlServer)
						{
							if (int.TryParse(cxInfo.DatabaseInfo.DbVersion?.Split('.')[0], out var version))
							{
								switch (version)
								{
									case  8 : providerVersion = ProviderName.SqlServer2000; break;
									case  9 : providerVersion = ProviderName.SqlServer2005; break;
									case 10 : providerVersion = ProviderName.SqlServer2008; break;
									case 11 : providerVersion = ProviderName.SqlServer2012; break;
									case 12 : providerVersion = ProviderName.SqlServer2014; break;
									default :
										if (version > 12)
											providerVersion = ProviderName.SqlServer2014;
										break;
								}
							}
						}
					}
				}
				catch
				{
				}

				cxInfo.DriverData.SetElementValue("providerVersion",     providerVersion);
				cxInfo.DriverData.SetElementValue("customConfiguration", model.CustomConfiguration.IsNullOrWhiteSpace() ? null : model.CustomConfiguration);

				cxInfo.CustomTypeInfo.CustomAssemblyPath     =  model.CustomAssemblyPath;
				cxInfo.CustomTypeInfo.CustomTypeName         =  model.CustomTypeName;
				cxInfo.AppConfigPath                         =  model.AppConfigPath;
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

		static Exception TestConnection(ConnectionViewModel model)
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
						// ReSharper disable once UnusedVariable
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

		public static Action<TraceInfo> GetOnTraceConnection(QueryExecutionManager executionManager)
		{
			return info =>
			{
				if (info.TraceInfoStep == TraceInfoStep.BeforeExecute)
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
							.AppendLine($"Exception: {ex.GetType()}")
							.AppendLine($"Message  : {ex.Message}")
							.AppendLine(ex.StackTrace)
							.AppendLine("*/")
							;
					}

					executionManager.SqlTranslationWriter.WriteLine(sb.ToString());
				}
				else if (info.RecordsAffected != null)
				{
					executionManager.SqlTranslationWriter.WriteLine($"-- Execution time: {info.ExecutionTime}. Records affected: {info.RecordsAffected}.\r\n");
				}
				else
				{
					executionManager.SqlTranslationWriter.WriteLine($"-- Execution time: {info.ExecutionTime}\r\n");
				}
			};
		}
	}
}
