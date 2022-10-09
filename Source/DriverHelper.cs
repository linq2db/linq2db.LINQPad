using System.Diagnostics;
using System.Text;
using CodeJam.Strings;
using CodeJam.Xml;
using LINQPad.Extensibility.DataContext;
using LinqToDB.Data;
using Microsoft.Data.SqlClient;

#if !LPX6
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Collections.Immutable;
#endif

namespace LinqToDB.LINQPad;

static class DriverHelper
{
	public const string Author = "Linq To DB Team";

	public static void Init()
	{
#if !LPX6
		ConfigureRedirects();
		SapHanaSPS04Fixes();
#endif
	}

	public static string GetConnectionDescription(IConnectionInfo cxInfo)
	{
		var providerName = (string?)cxInfo.DriverData.Element(CX.ProviderName);
		var dbInfo = cxInfo.DatabaseInfo;

		return $"[{providerName}] {dbInfo.Server}\\{dbInfo.Database} (v.{dbInfo.DbVersion})";
	}

	public static void ClearConnectionPools(IConnectionInfo cxInfo)
	{
		using var db = new LINQPadDataConnection(cxInfo);
		if (db.Connection is SqlConnection connection)
			SqlConnection.ClearPool(connection);
	}

	#region ShowConnectionDialog

	public static bool ShowConnectionDialog(IConnectionInfo cxInfo, bool isNewConnection, bool isDynamic)
	{
		var model        = new ConnectionViewModel();
		var providerName = isNewConnection
			? ProviderName.SqlServer
			: (string?)cxInfo.DriverData.Element(CX.ProviderName);

		if (providerName != null)
			model.SelectedProvider = model.Providers.FirstOrDefault(p => p.Name == providerName);

		model.Name                     = cxInfo.DisplayName;
		model.IsDynamic                = isDynamic;
		model.CustomAssemblyPath       = cxInfo.CustomTypeInfo.CustomAssemblyPath;
		model.CustomTypeName           = cxInfo.CustomTypeInfo.CustomTypeName;
		model.AppConfigPath            = cxInfo.AppConfigPath;
		model.CustomConfiguration      = cxInfo.DriverData.Element(CX.CustomConfiguration)?.Value;
		model.Persist                  = cxInfo.Persist;
		model.IsProduction             = cxInfo.IsProduction;
		model.EncryptConnectionString  = cxInfo.DatabaseInfo.EncryptCustomCxString;
		model.Pluralize                = !cxInfo.DynamicSchemaOptions.NoPluralization;
		model.Capitalize               = !cxInfo.DynamicSchemaOptions.NoCapitalization;
		model.IncludeRoutines          = cxInfo.DriverData.Element(CX.ExcludeRoutines)?.Value.ToLower() == "false";
		model.IncludeFKs               = cxInfo.DriverData.Element(CX.ExcludeFKs)?.Value.ToLower()      != "true";
		model.ConnectionString         = cxInfo.DatabaseInfo.CustomCxString.IsNullOrWhiteSpace() ? (string?)cxInfo.DriverData.Element(CX.ConnectionString) : cxInfo.DatabaseInfo.CustomCxString;
		model.IncludeSchemas           = cxInfo.DriverData.Element(CX.IncludeSchemas)          ?.Value;
		model.ExcludeSchemas           = cxInfo.DriverData.Element(CX.ExcludeSchemas)          ?.Value;
		model.IncludeCatalogs          = cxInfo.DriverData.Element(CX.IncludeCatalogs)         ?.Value;
		model.ExcludeCatalogs          = cxInfo.DriverData.Element(CX.ExcludeCatalogs)         ?.Value;
		//model.NormalizeNames           = cxInfo.DriverData.Element(CX.NormalizeNames)          ?.Value.ToLower() == "true";
		model.UseProviderSpecificTypes = cxInfo.DriverData.Element(CX.UseProviderSpecificTypes)?.Value.ToLower() == "true";
		model.UseCustomFormatter       = cxInfo.DriverData.Element(CX.UseCustomFormatter)      ?.Value.ToLower() == "true";
		model.CommandTimeout           = cxInfo.DriverData.ElementValueOrDefault(CX.CommandTimeout, str => str.ToInt32() ?? 0, 0);

		model.OptimizeJoins            = cxInfo.DriverData.Element(CX.OptimizeJoins) == null || cxInfo.DriverData.Element(CX.OptimizeJoins)?.Value.ToLower() == "true";
		model.ProviderPath             = (string?)cxInfo.DriverData.Element(CX.ProviderPath);

		if (ConnectionDialog.Show(model, isDynamic ? TestConnection : null))
		{
			providerName = model.SelectedProvider?.Name;

			cxInfo.DriverData.SetElementValue(CX.ProviderName,             providerName);
			cxInfo.DriverData.SetElementValue(CX.ProviderPath,             model.ProviderPath);
			cxInfo.DriverData.SetElementValue(CX.ConnectionString,         null);
			cxInfo.DriverData.SetElementValue(CX.ExcludeRoutines,          !model.IncludeRoutines ? "true" : "false");
			cxInfo.DriverData.SetElementValue(CX.ExcludeFKs,               !model.IncludeFKs      ? "true" : "false");
			cxInfo.DriverData.SetElementValue(CX.IncludeSchemas,           model.IncludeSchemas. IsNullOrWhiteSpace() ? null : model.IncludeSchemas);
			cxInfo.DriverData.SetElementValue(CX.ExcludeSchemas,           model.ExcludeSchemas. IsNullOrWhiteSpace() ? null : model.ExcludeSchemas);
			cxInfo.DriverData.SetElementValue(CX.IncludeCatalogs,          model.IncludeCatalogs.IsNullOrWhiteSpace() ? null : model.IncludeSchemas);
			cxInfo.DriverData.SetElementValue(CX.ExcludeCatalogs,          model.ExcludeCatalogs.IsNullOrWhiteSpace() ? null : model.ExcludeSchemas);
			cxInfo.DriverData.SetElementValue(CX.OptimizeJoins,            model.OptimizeJoins            ? "true" : "false");
			//cxInfo.DriverData.SetElementValue(CX.NormalizeNames,           model.NormalizeNames           ? "true" : null);
			cxInfo.DriverData.SetElementValue(CX.UseProviderSpecificTypes, model.UseProviderSpecificTypes ? "true" : null);
			cxInfo.DriverData.SetElementValue(CX.UseCustomFormatter,       model.UseCustomFormatter       ? "true" : null);
			cxInfo.DriverData.SetElementValue(CX.CommandTimeout,           model.CommandTimeout.ToString());

			try
			{
				if (model.ConnectionString != null)
				{
					var providerInfo             = ProviderHelper.GetProvider(providerName, model.ProviderPath);
					cxInfo.DatabaseInfo.Provider = providerInfo.GetConnectionNamespace();
					var provider                 = providerInfo.GetDataProvider(model.ConnectionString);

					using var db = new DataConnection(provider, model.ConnectionString)
					{
						CommandTimeout = model.CommandTimeout
					};

					cxInfo.DatabaseInfo.Provider  = db.Connection.GetType().Namespace;
					cxInfo.DatabaseInfo.Server    = db.Connection.DataSource;
					cxInfo.DatabaseInfo.Database  = db.Connection.Database;
					cxInfo.DatabaseInfo.DbVersion = db.Connection.ServerVersion;
				}
			}
			catch
			{
			}

			cxInfo.DriverData.SetElementValue(CX.CustomConfiguration, model.CustomConfiguration.IsNullOrWhiteSpace() ? null : model.CustomConfiguration);

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

	static Exception? TestConnection(ConnectionViewModel? model)
	{
		if (model == null)
			return null;

		try
		{
			if (model.SelectedProvider != null && model.ConnectionString != null)
			{
				var provider = ProviderHelper.GetProvider(model.SelectedProvider.Name, model.ProviderPath).GetDataProvider(model.ConnectionString);

				using var con = provider.CreateConnection(model.ConnectionString);
				con.Open();
				return null;
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

#if !LPX6
	static void ConfigureRedirects()
	{
		AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
		{
			var requestedAssembly = new AssemblyName(args.Name!);
			if (requestedAssembly.Name == "linq2db")
				return typeof(DataContext).Assembly;

			// manage netstandard dll hell
			if (requestedAssembly.Name == "System.Threading.Tasks.Extensions")
				return typeof(ValueTask).Assembly;
			if (requestedAssembly.Name == "System.Runtime.CompilerServices.Unsafe")
				return typeof(Unsafe).Assembly;
			if (requestedAssembly.Name == "System.Memory")
				return typeof(Span<>).Assembly;
			if (requestedAssembly.Name == "System.Buffers")
				return typeof(ArrayPool<>).Assembly;
			if (requestedAssembly.Name == "System.Collections.Immutable")
				return typeof(ImmutableArray).Assembly;

			return null;
		};
	}

	static void SapHanaSPS04Fixes()
	{
		// recent SAP HANA provider (SPS04 040, fixed in 045) uses Assembly.GetEntryAssembly() calls during native dlls discovery, which
		// leads to NRE as it returns null under NETFX, so we need to fake this method result to unblock HANA testing
		// https://github.com/microsoft/vstest/issues/1834
		// https://dejanstojanovic.net/aspnet/2015/january/set-entry-assembly-in-unit-testing-methods/
		try
		{
			var assembly = Assembly.GetCallingAssembly();

			var manager = new AppDomainManager();
			var entryAssemblyfield = manager.GetType().GetField("m_entryAssembly", BindingFlags.Instance | BindingFlags.NonPublic);
			entryAssemblyfield.SetValue(manager, assembly);

			var domain = AppDomain.CurrentDomain;
			var domainManagerField = domain.GetType().GetField("_domainManager", BindingFlags.Instance | BindingFlags.NonPublic);
			domainManagerField.SetValue(domain, manager);
		}
		catch { /* ne shmagla */ }
	}
#endif
}
