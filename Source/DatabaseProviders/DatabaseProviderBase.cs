using System.Data.Common;
using LinqToDB.Data;
using LinqToDB.DataProvider;

namespace LinqToDB.LINQPad;

internal abstract class DatabaseProviderBase : IDatabaseProvider
{
	protected DatabaseProviderBase(string database, string description, IReadOnlyList<ProviderInfo> providers)
	{
		Database    = database;
		Description = description;
		Providers   = providers;
	}

	public string                      Database                    { get; }
	public string                      Description                 { get; }
	public IReadOnlyList<ProviderInfo> Providers                   { get; }

	public virtual bool SupportsSecondaryConnection { get; }
	public virtual bool AutomaticProviderSelection  { get; }

	public virtual IReadOnlyCollection<Assembly> GetAdditionalReferences      (string providerName                     ) => Array.Empty<Assembly>();
	public virtual string?                       GetProviderAssemblyName      (string providerName                     ) => null;
	public virtual ProviderInfo?                 GetProviderByConnectionString(string connectionString                 ) => null;
	public virtual string?                       GetProviderDownloadUrl       (string? providerName                    ) => null;
	public virtual bool                          IsProviderPathSupported      (string providerName                     ) => false;
	public virtual void                          RegisterProviderFactory      (string providerName, string providerPath) { }
	public virtual string?                       TryGetDefaultPath            (string providerName                     ) => null;
#if NETFRAMEWORK
	public virtual void                          Unload                       (                                        ) { }
#endif

	public abstract void              ClearAllPools      (string providerName        );
	public abstract DateTime?         GetLastSchemaUpdate(ConnectionSettings settings);
	public abstract DbProviderFactory GetProviderFactory (string providerName        );

	public virtual IDataProvider GetDataProvider(string providerName, string connectionString)
	{
		return DataConnection.GetDataProvider(providerName, connectionString)
			?? throw new LinqToDBLinqPadException($"Can not activate provider '{providerName}'");
	}
}
