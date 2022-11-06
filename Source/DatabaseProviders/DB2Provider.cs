using LinqToDB.DataProvider.DB2iSeries;
using LinqToDB.Data;
#if !LPX6
using IBM.Data.DB2;
#else
using IBM.Data.DB2.Core;
#endif

namespace LinqToDB.LINQPad;

internal sealed class DB2Provider : IDatabaseProvider
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new (ProviderName.DB2LUW       , "DB2 for Linux, UNIX and Windows (LUW)"),
		// zOS provider not tested at all as we don't have access to database instance
		new (ProviderName.DB2zOS       , "DB2 for z/OS"                         ),
		new (DB2iSeriesProviderName.DB2, "DB2 for i (iSeries)"                  ),
	};

	string                      IDatabaseProvider.Database                    => ProviderName.DB2;
	string                      IDatabaseProvider.Description                 => "IBM DB2 (LUW, z/OS or iSeries)";
	IReadOnlyList<ProviderInfo> IDatabaseProvider.Providers                   => _providers;
	bool                        IDatabaseProvider.SupportsSecondaryConnection => false;
	bool                        IDatabaseProvider.AutomaticProviderSelection  => false;

	ProviderInfo?                 IDatabaseProvider.GetProviderByConnectionString(string  connectionString) => null;
	void                          IDatabaseProvider.ClearAllPools                (string  providerName    ) => DB2Connection.ReleaseObjectPool();
	IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences      (string  providerName    ) => Array.Empty<Assembly>();
	bool                          IDatabaseProvider.IsProviderPathSupported      (string  providerName    ) => false;
	string?                       IDatabaseProvider.GetProviderAssemblyName      (string  providerName    ) => null;
	string?                       IDatabaseProvider.GetProviderDownloadUrl       (string? providerName    ) => null;
	string?                       IDatabaseProvider.TryGetDefaultPath            (string  providerName    ) => null;
	string                        IDatabaseProvider.GetProviderFactoryName       (string  providerName    ) => "IBM.Data.DB2";

	void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath) { }

	DateTime? IDatabaseProvider.GetLastSchemaUpdate(ConnectionSettings settings)
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
}
