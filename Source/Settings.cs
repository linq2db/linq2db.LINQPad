using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using LINQPad.Extensibility.DataContext;
using LinqToDB.DataProvider.DB2iSeries;

namespace LinqToDB.LINQPad
{
	internal struct Settings
	{
		private const int CURRENT_VERSION = 5;
		private const string LIST_ITEM_NODE = "item";

		private static readonly char[] _defaultListSeparators = new[]{ ',', ';' };

		/// <summary>
		/// Connection configuration version. Values:
		/// <list type="bullet">
		/// <item>null: pre-v5 settings</item>
		/// <item>5: v5 settings</item>
		/// </list>
		/// </summary>
		private const string SETTING_VERSION  = "configVersion";


		public Settings(IConnectionInfo cxInfo)
		{
			ConnectionInfo = cxInfo;

			MigrateSettings();
		}

		public IConnectionInfo ConnectionInfo { get; }

		/// <summary>
		/// Migrate pre-v5 connection settings.
		/// </summary>
		private void MigrateSettings()
		{
			if (GetInt32(SETTING_VERSION) == CURRENT_VERSION)
				return;

			// migrate pre-v5 settings

			// 1. create new addtional setting Database from providerName setting
			var providerName = Provider;
			Database         = providerName switch
			{
				ProviderName.Access or ProviderName.AccessOdbc         => ProviderName.Access,
				ProviderName.MySqlConnector                            => ProviderName.MySql,
				ProviderName.SybaseManaged                             => ProviderName.Sybase,
				ProviderName.SQLiteClassic                             => ProviderName.SQLite,
				ProviderName.InformixDB2                               => ProviderName.Informix,
				ProviderName.SapHanaNative or ProviderName.SapHanaOdbc => ProviderName.SapHana,
				ProviderName.OracleManaged                             => ProviderName.Oracle,
				ProviderName.Firebird
					or ProviderName.PostgreSQL
					or ProviderName.DB2LUW
					or ProviderName.DB2zOS
					or ProviderName.SqlServer
					or DB2iSeriesProviderName.DB2
					or ProviderName.SqlCe                              => providerName,
				_                                                      => throw new LinqToDBLinqPadException($"Unknown provider: {providerName}")
			};

			// 2. convert comma/semicolon-separated strings with schemas/catalogs to list + flag
			var strValue = GetString("excludeSchemas");
			var schemas = strValue == null ? null : new HashSet<string>(strValue.Split(_defaultListSeparators, StringSplitOptions.RemoveEmptyEntries));
			if (schemas != null && schemas.Count > 0)
			{
				IncludeSchemas = false;
				Schemas = schemas.AsReadOnly();
			}
			else
			{
				strValue = GetString("includeSchemas");
				schemas = strValue == null ? null : new HashSet<string>(strValue.Split(_defaultListSeparators, StringSplitOptions.RemoveEmptyEntries));
				if (schemas != null && schemas.Count > 0)
				{
					IncludeSchemas = true;
					Schemas = schemas.AsReadOnly();
				}
			}

			strValue = GetString("excludeCatalogs");
			var catalogs = strValue == null ? null : new HashSet<string>(strValue.Split(_defaultListSeparators, StringSplitOptions.RemoveEmptyEntries));
			if (catalogs != null && catalogs.Count > 0)
			{
				IncludeCatalogs = false;
				Catalogs = catalogs.AsReadOnly();
			}
			else
			{
				strValue = GetString("includeCatalogs");
				catalogs = strValue == null ? null : new HashSet<string>(strValue.Split(_defaultListSeparators, StringSplitOptions.RemoveEmptyEntries));
				if (catalogs != null && catalogs.Count > 0)
				{
					IncludeCatalogs = true;
					Catalogs = catalogs.AsReadOnly();
				}
			}

			// 3. routines/FK load flags
			LoadAggregateFunctions = LoadScalarFunctions = LoadTableFunctions = LoadProcedures = !GetBoolean("excludeRoutines", true).Value;
			LoadForeignKeys        = !GetBoolean("excludeFKs", false).Value;
		}

		#region Settings

		#region Connection
		/// <summary>
		/// Database name (generic name of database provider).
		/// </summary>
		public string? Database
		{
			get => GetString(Names.DATABASE);
			set => SetString(Names.DATABASE, value);
		}

		/// <summary>
		/// Database provider name (specific database provider to use with database).
		/// </summary>
		public string? Provider
		{
			get => GetString(Names.PROVIDER);
			set => SetString(Names.PROVIDER, value);
		}

		/// <summary>
		/// Database provider assembly path.
		/// </summary>
		public string? ProviderPath
		{
			get => GetString(Names.PROVIDER_PATH);
			set => SetString(Names.PROVIDER_PATH, value);
		}

		/// <summary>
		/// Command timeout.
		/// </summary>
		public int CommandTimeout
		{
			get => GetInt32(Names.COMMAND_TIMEOUT, 0).Value;
			set => GetInt32(Names.COMMAND_TIMEOUT, value);
		}

		/// <summary>
		/// Connection string.
		/// </summary>
		public string? ConnectionString
		{
			get => string.IsNullOrWhiteSpace(ConnectionInfo.DatabaseInfo.CustomCxString) ? GetString(Names.CONNECTION_STRING, null) : ConnectionInfo.DatabaseInfo.CustomCxString;
			set => SetString(Names.CONNECTION_STRING, value);
		}
		#endregion

		#region Driver
		/// <summary>
		/// Path to custom JSON configuration file for static context.
		/// </summary>
		public string? CustomConfiguration
		{
			get => GetString(Names.CUSTOM_CONFIGURATION);
			set => SetString(Names.CUSTOM_CONFIGURATION, value);
		}

		public bool UseCustomFormatters
		{
			get => GetBoolean(Names.USE_CUSTOM_FORMATTERS, false).Value;
			set => SetBoolean(Names.USE_CUSTOM_FORMATTERS, value);
		}
		#endregion

		#region Scaffold
		/// <summary>
		/// Include/exclude schemas, specified by <see cref="Schemas"/> option.
		/// </summary>
		public bool IncludeSchemas
		{
			get => GetBoolean(Names.INCLUDE_SCHEMAS, false).Value;
			set => SetBoolean(Names.INCLUDE_SCHEMAS, value);
		}

		/// <summary>
		/// List of schemas to include/exclude (defined by <see cref="IncludeSchemas"/> option).
		/// </summary>
		public IReadOnlySet<string>? Schemas
		{
			get => GetSet(Names.SCHEMAS, _ => _);
			set => SetSet(Names.SCHEMAS, value, _ => _);
		}

		/// <summary>
		/// Include/exclude catalogs, specified by <see cref="Catalogs"/> option.
		/// </summary>
		public bool IncludeCatalogs
		{
			get => GetBoolean(Names.INCLUDE_CATALOGS, false).Value;
			set => SetBoolean(Names.INCLUDE_CATALOGS, value);
		}

		/// <summary>
		/// List of catalogs to include/exclude (defined by <see cref="IncludeCatalogs"/> option).
		/// </summary>
		public IReadOnlySet<string>? Catalogs
		{
			get => GetSet(Names.CATALOGS, _ => _);
			set => SetSet(Names.CATALOGS, value, _ => _);
		}

		/// <summary>
		/// Populate stored procedures.
		/// </summary>
		public bool LoadProcedures
		{
			get => GetBoolean(Names.LOAD_PROCEDURES, false).Value;
			set => SetBoolean(Names.LOAD_PROCEDURES, value);
		}

		/// <summary>
		/// Populate table functions.
		/// </summary>
		public bool LoadTableFunctions
		{
			get => GetBoolean(Names.LOAD_TABLE_FUNCTIONS, false).Value;
			set => SetBoolean(Names.LOAD_TABLE_FUNCTIONS, value);
		}

		/// <summary>
		/// Populate scalar functions.
		/// </summary>
		public bool LoadScalarFunctions
		{
			get => GetBoolean(Names.LOAD_SCALAR_FUNCTIONS, false).Value;
			set => SetBoolean(Names.LOAD_SCALAR_FUNCTIONS, value);
		}

		/// <summary>
		/// Populate aggregate functions.
		/// </summary>
		public bool LoadAggregateFunctions
		{
			get => GetBoolean(Names.LOAD_AGGREGATE_FUNCTIONS, false).Value;
			set => SetBoolean(Names.LOAD_AGGREGATE_FUNCTIONS, value);
		}

		/// <summary>
		/// Populate foreign keys.
		/// </summary>
		public bool LoadForeignKeys
		{
			get => GetBoolean(Names.LOAD_FOREIGN_KEYS, true).Value;
			set => SetBoolean(Names.LOAD_FOREIGN_KEYS, value);
		}

		/// <summary>
		/// Use provider data types.
		/// </summary>
		public bool UseProviderTypes
		{
			get => GetBoolean(Names.USE_PROVIDER_TYPES, false).Value;
			set => SetBoolean(Names.USE_PROVIDER_TYPES, value);
		}
		#endregion

		#region Linq To DB
		public bool OptimizeJoins
		{
			get => GetBoolean(Names.OPTIMIZE_JOINS, Common.Configuration.Linq.OptimizeJoins).Value;
			set => SetBoolean(Names.OPTIMIZE_JOINS, value);
		}
		#endregion

		private static class LegacyNames
		{
			public const string ProviderName             = "providerName";
			public const string ProviderPath             = "providerPath";
			public const string ConnectionString         = "connectionString";
			public const string ExcludeRoutines          = "excludeRoutines";
			public const string ExcludeFKs               = "excludeFKs";
			public const string IncludeSchemas           = "includeSchemas";
			public const string ExcludeSchemas           = "excludeSchemas";
			public const string IncludeCatalogs          = "includeCatalogs";
			public const string ExcludeCatalogs          = "excludeCatalogs";
			public const string OptimizeJoins            = "optimizeJoins";
			public const string UseProviderSpecificTypes = "useProviderSpecificTypes";
			public const string UseCustomFormatter       = "useCustomFormatter";
			public const string CommandTimeout           = "commandTimeout";
			public const string NormalizeNames           = "normalizeNames";
			public const string CustomConfiguration      = "customConfiguration";
		}

		/// <summary>
		/// Names for settings nodes.
		/// </summary>
		private static class Names
		{
			#region Connection Settings
			/// <summary>
			/// Database name (generic provider name).
			/// </summary>
			public const string DATABASE = "database";

			/// <summary>
			/// Database provider name.
			/// </summary>
			public const string PROVIDER = "providerName";

			/// <summary>
			/// Database provider assembly path.
			/// </summary>
			public const string PROVIDER_PATH = "providerPath";

			/// <summary>
			/// Command timeout.
			/// </summary>
			public const string COMMAND_TIMEOUT = "commandTimeout";

			/// <summary>
			/// Connection string.
			/// </summary>
			public const string CONNECTION_STRING = "connectionString";
			#endregion

			#region Driver Settings
			/// <summary>
			/// Path to custom JSON configuration file for static context.
			/// </summary>
			public const string CUSTOM_CONFIGURATION = "customConfiguration";

			/// <summary>
			/// Enable use of custom formatters for some types.
			/// </summary>
			public const string USE_CUSTOM_FORMATTERS = "useCustomFormatter";
			#endregion

			#region Scaffold Settings
			/// <summary>
			/// List of schemas to include/exclude.
			/// </summary>
			public const string INCLUDE_SCHEMAS = "includeSchemas";
			/// <summary>
			/// List of schemas to include/exclude.
			/// </summary>
			public const string SCHEMAS = "schemas";
			/// <summary>
			/// List of schemas to include/exclude.
			/// </summary>
			public const string INCLUDE_CATALOGS = "includeCatalogs";
			/// <summary>
			/// List of catalogs to include/exclude.
			/// </summary>
			public const string CATALOGS = "catalogs";
			/// <summary>
			/// Enable/disable procedures load.
			/// </summary>
			public const string LOAD_PROCEDURES = "loadProcedures";
			/// <summary>
			/// Enable/disable table functions load.
			/// </summary>
			public const string LOAD_TABLE_FUNCTIONS = "loadTableFunctions";
			/// <summary>
			/// Enable/disable scalar functions load.
			/// </summary>
			public const string LOAD_SCALAR_FUNCTIONS = "loadScalarFunctions";
			/// <summary>
			/// Enable/disable aggregate functions load.
			/// </summary>
			public const string LOAD_AGGREGATE_FUNCTIONS = "loadAggregateFunctions";
			/// <summary>
			/// Enable/disable foreign keys load.
			/// </summary>
			public const string LOAD_FOREIGN_KEYS = "loadFKs";
			/// <summary>
			/// Use provider-specific types for mappings.
			/// </summary>
			public const string USE_PROVIDER_TYPES = "useProviderSpecificTypes";
			#endregion

			#region Linq To DB settings
			/// <summary>
			/// Value for <see cref="Common.Configuration.Linq.OptimizeJoins"/> Linq To DB setting.
			/// </summary>
			public const string OPTIMIZE_JOINS = "optimizeJoins";
			#endregion
		}

		#endregion

		#region Settings Management

		[return: NotNullIfNotNull(nameof(defaultValue))]
		private int? GetInt32(string settingName, int? defaultValue = null)
		{
			var strValue = GetString(settingName);

			if (strValue != null && int.TryParse(strValue, NumberStyles.None, CultureInfo.InvariantCulture, out var intValue))
				return intValue;

			return defaultValue;
		}

		private void SetInt32(string settingName, int? value)
		{
			//if (value != null)
			//	ConnectionInfo.DriverData.SetElementValue(settingName, value.Value.ToString(CultureInfo.InvariantCulture));
			//else
			//	ConnectionInfo.DriverData.Element(settingName)?.Remove();
		}

		[return: NotNullIfNotNull(nameof(defaultValue))]
		private bool? GetBoolean(string settingName, bool? defaultValue = null)
		{
			var strValue = GetString(settingName);

			return strValue == "true" ? true : strValue == "false" ? false : defaultValue;
		}

		private void SetBoolean(string settingName, bool? value)
		{
			//if (value != null)
			//	ConnectionInfo.DriverData.SetElementValue(settingName, value.Value ? "true" : "false");
			//else
			//	ConnectionInfo.DriverData.Element(settingName)?.Remove();
		}

		[return: NotNullIfNotNull(nameof(defaultValue))]
		private string? GetString(string settingName, string? defaultValue = null)
		{
			return ConnectionInfo.DriverData.Element(settingName)?.Value ?? defaultValue;
		}

		private void SetString(string settingName, string? value)
		{
			//if (value != null)
			//	ConnectionInfo.DriverData.SetElementValue(settingName, value);
			//else
			//	ConnectionInfo.DriverData.Element(settingName)?.Remove();
		}

		private IReadOnlySet<TValue>? GetSet<TValue>(string settingName, Func<string, TValue?> parser)
			where TValue: class
		{
			var children = ConnectionInfo.DriverData.Element(settingName)?.Elements(LIST_ITEM_NODE);
			if (children == null)
			{
				return null;
			}

			var items = new HashSet<TValue>();
			foreach (var node in children)
			{
				var item = parser(node.Value);
				if (item != null)
				{
					items.Add(item);
				}
			}

			return items.Count > 0 ? items.AsReadOnly() : null;
		}

		private void SetSet<TValue>(string settingName, IReadOnlySet<TValue>? values, Func<TValue, string> serializer)
		{
			//ConnectionInfo.DriverData.Element(settingName)?.Remove();

			//if (values != null && values.Count > 0)
			//{
			//	var children = new XElement[values.Count];
			//	var idx = 0;

			//	foreach (var value in values)
			//	{
			//		children[idx] = new XElement(LIST_ITEM_NODE, serializer(value));
			//		idx++;
			//	}

			//	ConnectionInfo.DriverData.Add(new XElement(settingName, children));
			//}
		}

		#endregion
	}
}
