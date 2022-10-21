using LINQPad.Extensibility.DataContext;
using LinqToDB.Data;
using LinqToDB.Mapping;

#if !LPX6
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Collections.Immutable;
#endif

namespace LinqToDB.LINQPad;

/// <summary>
/// Contains shared driver code for dynamic (scaffolded) and static (precompiled) drivers.
/// </summary>
internal static class DriverHelper
{
	public const string Name   = "Linq To DB";
	public const string Author = "Linq To DB Team";

	/// <summary>
	/// Returned by <see cref="DataContextDriver.GetNamespacesToAdd(IConnectionInfo)"/> method implementation.
	/// </summary>
	public static readonly IReadOnlyCollection<string> DefaultImports = new[]
	{
		"LinqToDB",
		"LinqToDB.Data",
		"LinqToDB.Mapping"
	};

/// <summary>
/// Initialization method, called from driver's static constructor.
/// </summary>
public static void Init()
	{
#if !LPX6
		ConfigureRedirects();
		SapHanaSPS04Fixes();
#endif
	}

	/// <summary>
	/// Implements <see cref="DataContextDriver.InitializeContext(IConnectionInfo, object, QueryExecutionManager)"/> method.
	/// </summary>
	public static (MappingSchema mappingSchema, bool useCustomFormatter) InitializeContext(
		Settings              settings,
		IDataContext          context,
		QueryExecutionManager executionManager)
	{
		// apply context-specific Linq To DB options
		Common.Configuration.Linq.OptimizeJoins = settings.OptimizeJoins;

		if (context is DataConnection dc)
		{
			dc.OnTraceConnection = GetSqlLogAction(executionManager);
			DataConnection.TurnTraceSwitchOn();
		}
		else if (context is DataContext dctx)
		{
			dctx.OnTraceConnection = GetSqlLogAction(executionManager);
			DataConnection.TurnTraceSwitchOn();
		}

		return (context.MappingSchema, settings.UseCustomFormatters);

		// Implements Linq To DB connection logging handler to feed SQL logs to LINQPad.
		static Action<TraceInfo> GetSqlLogAction(QueryExecutionManager executionManager)
		{
			return info =>
			{
				switch (info.TraceInfoStep)
				{
					case TraceInfoStep.BeforeExecute:
						// log SQL query
						executionManager.SqlTranslationWriter.WriteLine(info.SqlText);
						break;
					case TraceInfoStep.Error:
						// log error
						if (info.Exception != null)
						{
							for (var ex = info.Exception; ex != null; ex = ex.InnerException)
							{
								executionManager.SqlTranslationWriter.WriteLine();
								executionManager.SqlTranslationWriter.WriteLine("/*");
								executionManager.SqlTranslationWriter.WriteLine($"Exception: {ex.GetType()}");
								executionManager.SqlTranslationWriter.WriteLine($"Message  : {ex.Message}");
								executionManager.SqlTranslationWriter.WriteLine(ex.StackTrace);
								executionManager.SqlTranslationWriter.WriteLine("*/");
							}
						}
						break;
					case TraceInfoStep.Completed:
						// log data reader execution stats
						executionManager.SqlTranslationWriter.WriteLine($"-- Data read time: {info.ExecutionTime}. Records fetched: {info.RecordsAffected}.\r\n");
						break;
					case TraceInfoStep.AfterExecute:
						// log query execution stats
						executionManager.SqlTranslationWriter.WriteLine($"-- Execution time: {info.ExecutionTime}. Records affected: {info.RecordsAffected}.\r\n");
						break;
				}
			};
		}
	}

	/// <summary>
	/// Implements <see cref="DataContextDriver.GetConnectionDescription(IConnectionInfo)"/> method.
	/// </summary>
	public static string GetConnectionDescription(Settings settings)
	{
		var dbInfo = settings.ConnectionInfo.DatabaseInfo;

		// this is default connection name string in connecion explorer when user doesn't specify own name
		return $"[Linq To DB: {settings.Provider}] {dbInfo.Server}\\{dbInfo.Database} (v.{dbInfo.DbVersion})";
	}

	/// <summary>
	/// Implements <see cref="DataContextDriver.ClearConnectionPools(IConnectionInfo)"/> method.
	/// </summary>
	public static void ClearConnectionPools(Settings settings) => settings.GetProvider().ClearAllPools();

	/// <summary>
	/// Implements <see cref="DataContextDriver.ShowConnectionDialog(IConnectionInfo, ConnectionDialogOptions)"/> method.
	/// </summary>
	public static bool ShowConnectionDialog(Settings settings, ConnectionDialogOptions dialogOptions, bool isDynamic)
	{
		var model        = new ConnectionViewModel();
		var providerName = dialogOptions.IsNewConnection
			? ProviderName.SqlServer
			: settings.Database;

		var cxInfo = settings.ConnectionInfo;

		if (providerName != null)
			model.SelectedProvider = model.Providers.FirstOrDefault(p => p.Database == providerName);

		model.Name                     = cxInfo.DisplayName;
		model.IsDynamic                = isDynamic;
		model.CustomAssemblyPath       = cxInfo.CustomTypeInfo.CustomAssemblyPath;
		model.CustomTypeName           = cxInfo.CustomTypeInfo.CustomTypeName;
		model.AppConfigPath            = cxInfo.AppConfigPath;
		model.CustomConfiguration      = settings.CustomConfiguration;
		model.Persist                  = cxInfo.Persist;
		model.IsProduction             = cxInfo.IsProduction;
		model.EncryptConnectionString  = cxInfo.DatabaseInfo.EncryptCustomCxString;
		model.Pluralize                = !cxInfo.DynamicSchemaOptions.NoPluralization;
		model.Capitalize               = !cxInfo.DynamicSchemaOptions.NoCapitalization;
		// TODO
		//model.IncludeRoutines          = cxInfo.DriverData.Element(CX.ExcludeRoutines)?.Value.ToLower() == "false";
		model.IncludeFKs               = settings.LoadForeignKeys;
		model.ConnectionString         = settings.ConnectionString;
		// TODO
		//model.IncludeSchemas           = cxInfo.DriverData.Element(CX.IncludeSchemas)          ?.Value;
		//model.ExcludeSchemas           = cxInfo.DriverData.Element(CX.ExcludeSchemas)          ?.Value;
		//model.IncludeCatalogs          = cxInfo.DriverData.Element(CX.IncludeCatalogs)         ?.Value;
		//model.ExcludeCatalogs          = cxInfo.DriverData.Element(CX.ExcludeCatalogs)         ?.Value;
		model.UseProviderSpecificTypes = settings.UseProviderTypes;
		model.UseCustomFormatter       = settings.UseCustomFormatters;
		model.CommandTimeout           = settings.CommandTimeout;

		model.OptimizeJoins            = settings.OptimizeJoins;
		model.ProviderPath             = settings.ProviderPath;

		if (ConnectionDialog.Show(model, isDynamic ? TestConnection : null))
		{
			//providerName = model.SelectedProvider?.Name;
			var database = model.SelectedProvider?.Database;

			settings.Database = database;
			// TODO
			//settings.Provider     = providerName;
			settings.ProviderPath = model.ProviderPath;
			settings.ConnectionString = null;
			// TODO
			//cxInfo.DriverData.SetElementValue(CX.ExcludeRoutines, !model.IncludeRoutines ? "true" : "false");
			settings.LoadForeignKeys = model.IncludeFKs;
			// TODO
			//cxInfo.DriverData.SetElementValue(CX.IncludeSchemas,           string.IsNullOrWhiteSpace(model.IncludeSchemas ) ? null : model.IncludeSchemas);
			//cxInfo.DriverData.SetElementValue(CX.ExcludeSchemas,           string.IsNullOrWhiteSpace(model.ExcludeSchemas ) ? null : model.ExcludeSchemas);
			//cxInfo.DriverData.SetElementValue(CX.IncludeCatalogs,          string.IsNullOrWhiteSpace(model.IncludeCatalogs) ? null : model.IncludeSchemas);
			//cxInfo.DriverData.SetElementValue(CX.ExcludeCatalogs,          string.IsNullOrWhiteSpace(model.ExcludeCatalogs) ? null : model.ExcludeSchemas);
			settings.OptimizeJoins = model.OptimizeJoins;
			settings.UseProviderTypes = model.UseProviderSpecificTypes;
			settings.UseCustomFormatters = model.UseCustomFormatter;
			settings.CommandTimeout = model.CommandTimeout;

			try
			{
				if (model.ConnectionString != null)
				{
					var databaseProvider         = DatabaseProviders.GetProviderByName(providerName!);
					var provider                 = DatabaseProviders.GetDataProvider(providerName, model.ConnectionString, model.ProviderPath);
					cxInfo.DatabaseInfo.Provider = databaseProvider.GetProviderFactoryName(providerName!);

					using var cn = provider.CreateConnection(model.ConnectionString);

					cxInfo.DatabaseInfo.Server    = cn.DataSource;
					cxInfo.DatabaseInfo.Database  = cn.Database;
					cxInfo.DatabaseInfo.DbVersion = cn.ServerVersion;
				}
			}
			catch
			{
			}

			settings.CustomConfiguration = string.IsNullOrWhiteSpace(model.CustomConfiguration) ? null : model.CustomConfiguration;

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
			cxInfo.DisplayName                           = string.IsNullOrWhiteSpace(model.Name) ? null : model.Name;

			return true;
		}

		return false;

		static Exception? TestConnection(ConnectionViewModel? model)
		{
			if (model == null)
				return null;

			try
			{
				if (model.SelectedProvider == null)
					throw new LinqToDBLinqPadException("Database provider is not selected");

				if (model.ConnectionString == null)
					throw new LinqToDBLinqPadException("Connection string is not specified");

				var provider = DatabaseProviders.GetDataProvider(model.Name, model.ConnectionString, model.ProviderPath);

				using var con = provider.CreateConnection(model.ConnectionString);
				con.Open();
				return null;
			}
			catch (Exception ex)
			{
				return ex;
			}
		}
	}

#if !LPX6
	/// <summary>
	/// Dynamically resolve assembly bindings to currently used assembly version for transitive dependencies. Used by .NET Framework build (LINQPad 6).
	/// </summary>
	private static void ConfigureRedirects()
	{
		AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
		{
			var requestedAssembly = new AssemblyName(args.Name!);

			if (requestedAssembly.Name == "linq2db")
				return typeof(DataContext).Assembly;

			// manage transitive dependencies dll hell
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

	/// <summary>
	/// Try to apply load fix for some version of native SAP HANA provider.
	/// </summary>
	private static void SapHanaSPS04Fixes()
	{
		// recent SAP HANA provider (SPS04 040, fixed in 045) uses Assembly.GetEntryAssembly() calls during native dlls discovery, which
		// leads to NRE as it returns null under NETFX, so we need to fake this method result to unblock HANA testing
		// https://github.com/microsoft/vstest/issues/1834
		// https://dejanstojanovic.net/aspnet/2015/january/set-entry-assembly-in-unit-testing-methods/
		try
		{
			var assembly = Assembly.GetCallingAssembly();

			var manager            = new AppDomainManager();
			var entryAssemblyfield = manager.GetType().GetField("m_entryAssembly", BindingFlags.Instance | BindingFlags.NonPublic);
			entryAssemblyfield.SetValue(manager, assembly);

			var domain             = AppDomain.CurrentDomain;
			var domainManagerField = domain.GetType().GetField("_domainManager", BindingFlags.Instance | BindingFlags.NonPublic);
			domainManagerField.SetValue(domain, manager);
		}
		catch { }
	}
#endif
}
