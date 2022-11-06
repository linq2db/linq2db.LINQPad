using MySqlConnector;
using LinqToDB.Data;

namespace LinqToDB.LINQPad;

internal sealed class MySqlProvider : IDatabaseProvider
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new (ProviderName.MySqlConnector, "MySql/MariaDB"),
	};

	string                      IDatabaseProvider.Database                    => ProviderName.MySql;
	string                      IDatabaseProvider.Description                 => "MySql/MariaDB";
	IReadOnlyList<ProviderInfo> IDatabaseProvider.Providers                   => _providers;
	bool                        IDatabaseProvider.SupportsSecondaryConnection => false;
	bool                        IDatabaseProvider.AutomaticProviderSelection  => false;

	ProviderInfo?                 IDatabaseProvider.GetProviderByConnectionString(string  connectionString) => null;
	bool                          IDatabaseProvider.IsProviderPathSupported      (string  providerName    ) => false;
	string?                       IDatabaseProvider.GetProviderAssemblyName      (string  providerName    ) => null;
	string?                       IDatabaseProvider.GetProviderDownloadUrl       (string? providerName    ) => null;
	string?                       IDatabaseProvider.TryGetDefaultPath            (string  providerName    ) => null;
	// https://github.com/mysql-net/MySqlConnector/blob/master/docs/content/overview/dbproviderfactories.md
	string                        IDatabaseProvider.GetProviderFactoryName       (string  providerName    ) => "MySqlConnector";
	void                          IDatabaseProvider.ClearAllPools                (string  providerName    ) => MySqlConnection.ClearAllPools();
	IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences      (string  providerName    ) => Array.Empty<Assembly>();

	void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath) { }

	DateTime? IDatabaseProvider.GetLastSchemaUpdate(ConnectionSettings settings)
	{
		using var db = new LINQPadDataConnection(settings);
		return db.Query<DateTime?>("SELECT MAX(u.time) FROM (SELECT MAX(UPDATE_TIME) AS time FROM information_schema.TABLES UNION SELECT MAX(CREATE_TIME) FROM information_schema.TABLES UNION SELECT MAX(LAST_ALTERED) FROM information_schema.ROUTINES) as u").FirstOrDefault();
	}
}
