using Npgsql;

namespace LinqToDB.LINQPad;

internal sealed class PostgreSQLProvider : IDatabaseProvider
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new(ProviderName.PostgreSQL92, "PostgreSQL 9.2 Dialect"),
		new(ProviderName.PostgreSQL93, "PostgreSQL 9.3 Dialect"),
		new(ProviderName.PostgreSQL95, "PostgreSQL 9.5 Dialect"),
		new(ProviderName.PostgreSQL15, "PostgreSQL 15 Dialect" ),
	};

	string                      IDatabaseProvider.Database                    => ProviderName.PostgreSQL;
	string                      IDatabaseProvider.Description                 => "PostgreSQL";
	IReadOnlyList<ProviderInfo> IDatabaseProvider.Providers                   => _providers;
	bool                        IDatabaseProvider.SupportsSecondaryConnection => false;
	bool                        IDatabaseProvider.AutomaticProviderSelection  => false;

	ProviderInfo?                 IDatabaseProvider.GetProviderByConnectionString(string connectionString    ) => null;
	void                          IDatabaseProvider.ClearAllPools                (string providerName        ) => NpgsqlConnection.ClearAllPools();
	IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences      (string providerName        ) => Array.Empty<Assembly>();
	// no information in schema
	DateTime?                     IDatabaseProvider.GetLastSchemaUpdate          (ConnectionSettings settings) => null;
	bool                          IDatabaseProvider.IsProviderPathSupported      (string  providerName       ) => false;
	string?                       IDatabaseProvider.GetProviderAssemblyName      (string  providerName       ) => null;
	string?                       IDatabaseProvider.GetProviderDownloadUrl       (string? providerName       ) => null;
	string?                       IDatabaseProvider.TryGetDefaultPath            (string  providerName       ) => null;
	string                        IDatabaseProvider.GetProviderFactoryName       (string  providerName       ) => "Npgsql";

	void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath) { }
}
