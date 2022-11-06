using System.Data.SQLite;

namespace LinqToDB.LINQPad;

internal sealed class SQLiteProvider : IDatabaseProvider
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new(ProviderName.SQLiteClassic, "SQLite")
	};

	string                      IDatabaseProvider.Database                    => ProviderName.SQLite;
	string                      IDatabaseProvider.Description                 => "SQLite";
	IReadOnlyList<ProviderInfo> IDatabaseProvider.Providers                   => _providers;
	bool                        IDatabaseProvider.SupportsSecondaryConnection => false;
	bool                        IDatabaseProvider.AutomaticProviderSelection  => false;

	ProviderInfo?                 IDatabaseProvider.GetProviderByConnectionString(string connectionString    ) => null;
	void                          IDatabaseProvider.ClearAllPools                (string providerName        ) => SQLiteConnection.ClearAllPools();
	IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences      (string providerName        ) => Array.Empty<Assembly>();
	// no information in schema
	DateTime?                     IDatabaseProvider.GetLastSchemaUpdate          (ConnectionSettings settings) => null;
	bool                          IDatabaseProvider.IsProviderPathSupported      (string  providerName       ) => false;
	string?                       IDatabaseProvider.GetProviderAssemblyName      (string  providerName       ) => null;
	string?                       IDatabaseProvider.GetProviderDownloadUrl       (string? providerName       ) => null;
	string?                       IDatabaseProvider.TryGetDefaultPath            (string  providerName       ) => null;
	// there is no provider-defined default factory name, use assembly/namespace name
	string                        IDatabaseProvider.GetProviderFactoryName       (string  providerName       ) => "System.Data.SQLite";

	void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath) { }
}
