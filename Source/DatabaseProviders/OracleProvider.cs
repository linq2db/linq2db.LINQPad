using LinqToDB.Data;
using Oracle.ManagedDataAccess.Client;

namespace LinqToDB.LINQPad;

internal sealed class OracleProvider : IDatabaseProvider
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new (ProviderName.Oracle11Managed, "Oracle 11g Dialect"),
		new (ProviderName.OracleManaged  , "Oracle 12c Dialect"),
	};

	string                      IDatabaseProvider.Database                    => ProviderName.Oracle;
	string                      IDatabaseProvider.Description                 => "Oracle";
	IReadOnlyList<ProviderInfo> IDatabaseProvider.Providers                   => _providers;
	bool                        IDatabaseProvider.SupportsSecondaryConnection => false;
	bool                        IDatabaseProvider.AutomaticProviderSelection  => false;

	ProviderInfo?                 IDatabaseProvider.GetProviderByConnectionString(string  connectionString) => null;
	IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences      (string  providerName    ) => Array.Empty<Assembly>();
	void                          IDatabaseProvider.ClearAllPools                (string  providerName    ) => OracleConnection.ClearAllPools();
	bool                          IDatabaseProvider.IsProviderPathSupported      (string  providerName    ) => false;
	string?                       IDatabaseProvider.GetProviderAssemblyName      (string  providerName    ) => null;
	string?                       IDatabaseProvider.GetProviderDownloadUrl       (string? providerName    ) => null;
	string?                       IDatabaseProvider.TryGetDefaultPath            (string  providerName    ) => null;
	// use namespace
	string                        IDatabaseProvider.GetProviderFactoryName       (string  providerName    ) => "Oracle.ManagedDataAccess.Client";

	void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath) { }

	DateTime? IDatabaseProvider.GetLastSchemaUpdate(ConnectionSettings settings)
	{
		using var db = new LINQPadDataConnection(settings);
		return db.Query<DateTime?>("SELECT MAX(LAST_DDL_TIME) FROM USER_OBJECTS WHERE OBJECT_TYPE IN ('TABLE', 'VIEW', 'INDEX', 'FUNCTION', 'PACKAGE', 'PACKAGE BODY', 'PROCEDURE', 'MATERIALIZED VIEW')").FirstOrDefault();
	}
}
