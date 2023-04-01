using System.Data.Common;

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

	public abstract void              ClearAllPools      (string providerName        );
	public abstract DateTime?         GetLastSchemaUpdate(ConnectionSettings settings);
	public abstract DbProviderFactory GetProviderFactory (string providerName        );

	public virtual IEnumerable<(Type type, TypeRenderer renderer)> GetTypeRenderers() => Array.Empty<(Type, TypeRenderer)>();
}
