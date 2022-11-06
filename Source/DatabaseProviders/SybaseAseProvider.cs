using AdoNetCore.AseClient;
using LinqToDB.Data;

namespace LinqToDB.LINQPad;

internal sealed class SybaseAseProvider : IDatabaseProvider
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new(ProviderName.SybaseManaged, "SAP/Sybase ASE")
	};

	string                      IDatabaseProvider.Database                    => ProviderName.Sybase;
	string                      IDatabaseProvider.Description                 => "SAP/Sybase ASE";
	IReadOnlyList<ProviderInfo> IDatabaseProvider.Providers                   => _providers;
	bool                        IDatabaseProvider.SupportsSecondaryConnection => false;
	bool                        IDatabaseProvider.AutomaticProviderSelection  => false;

	ProviderInfo?                 IDatabaseProvider.GetProviderByConnectionString(string  connectionString) => null;
	void                          IDatabaseProvider.ClearAllPools                (string  providerName    ) => AseConnection.ClearPools();
	IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences      (string  providerName    ) => Array.Empty<Assembly>();
	bool                          IDatabaseProvider.IsProviderPathSupported      (string  providerName    ) => false;
	string?                       IDatabaseProvider.GetProviderAssemblyName      (string  providerName    ) => null;
	string?                       IDatabaseProvider.GetProviderDownloadUrl       (string? providerName    ) => null;
	string?                       IDatabaseProvider.TryGetDefaultPath            (string  providerName    ) => null;
	// there is no provider-defined default factory name, use assembly name
	string                        IDatabaseProvider.GetProviderFactoryName       (string  providerName    ) => "AdoNetCore.AseClient";

	void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath) { }

	DateTime? IDatabaseProvider.GetLastSchemaUpdate(ConnectionSettings settings)
	{
		using var db = new LINQPadDataConnection(settings);
		return db.Query<DateTime?>("SELECT MAX(crdate) FROM sysobjects").FirstOrDefault();
	}
}
