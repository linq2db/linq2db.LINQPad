using LinqToDB.Data;
using MySqlConnector;

namespace LinqToDB.LINQPad;

internal sealed class ClickHouseProvider : IDatabaseProvider
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new (ProviderName.ClickHouseClient  , "HTTP(S) Interface"     ),
		new (ProviderName.ClickHouseMySql   , "MySQL Interface"       ),
		new (ProviderName.ClickHouseOctonica, "Binary (TCP) Interface"),
	};

	string                      IDatabaseProvider.Database                    => ProviderName.ClickHouse;
	string                      IDatabaseProvider.Description                 => "ClickHouse";
	IReadOnlyList<ProviderInfo> IDatabaseProvider.Providers                   => _providers;
	bool                        IDatabaseProvider.SupportsSecondaryConnection => false;
	bool                        IDatabaseProvider.AutomaticProviderSelection  => false;

	ProviderInfo?                 IDatabaseProvider.GetProviderByConnectionString(string  connectionString) => null;
	IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences      (string  providerName    ) => Array.Empty<Assembly>();
	bool                          IDatabaseProvider.IsProviderPathSupported      (string  providerName    ) => false;
	string?                       IDatabaseProvider.GetProviderAssemblyName      (string  providerName    ) => null;
	string?                       IDatabaseProvider.GetProviderDownloadUrl       (string? providerName    ) => null;
	string?                       IDatabaseProvider.TryGetDefaultPath            (string  providerName    ) => null;

	void                          IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath) { }

	void IDatabaseProvider.ClearAllPools(string providerName)
	{
		// octonica provider doesn't implement connection pooling
		// client provider use http connections pooling
		if (providerName == ProviderName.ClickHouseMySql)
			MySqlConnection.ClearAllPools();
	}

	DateTime? IDatabaseProvider.GetLastSchemaUpdate(ConnectionSettings settings)
	{
		using var db = new LINQPadDataConnection(settings);
		return db.Query<DateTime?>("SELECT MAX(metadata_modification_time) FROM system.tables WHERE database = database()").FirstOrDefault();
	}

	string? IDatabaseProvider.GetProviderFactoryName(string providerName)
	{
		return providerName switch
		{
			ProviderName.ClickHouseMySql    => "MySqlConnector",
			ProviderName.ClickHouseClient   => "ClickHouse.Client.ADO",
			ProviderName.ClickHouseOctonica => "Octonica.ClickHouseClient",
			_                               => throw new LinqToDBLinqPadException($"Unknown ClickHouse provider '{providerName}'")
		};
	}
}
