using LinqToDB.Data;
using System.Data.Common;
using LinqToDB.DataProvider.DB2iSeries;

#if NETFRAMEWORK
using IBM.Data.DB2;
#else
using IBM.Data.Db2;
#endif

namespace LinqToDB.LINQPad;

internal sealed class DB2Provider : DatabaseProviderBase
{
	private static readonly IReadOnlyList<ProviderInfo> _providers =
	[
		new (ProviderName.DB2LUW       , "DB2 for Linux, UNIX and Windows (LUW)"),
		// zOS provider not tested at all as we don't have access to database instance
		new (ProviderName.DB2zOS       , "DB2 for z/OS"                         ),
		new (DB2iSeriesProviderName.DB2, "DB2 for i (iSeries)"                  ),
	];

	public DB2Provider()
		: base(ProviderName.DB2, "IBM DB2 (LUW, z/OS or iSeries)", _providers)
	{
		DataConnection.AddProviderDetector(DB2iSeriesTools.ProviderDetector);
	}

	public override void ClearAllPools(string providerName)
	{
		DB2Connection.ReleaseObjectPool();
	}

	public override DateTime? GetLastSchemaUpdate(ConnectionSettings settings)
	{
		var sql = settings.Connection.Provider switch
		{
			ProviderName.DB2LUW        => "SELECT MAX(TIME) FROM (SELECT MAX(ALTER_TIME) AS TIME FROM SYSCAT.ROUTINES UNION SELECT MAX(ALTER_TIME) AS TIME FROM SYSCAT.TABLES)",
			ProviderName.DB2zOS        => "SELECT MAX(TIME) FROM (SELECT MAX(ALTEREDTS) AS TIME FROM SYSIBM.SYSROUTINES UNION SELECT MAX(ALTEREDTS) AS TIME FROM SYSIBM.SYSTABLES)",
			DB2iSeriesProviderName.DB2 => "SELECT MAX(TIME) FROM (SELECT MAX(LAST_ALTERED) AS TIME FROM QSYS2.SYSROUTINES UNION SELECT MAX(ROUTINE_CREATED) AS TIME FROM QSYS2.SYSROUTINES UNION SELECT MAX(LAST_ALTERED_TIMESTAMP) AS TIME FROM QSYS2.SYSTABLES)",
			_                          => throw new LinqToDBLinqPadException($"Unknown DB2 provider '{settings.Connection.Provider}'")
		};

		using var db = new LINQPadDataConnection(settings);
		return db.Query<DateTime?>(sql).FirstOrDefault();
	}

	public override DbProviderFactory GetProviderFactory(string providerName)
	{
		return DB2Factory.Instance;
	}
}
