#if !LPX6
using IBM.Data.DB2;
#else
using IBM.Data.DB2.Core;
#endif

namespace LinqToDB.LINQPad;

internal sealed class InformixProvider : IDatabaseProvider
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new (ProviderName.InformixDB2, "Informix")
	};

	string                      IDatabaseProvider.Database                    => ProviderName.Informix;
	string                      IDatabaseProvider.Description                 => "IBM Informix";
	IReadOnlyList<ProviderInfo> IDatabaseProvider.Providers                   => _providers;
	bool                        IDatabaseProvider.SupportsSecondaryConnection => false;
	bool                        IDatabaseProvider.AutomaticProviderSelection  => false;

	ProviderInfo?                 IDatabaseProvider.GetProviderByConnectionString(string connectionString) => null;
	void                          IDatabaseProvider.ClearAllPools                (string  providerName   ) => DB2Connection.ReleaseObjectPool();
	IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences      (string  providerName   ) => Array.Empty<Assembly>();
	bool                          IDatabaseProvider.IsProviderPathSupported      (string  providerName   ) => false;
	string?                       IDatabaseProvider.GetProviderAssemblyName      (string  providerName   ) => null;
	string?                       IDatabaseProvider.GetProviderDownloadUrl       (string? providerName   ) => null;
	string?                       IDatabaseProvider.TryGetDefaultPath            (string  providerName   ) => null;
	string                        IDatabaseProvider.GetProviderFactoryName       (string  providerName   ) => "IBM.Data.DB2";
	// Informix provides only table creation date without time, which is useless
	DateTime?                     IDatabaseProvider.GetLastSchemaUpdate          (ConnectionSettings settings) => null;

	void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath) { }
}
