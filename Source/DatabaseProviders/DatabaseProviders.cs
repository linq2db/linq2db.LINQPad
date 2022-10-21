using Microsoft.Data.SqlClient;
using System.Data.SQLite;
using AdoNetCore.AseClient;
using FirebirdSql.Data.FirebirdClient;
using LinqToDB.DataProvider.DB2iSeries;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using System.Data.OleDb;
using System.Data.Odbc;
using LinqToDB.DataProvider.SapHana;
using LinqToDB.Data;
using System.Data;
using Microsoft.SqlServer.Types;
using System.Runtime.InteropServices;
using LinqToDB.DataProvider;
using LINQPad.Extensibility.DataContext;
#if !LPX6
using IBM.Data.DB2;
#else
using System.IO;
using System.Data.Common;
using IBM.Data.DB2.Core;
#endif

namespace LinqToDB.LINQPad
{
	internal static class DatabaseProviders
	{
		public static readonly IReadOnlyDictionary<string, IDatabaseProvider> Providers;
		public static readonly IReadOnlyDictionary<string, IDatabaseProvider> ProvidersByProviderName;

		static DatabaseProviders()
		{
			var providers = new Dictionary<string, IDatabaseProvider>();
			var providersByName = new Dictionary<string, IDatabaseProvider>();
			Providers = providers;
			ProvidersByProviderName = providersByName;

			Register(providers, providersByName, new AccessProvider());
			Register(providers, providersByName, new FirebirdProvider());
			Register(providers, providersByName, new MySqlProvider());
			Register(providers, providersByName, new PostgreSQLProvider());
			Register(providers, providersByName, new SybaseAseProvider());
			Register(providers, providersByName, new SQLiteProvider());
			Register(providers, providersByName, new SqlCeProvider());
			Register(providers, providersByName, new DB2LUWProvider());
			Register(providers, providersByName, new DB2zOSProvider());
			Register(providers, providersByName, new DB2iSeriesProvider());
			Register(providers, providersByName, new InformixProvider());
			Register(providers, providersByName, new SapHanaProvider());
			Register(providers, providersByName, new OracleProvider());
			Register(providers, providersByName, new SqlServerProvider());
			Register(providers, providersByName, new ClickHouseProvider());

			static void Register(Dictionary<string, IDatabaseProvider> providers, Dictionary<string, IDatabaseProvider> providersByName, IDatabaseProvider provider)
			{
				providers.Add(provider.Database, provider);
				foreach (var providerName in provider.ProviderNames)
					providersByName.Add(providerName, provider);
			}
		}

		public static IDataProvider GetDataProvider(this Settings settings) => GetDataProvider(settings.Provider, settings.ConnectionString, settings.ProviderPath);

		public static IDataProvider GetDataProvider(string? providerName, string? connectionString, string? providerPath)
		{
			if (string.IsNullOrWhiteSpace(providerName))
				throw new LinqToDBLinqPadException("Can not activate provider. Provider is not selected.");

			if (string.IsNullOrWhiteSpace(connectionString))
				throw new LinqToDBLinqPadException($"Can not activate provider '{providerName}'. Connection string not specified.");

			if (providerPath != null)
				GetProviderByName(providerName!).RegisterProviderFactory(providerName!, providerPath);

			return DataConnection.GetDataProvider(providerName!, connectionString!)
				?? throw new LinqToDBLinqPadException($"Can not activate provider '{providerName}'");
		}

		public static IDatabaseProvider GetProviderByName(string providerName)
		{
			if (ProvidersByProviderName.TryGetValue(providerName, out var provider))
				return provider;

			throw new LinqToDBLinqPadException($"Cannot find database provider '{providerName}'");
		}

		public static IDatabaseProvider GetProvider(this Settings settings)
		{
			var database = settings.Database;

			if (database != null && Providers.TryGetValue(database, out var provider))
				return provider;

			throw new LinqToDBLinqPadException($"Cannot find provider for database '{database}'");
		}
	}

	/// <summary>
	/// Provides database provider abstraction.
	/// </summary>
	internal interface IDatabaseProvider
	{
		/// <summary>
		/// Gets database name (generic <see cref="ProviderName"/> value).
		/// </summary>
		string Database { get; }

		/// <summary>
		/// Gets database provider name for UI.
		/// </summary>
		string Description { get; }

		/// <summary>
		/// Returns provider factory name. Used to initialize <see cref="IDatabaseInfo.Provider"/>, but probably we don't need it at all as we override
		/// <see cref="DataContextDriver.GetIDbConnection(IConnectionInfo)"/>.
		/// </summary>
		string? GetProviderFactoryName(string providerName);

		/// <summary>
		/// Release all connections.
		/// </summary>
		void ClearAllPools();

		/// <summary>
		/// Returns last schema update time.
		/// </summary>
		DateTime? GetLastSchemaUpdate(Settings settings);

		/// <summary>
		/// Returns additional reference assemblies for dynamic model compilation (except main provider assembly).
		/// </summary>
		IReadOnlyCollection<Assembly> GetAdditionalReferences();

		/// <summary>
		/// List of supported provider names for provider.
		/// </summary>
		IReadOnlyCollection<string> ProviderNames { get; }

		/// <summary>
		/// Returns <c>true</c>, if specified provider for current database provider supports provider assembly path configuration.
		/// </summary>
		bool IsProviderPathSupported(string providerName);

		/// <summary>
		/// If provider supports assembly path configuration, method
		/// returns help text for configuration UI to help user locate and/or install provider.
		/// </summary>
		string? GetProviderAssemblyName(string providerName);

		/// <summary>
		/// If provider supports assembly path configuration, method could return URL to provider download page.
		/// </summary>
		string? GetProviderDownloadUrl(string providerName);

		/// <summary>
		/// If provider supports assembly path configuration (<see cref="IsProviderPathSupported(string)"/>), method tries to return default path to provider assembly,
		/// but only if assembly exists on specified path.
		/// </summary>
		string? TryGetDefaultPath(string providerName);

		/// <summary>
		/// If provider supports assembly path configuration (<see cref="IsProviderPathSupported(string)"/>), method
		/// performs provider factory registration to allow Linq To DB locate provider assembly.
		/// </summary>
		void RegisterProviderFactory(string providerName, string providerPath);
	}

	internal class AccessProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => ProviderName.Access;

		string IDatabaseProvider.Description => "Microsoft Access";

		private static readonly string[] _providers = new []
		{
			ProviderName.Access,
			ProviderName.AccessOdbc,
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		void IDatabaseProvider.ClearAllPools()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				OleDbConnection.ReleaseObjectPool();
			}
			OdbcConnection.ReleaseObjectPool();
		}

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var cn = settings.GetDataProvider().CreateConnection(settings.ConnectionString!);

			// only OLE DB schema has required information
			if (cn is OleDbConnection)
			{
				var dt1 = cn.GetSchema("Tables"    ).Rows.Cast<DataRow>().Max(r => (DateTime)r["DATE_MODIFIED"]);
				var dt2 = cn.GetSchema("Procedures").Rows.Cast<DataRow>().Max(r => (DateTime)r["DATE_MODIFIED"]);
				return dt1 > dt2 ? dt1 : dt2;
			}

			return null;
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => false;
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;

		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}

		string IDatabaseProvider.GetProviderFactoryName(string providerName) => providerName == ProviderName.Access ? "System.Data.OleDb" : "System.Data.Odbc";
	}

	internal class FirebirdProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => ProviderName.Firebird;

		string IDatabaseProvider.Description => "Firebird";

		private static readonly string[] _providers = new []
		{
			ProviderName.Firebird,
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		void IDatabaseProvider.ClearAllPools() => FbConnection.ClearAllPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			// no information in schema
			return null;
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => false;
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;

		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}

		// https://github.com/FirebirdSQL/NETProvider/blob/master/src/EntityFramework.Firebird/FbProviderServices.cs#L39
		string IDatabaseProvider.GetProviderFactoryName(string providerName) => "FirebirdSql.Data.FirebirdClient";
	}

	internal class MySqlProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => ProviderName.MySql;

		string IDatabaseProvider.Description => "MySql/MariaDB";

		private static readonly string[] _providers = new []
		{
			ProviderName.MySqlConnector,
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		void IDatabaseProvider.ClearAllPools() => MySqlConnection.ClearAllPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(u.time) FROM (SELECT MAX(UPDATE_TIME) AS time FROM information_schema.TABLES UNION SELECT MAX(CREATE_TIME) FROM information_schema.TABLES UNION SELECT MAX(LAST_ALTERED) FROM information_schema.ROUTINES) as u").FirstOrDefault();
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => false;
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;

		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}

		// https://github.com/mysql-net/MySqlConnector/blob/master/docs/content/overview/dbproviderfactories.md
		string IDatabaseProvider.GetProviderFactoryName(string providerName) => "MySqlConnector";
	}

	internal class PostgreSQLProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => ProviderName.PostgreSQL;

		string IDatabaseProvider.Description => "PostgreSQL";

		private static readonly string[] _providers = new []
		{
			ProviderName.PostgreSQL92,
			ProviderName.PostgreSQL93,
			ProviderName.PostgreSQL95,
			ProviderName.PostgreSQL15,
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		void IDatabaseProvider.ClearAllPools() => NpgsqlConnection.ClearAllPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			// no information in schema
			return null;
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => false;
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;

		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}

		string IDatabaseProvider.GetProviderFactoryName(string providerName) => "Npgsql";
	}

	internal class SybaseAseProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => ProviderName.Sybase;

		string IDatabaseProvider.Description => "SAP/Sybase ASE";

		private static readonly string[] _providers = new []
		{
			ProviderName.SybaseManaged
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		void IDatabaseProvider.ClearAllPools() => AseConnection.ClearPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(crdate) FROM sysobjects").FirstOrDefault();
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => false;
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;

		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}

		// there is no provider-defined default factory name, use assembly name
		string IDatabaseProvider.GetProviderFactoryName(string providerName) => "AdoNetCore.AseClient";
	}

	internal class SQLiteProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => ProviderName.SQLite;

		string IDatabaseProvider.Description => "SQLite";

		private static readonly string[] _providers = new []
		{
			ProviderName.SQLiteClassic
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		void IDatabaseProvider.ClearAllPools() => SQLiteConnection.ClearAllPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			// no information in schema
			return null;
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => false;
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;

		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}

		// there is no provider-defined default factory name, use assembly/namespace name
		string IDatabaseProvider.GetProviderFactoryName(string providerName) => "System.Data.SQLite";
	}

	internal class SqlCeProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => ProviderName.SqlCe;

		string IDatabaseProvider.Description => "Microsoft SQL Server Compact Edition (SQL CE)";

		private static readonly string[] _providers = new []
		{
			ProviderName.SqlCe
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		void IDatabaseProvider.ClearAllPools()
		{
			// connection pooling not supported by provider
		}

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			// no information in schema
			return null;
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => true;

#if LPX6
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => "System.Data.SqlServerCe.dll";
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => "https://www.microsoft.com/en-us/download/details.aspx?id=30709";
		string? IDatabaseProvider.TryGetDefaultPath(string providerName)
		{
			var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
			if (!string.IsNullOrEmpty(programFiles))
			{

				var path = Path.Combine(programFiles, "Microsoft SQL Server Compact Edition\\v4.0\\Private\\System.Data.SqlServerCe.dll");

				if (File.Exists(path))
				{
					return path;
				}
			}

			return null;
		}

		private static bool _factoryRegistered;

		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
			if (_factoryRegistered)
				return;

			if (!File.Exists(providerPath))
			{
				throw new LinqToDBLinqPadException($"Cannot find SQL CE provider assembly at '{providerPath}'");
			}

			try
			{
				var assembly = Assembly.LoadFrom(providerPath);
				DbProviderFactories.RegisterFactory("System.Data.SqlServerCe.4.0", assembly.GetType("System.Data.SqlServerCe.SqlCeProviderFactory")!);
				_factoryRegistered = true;
			}
			catch (Exception ex)
			{
				throw new LinqToDBLinqPadException($"Failed to initialize SQL CE provider factory: ({ex.GetType().Name}) {ex.Message}");
			}
		}

#else
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;

		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}
#endif

		string IDatabaseProvider.GetProviderFactoryName(string providerName) => "System.Data.SqlServerCe.4.0";
	}

	internal class DB2LUWProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => ProviderName.DB2LUW;

		string IDatabaseProvider.Description => "DB2 for Linux, UNIX and Windows (LUW)";

		private static readonly string[] _providers = new []
		{
			ProviderName.DB2LUW
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		void IDatabaseProvider.ClearAllPools() => DB2Connection.ReleaseObjectPool();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(TIME) FROM (SELECT MAX(ALTER_TIME) AS TIME FROM SYSCAT.ROUTINES UNION SELECT MAX(ALTER_TIME) AS TIME FROM SYSCAT.TABLES)").FirstOrDefault();
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => false;
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;
		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}

		string IDatabaseProvider.GetProviderFactoryName(string providerName) => "IBM.Data.DB2";
	}

	// z/OS provider not tested at all as we don't have access to database instance
	internal class DB2zOSProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => ProviderName.DB2zOS;

		string IDatabaseProvider.Description => "DB2 for z/OS";

		private static readonly string[] _providers = new []
		{
			ProviderName.DB2zOS
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		void IDatabaseProvider.ClearAllPools() => DB2Connection.ReleaseObjectPool();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(TIME) FROM (SELECT MAX(ALTEREDTS) AS TIME FROM SYSIBM.SYSROUTINES UNION SELECT MAX(ALTEREDTS) AS TIME FROM SYSIBM.SYSTABLES)").FirstOrDefault();
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => false;
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;
		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}

		string IDatabaseProvider.GetProviderFactoryName(string providerName) => "IBM.Data.DB2";
	}

	internal class DB2iSeriesProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => DB2iSeriesProviderName.DB2;

		string IDatabaseProvider.Description => "DB2 for iSeries";

		private static readonly string[] _providers = new []
		{
			// TODO: correct?
			DB2iSeriesProviderName.DB2
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		void IDatabaseProvider.ClearAllPools() => DB2Connection.ReleaseObjectPool();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(TIME) FROM (SELECT MAX(LAST_ALTERED) AS TIME FROM QSYS2.SYSROUTINES UNION SELECT MAX(ROUTINE_CREATED) AS TIME FROM QSYS2.SYSROUTINES UNION SELECT MAX(LAST_ALTERED_TIMESTAMP) AS TIME FROM QSYS2.SYSTABLES)").FirstOrDefault();
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => false;
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;
		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}

		string IDatabaseProvider.GetProviderFactoryName(string providerName) => "IBM.Data.DB2";
	}

	internal class InformixProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => ProviderName.Informix;

		string IDatabaseProvider.Description => "Informix";

		private static readonly string[] _providers = new []
		{
			ProviderName.InformixDB2,
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		void IDatabaseProvider.ClearAllPools() => DB2Connection.ReleaseObjectPool();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			// Informix provides only table creation date without time, which is useless
			return null;
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => false;
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;
		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}

		string IDatabaseProvider.GetProviderFactoryName(string providerName) => "IBM.Data.DB2";
	}

	internal class SapHanaProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => ProviderName.SapHana;

		string IDatabaseProvider.Description => "SAP HANA";

		private static readonly string[] _providers = new []
		{
			ProviderName.SapHanaNative,
			ProviderName.SapHanaOdbc,
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		void IDatabaseProvider.ClearAllPools()
		{
			OdbcConnection.ReleaseObjectPool();

			var typeName = $"{SapHanaProviderAdapter.ClientNamespace}.HanaConnection, {SapHanaProviderAdapter.AssemblyName}";
			Type.GetType(typeName, false)?.GetMethod("ClearAllPools", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
		}

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(time) FROM (SELECT MAX(CREATE_TIME) AS time FROM M_CS_TABLES UNION SELECT MAX(MODIFY_TIME) FROM M_CS_TABLES UNION SELECT MAX(CREATE_TIME) FROM M_RS_TABLES UNION SELECT MAX(CREATE_TIME) FROM PROCEDURES UNION SELECT MAX(CREATE_TIME) FROM FUNCTIONS)").FirstOrDefault();
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => providerName.Contains(ProviderName.SapHanaNative);

#if LPX6
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => providerName.Contains(ProviderName.SapHanaNative) ? "Sap.Data.Hana.Core.v2.1.dll" : null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => providerName.Contains(ProviderName.SapHanaNative) ? "https://tools.hana.ondemand.com/#hanatools" : null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName)
		{
			if (providerName.Contains(ProviderName.SapHanaNative))
			{
				var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
				if (!string.IsNullOrEmpty(programFiles))
				{

					var path = Path.Combine(programFiles, "sap\\hdbclient\\dotnetcore\\v2.1\\Sap.Data.Hana.Core.v2.1.dll");

					if (File.Exists(path))
					{
						return path;
					}
				}
			}

			return null;
		}

		private static bool _factoryRegistered;
		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
			if (providerName.Contains(ProviderName.SapHanaNative) && !_factoryRegistered)
			{
				if (!File.Exists(providerPath))
				{
					throw new LinqToDBLinqPadException($"Cannot find SAP HANA provider assembly at '{providerPath}'");
				}

				try
				{
					var sapHanaAssembly = Assembly.LoadFrom(providerPath);
					DbProviderFactories.RegisterFactory("Sap.Data.Hana", sapHanaAssembly.GetType("Sap.Data.Hana.HanaFactory")!);
					_factoryRegistered = true;
				}
				catch (Exception ex)
				{
					throw new LinqToDBLinqPadException($"Failed to initialize SAP HANA provider factory: ({ex.GetType().Name}) {ex.Message}");
				}

				// TODO: remove after testing
				//try
				//{
				//	var targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory!, Path.GetFileName(providerPath));

				//	if (File.Exists(providerPath))
				//	{
				//		// original path contains spaces which breaks broken native dlls discovery logic in SAP provider
				//		// (at least SPS04 040)
				//		// if you run tests from path with spaces - it will not help you
				//		File.Copy(providerPath, targetPath, true);
				//		var sapHanaAssembly = Assembly.LoadFrom(targetPath);
				//		DbProviderFactories.RegisterFactory("Sap.Data.Hana", sapHanaAssembly.GetType("Sap.Data.Hana.HanaFactory")!);
				//		_factoryRegistered = true;
				//	}
				//}
				//catch (Exception ex)
				//{
				//	throw new LinqToDBLinqPadException($"Failed to register SAP HANA factory: ({ex.GetType().Name}) {ex.Message}");
				//}
			}
		}
#else
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;
		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}
#endif

		string IDatabaseProvider.GetProviderFactoryName(string providerName) => providerName == ProviderName.SapHanaNative ? "Sap.Data.Hana" : "System.Data.Odbc";
	}

	internal class OracleProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => ProviderName.Oracle;

		string IDatabaseProvider.Description => "Oracle";

		private static readonly string[] _providers = new []
		{
			ProviderName.Oracle11Managed,
			ProviderName.OracleManaged,
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		void IDatabaseProvider.ClearAllPools() => OracleConnection.ClearAllPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(LAST_DDL_TIME) FROM USER_OBJECTS WHERE OBJECT_TYPE IN ('TABLE', 'VIEW', 'INDEX', 'FUNCTION', 'PACKAGE', 'PACKAGE BODY', 'PROCEDURE', 'MATERIALIZED VIEW')").FirstOrDefault();
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => false;
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;
		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}

		// use namespace
		string IDatabaseProvider.GetProviderFactoryName(string providerName) => "Oracle.ManagedDataAccess.Client";
	}

	internal class SqlServerProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => ProviderName.SqlServer;

		string IDatabaseProvider.Description => "Microsoft SQL Server";

		private static readonly string[] _providers = new []
		{
			ProviderName.SqlServer2005,
			ProviderName.SqlServer2008,
			ProviderName.SqlServer2012,
			ProviderName.SqlServer2014,
			ProviderName.SqlServer2016,
			ProviderName.SqlServer2017,
			ProviderName.SqlServer2019,
			ProviderName.SqlServer2022,
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		private static readonly IReadOnlyCollection<Assembly> _additionalAssemblies = new[] { typeof(SqlHierarchyId).Assembly };

		void IDatabaseProvider.ClearAllPools() => SqlConnection.ClearAllPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => _additionalAssemblies;

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(modify_date) FROM sys.objects").FirstOrDefault();
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => false;
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;
		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}

		string IDatabaseProvider.GetProviderFactoryName(string providerName) => "Microsoft.Data.SqlClient";
	}

	internal class ClickHouseProvider : IDatabaseProvider
	{
		string IDatabaseProvider.Database => ProviderName.ClickHouse;

		string IDatabaseProvider.Description => "ClickHouse";

		private static readonly string[] _providers = new []
		{
			ProviderName.ClickHouseClient,
			ProviderName.ClickHouseMySql,
			ProviderName.ClickHouseOctonica,
		};

		IReadOnlyCollection<string> IDatabaseProvider.ProviderNames => _providers;

		// only for mysql provider:
		// - octonica provider doesn't implement connection pooling
		// - client provider use http connections pooling
		void IDatabaseProvider.ClearAllPools() => MySqlConnection.ClearAllPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(metadata_modification_time) FROM system.tables WHERE database = database()").FirstOrDefault();
		}

		bool IDatabaseProvider.IsProviderPathSupported(string providerName) => false;
		string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
		string? IDatabaseProvider.GetProviderDownloadUrl(string providerName) => null;
		string? IDatabaseProvider.TryGetDefaultPath(string providerName) => null;
		void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
		{
		}

		string? IDatabaseProvider.GetProviderFactoryName(string providerName)
		{
			return providerName switch
			{
				ProviderName.ClickHouseMySql => "MySqlConnector",
				ProviderName.ClickHouseClient => "ClickHouse.Client.ADO",
				ProviderName.ClickHouseOctonica => "Octonica.ClickHouseClient",
				_ => throw new LinqToDBLinqPadException($"Unknown CLickHouse provider '{providerName}'")
			};
		}
	}
}
