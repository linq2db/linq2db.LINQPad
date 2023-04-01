using System.Data.Common;
using LinqToDB.Data;
using LinqToDB.DataProvider;

namespace LinqToDB.LINQPad;

internal static class DatabaseProviders
{
	private static readonly IReadOnlyDictionary<Type, TypeRenderer> _typeRenderers;

	public static readonly IReadOnlyDictionary<string, IDatabaseProvider> Providers;
	public static readonly IReadOnlyDictionary<string, IDatabaseProvider> ProvidersByProviderName;

	static DatabaseProviders()
	{
		var typeRenderers       = new Dictionary<Type, TypeRenderer>();
		var providers           = new Dictionary<string, IDatabaseProvider>();
		var providersByName     = new Dictionary<string, IDatabaseProvider>();
		Providers               = providers;
		ProvidersByProviderName = providersByName;
		_typeRenderers          = typeRenderers;

		ValueFormatter.RegisterSharedRenderers(typeRenderers);

		Register(providers, providersByName, typeRenderers, new AccessProvider    ());
		Register(providers, providersByName, typeRenderers, new FirebirdProvider  ());
		Register(providers, providersByName, typeRenderers, new MySqlProvider     ());
		Register(providers, providersByName, typeRenderers, new PostgreSQLProvider());
		Register(providers, providersByName, typeRenderers, new SybaseAseProvider ());
		Register(providers, providersByName, typeRenderers, new SQLiteProvider    ());
		Register(providers, providersByName, typeRenderers, new SqlCeProvider     ());
		Register(providers, providersByName, typeRenderers, new DB2Provider       ());
		Register(providers, providersByName, typeRenderers, new InformixProvider  ());
		Register(providers, providersByName, typeRenderers, new SapHanaProvider   ());
		Register(providers, providersByName, typeRenderers, new OracleProvider    ());
		Register(providers, providersByName, typeRenderers, new SqlServerProvider ());
		Register(providers, providersByName, typeRenderers, new ClickHouseProvider());

		static void Register(
			Dictionary<string, IDatabaseProvider> providers,
			Dictionary<string, IDatabaseProvider> providersByName,
			Dictionary<Type  , TypeRenderer     > typeRenderers,
			IDatabaseProvider provider)
		{
			providers.Add(provider.Database, provider);

			foreach (var info in provider.Providers)
				providersByName.Add(info.Name, provider);

			foreach (var (type, renderer) in provider.GetTypeRenderers())
				typeRenderers.Add(type, renderer);
		}
	}

	public static DbConnection CreateConnection (ConnectionSettings settings) => GetDataProvider(settings).CreateConnection(settings.Connection.ConnectionString!);

	public static DbProviderFactory GetProviderFactory(ConnectionSettings settings) => GetProviderByName(settings.Connection.Provider!).GetProviderFactory(settings.Connection.Provider!);

	public static IDataProvider GetDataProvider(ConnectionSettings settings) => GetDataProvider(settings.Connection.Provider, settings.Connection.ConnectionString, settings.Connection.ProviderPath);

	public static IDataProvider GetDataProvider(string? providerName, string? connectionString, string? providerPath)
	{
		if (string.IsNullOrWhiteSpace(providerName))
			throw new LinqToDBLinqPadException("Can not activate provider. Provider is not selected.");

		if (string.IsNullOrWhiteSpace(connectionString))
			throw new LinqToDBLinqPadException($"Can not activate provider '{providerName}'. Connection string not specified.");

		if (providerPath != null)
			GetProviderByName(providerName!).RegisterProviderFactory(providerName!, providerPath);

		return DataConnection.GetDataProvider(providerName!, connectionString!)
			?? throw new LinqToDBLinqPadException($"Can not activate provider '{providerName}'");
	}

	private static IDatabaseProvider GetProviderByName(string providerName)
	{
		if (ProvidersByProviderName.TryGetValue(providerName, out var provider))
			return provider;

		throw new LinqToDBLinqPadException($"Cannot find database provider '{providerName}'");
	}

	/// <summary>
	/// Gets database provider abstraction by database name.
	/// </summary>
	/// <param name="database">Database name (identifier of provider abstraction).</param>
	public static IDatabaseProvider GetProvider(string? database)
	{
		if (database != null && Providers.TryGetValue(database, out var provider))
			return provider;

		throw new LinqToDBLinqPadException($"Cannot find provider for database '{database}'");
	}

	public static void RenderValue(ref object? value)
	{
		if (value != null && _typeRenderers.TryGetValue(value.GetType(), out var renderer))
			renderer(ref value);

		value = ValueFormatter.Format(value);
	}
}
