using System;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;

using CodeJam.Strings;
using CodeJam.Xml;

using LinqToDB.Data;

using LINQPad.Extensibility.DataContext;

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

				var providerInfo = ProviderHelper.GetProvider(providerName);
				cxInfo.DatabaseInfo.Provider = providerInfo.GetConnectionNamespace();

				try
				{
					var provider = ProviderHelper.GetProvider(model.SelectedProvider.Name).GetDataProvider(model.ConnectionString);

					using (var db = new DataConnection(provider, model.ConnectionString))
					{
						db.CommandTimeout = model.CommandTimeout;

						cxInfo.DatabaseInfo.Provider  = db.Connection.GetType().Namespace;
						cxInfo.DatabaseInfo.Server    = ((DbConnection)db.Connection).DataSource;
						cxInfo.DatabaseInfo.Database  = db.Connection.Database;
						cxInfo.DatabaseInfo.DbVersion = ((DbConnection)db.Connection).ServerVersion;
					}
				}
				catch
				{
				}

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

			try
			{
				if (model.SelectedProvider != null)
				{
					var provider = ProviderHelper.GetProvider(model.SelectedProvider.Name).GetDataProvider(model.ConnectionString);

					using (var  con = provider.CreateConnection(model.ConnectionString))
					{
						con.Open();
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
