using FirebirdSql.Data.FirebirdClient;

namespace LinqToDB.LINQPad;

internal sealed class FirebirdProvider : IDatabaseProvider
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new (ProviderName.Firebird, "Firebird"),
	};

	string                      IDatabaseProvider.Database                    => ProviderName.Firebird;
	string                      IDatabaseProvider.Description                 => "Firebird";
	IReadOnlyList<ProviderInfo> IDatabaseProvider.Providers                   => _providers;
	bool                        IDatabaseProvider.SupportsSecondaryConnection => false;
	bool                        IDatabaseProvider.AutomaticProviderSelection  => false;

	ProviderInfo?                 IDatabaseProvider.GetProviderByConnectionString(string connectionString    ) => null;
	void                          IDatabaseProvider.ClearAllPools                (string providerName        ) => FbConnection.ClearAllPools();
	IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences      (string providerName        ) => Array.Empty<Assembly>();
	// no information in schema
	DateTime?                     IDatabaseProvider.GetLastSchemaUpdate          (ConnectionSettings settings) => null;
	bool                          IDatabaseProvider.IsProviderPathSupported      (string  providerName       ) => false;
	string?                       IDatabaseProvider.GetProviderAssemblyName      (string  providerName       ) => null;
	string?                       IDatabaseProvider.GetProviderDownloadUrl       (string? providerName       ) => null;
	string?                       IDatabaseProvider.TryGetDefaultPath            (string  providerName       ) => null;
	// https://github.com/FirebirdSQL/NETProvider/blob/master/src/EntityFramework.Firebird/FbProviderServices.cs#L39
	string                        IDatabaseProvider.GetProviderFactoryName       (string  providerName       ) => "FirebirdSql.Data.FirebirdClient";

	void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath) { }
}
