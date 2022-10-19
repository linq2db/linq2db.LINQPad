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
#if !LPX6
using IBM.Data.DB2;
#else
using IBM.Data.DB2.Core;
#endif

namespace LinqToDB.LINQPad
{
	internal static class DatabaseProviders
	{
		private static readonly IDictionary<string, IDatabaseProvider> _providers = new Dictionary<string, IDatabaseProvider>()
			{
				{ ProviderName.Access       , new AccessProvider    () },
				{ ProviderName.Firebird     , new FirebirdProvider  () },
				{ ProviderName.MySql        , new MySqlProvider     () },
				{ ProviderName.PostgreSQL   , new PostgreSQLProvider() },
				{ ProviderName.Sybase       , new SybaseAseProvider () },
				{ ProviderName.SQLite       , new SQLiteProvider    () },
				{ ProviderName.SqlCe        , new SqlCeProvider     () },
				{ ProviderName.DB2LUW       , new DB2LUWProvider    () },
				{ ProviderName.DB2zOS       , new DB2zOSProvider    () },
				{ DB2iSeriesProviderName.DB2, new DB2iSeriesProvider() },
				{ ProviderName.Informix     , new InformixProvider  () },
				{ ProviderName.SapHana      , new SapHanaProvider   () },
				{ ProviderName.Oracle       , new OracleProvider    () },
				{ ProviderName.SqlServer    , new SqlServerProvider () },
				{ ProviderName.ClickHouse   , new ClickHouseProvider() },
			};

		public static IDatabaseProvider? GetProvider(this Settings settings)
		{
			var database = settings.Database;

			if (database != null && _providers.TryGetValue(database, out var provider))
				return provider;

			return null;
		}
	}

	internal interface IDatabaseProvider
	{
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
	}

	internal class AccessProvider : IDatabaseProvider
	{
		void IDatabaseProvider.ClearAllPools()
		{
			OleDbConnection.ReleaseObjectPool();
			OdbcConnection.ReleaseObjectPool();
		}

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);

			// only OLE DB schema has required information
			if (db.Connection is OleDbConnection)
			{
				var dt1 = db.Connection.GetSchema("Tables"    ).Rows.Cast<DataRow>().Max(r => (DateTime)r["DATE_MODIFIED"]);
				var dt2 = db.Connection.GetSchema("Procedures").Rows.Cast<DataRow>().Max(r => (DateTime)r["DATE_MODIFIED"]);
				return dt1 > dt2 ? dt1 : dt2;
			}

			return null;
		}
	}

	internal class FirebirdProvider : IDatabaseProvider
	{
		void IDatabaseProvider.ClearAllPools() => FbConnection.ClearAllPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			// no information in schema
			return null;
		}
	}

	internal class MySqlProvider : IDatabaseProvider
	{
		void IDatabaseProvider.ClearAllPools() => MySqlConnection.ClearAllPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(u.time) FROM (SELECT MAX(UPDATE_TIME) AS time FROM information_schema.TABLES UNION SELECT MAX(CREATE_TIME) FROM information_schema.TABLES UNION SELECT MAX(LAST_ALTERED) FROM information_schema.ROUTINES) as u").FirstOrDefault();
		}
	}

	internal class PostgreSQLProvider : IDatabaseProvider
	{
		void IDatabaseProvider.ClearAllPools() => NpgsqlConnection.ClearAllPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			// no information in schema
			return null;
		}
	}

	internal class SybaseAseProvider : IDatabaseProvider
	{
		void IDatabaseProvider.ClearAllPools() => AseConnection.ClearPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(crdate) FROM sysobjects").FirstOrDefault();
		}
	}

	internal class SQLiteProvider : IDatabaseProvider
	{
		void IDatabaseProvider.ClearAllPools() => SQLiteConnection.ClearAllPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			// no information in schema
			return null;
		}
	}

	internal class SqlCeProvider : IDatabaseProvider
	{
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
	}

	internal class DB2LUWProvider : IDatabaseProvider
	{
		void IDatabaseProvider.ClearAllPools() => DB2Connection.ReleaseObjectPool();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(TIME) FROM (SELECT MAX(ALTER_TIME) AS TIME FROM SYSCAT.ROUTINES UNION SELECT MAX(ALTER_TIME) AS TIME FROM SYSCAT.TABLES)").FirstOrDefault();
		}
	}

	// z/OS provider not tested at all as we don't have access to database instance
	internal class DB2zOSProvider : IDatabaseProvider
	{
		void IDatabaseProvider.ClearAllPools() => DB2Connection.ReleaseObjectPool();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(TIME) FROM (SELECT MAX(ALTEREDTS) AS TIME FROM SYSIBM.SYSROUTINES UNION SELECT MAX(ALTEREDTS) AS TIME FROM SYSIBM.SYSTABLES)").FirstOrDefault();
		}
	}

	internal class DB2iSeriesProvider : IDatabaseProvider
	{
		void IDatabaseProvider.ClearAllPools() => DB2Connection.ReleaseObjectPool();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(TIME) FROM (SELECT MAX(LAST_ALTERED) AS TIME FROM QSYS2.SYSROUTINES UNION SELECT MAX(ROUTINE_CREATED) AS TIME FROM QSYS2.SYSROUTINES UNION SELECT MAX(LAST_ALTERED_TIMESTAMP) AS TIME FROM QSYS2.SYSTABLES)").FirstOrDefault();
		}
	}

	internal class InformixProvider : IDatabaseProvider
	{
		void IDatabaseProvider.ClearAllPools() => DB2Connection.ReleaseObjectPool();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			// Informix provides only table creation date without time, which is useless
			return null;
		}
	}

	internal class SapHanaProvider : IDatabaseProvider
	{
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
	}

	internal class OracleProvider : IDatabaseProvider
	{
		void IDatabaseProvider.ClearAllPools() => OracleConnection.ClearAllPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => Array.Empty<Assembly>();

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(LAST_DDL_TIME) FROM USER_OBJECTS WHERE OBJECT_TYPE IN ('TABLE', 'VIEW', 'INDEX', 'FUNCTION', 'PACKAGE', 'PACKAGE BODY', 'PROCEDURE', 'MATERIALIZED VIEW')").FirstOrDefault();
		}
	}

	internal class SqlServerProvider : IDatabaseProvider
	{
		private static readonly IReadOnlyCollection<Assembly> _additionalAssemblies = new[] { typeof(SqlHierarchyId).Assembly };

		void IDatabaseProvider.ClearAllPools() => SqlConnection.ClearAllPools();

		IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences() => _additionalAssemblies;

		DateTime? IDatabaseProvider.GetLastSchemaUpdate(Settings settings)
		{
			using var db = new LINQPadDataConnection(settings);
			return db.Query<DateTime?>("SELECT MAX(modify_date) FROM sys.objects").FirstOrDefault();
		}
	}

	internal class ClickHouseProvider : IDatabaseProvider
	{
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
	}
}
