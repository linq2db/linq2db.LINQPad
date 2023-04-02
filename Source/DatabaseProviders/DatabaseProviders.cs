using System.Data.Common;
using LinqToDB.Data;
using LinqToDB.DataProvider;

namespace LinqToDB.LINQPad;

internal static class DatabaseProviders
{
	public static readonly IReadOnlyDictionary<string, IDatabaseProvider> Providers;
	public static readonly IReadOnlyDictionary<string, IDatabaseProvider> ProvidersByProviderName;

	static DatabaseProviders()
	{
		var providers           = new Dictionary<string, IDatabaseProvider  >();
		var providersByName     = new Dictionary<string, IDatabaseProvider  >();
		Providers               = providers;
		ProvidersByProviderName = providersByName;

		Register(providers, providersByName, new AccessProvider    ());
		Register(providers, providersByName, new FirebirdProvider  ());
		Register(providers, providersByName, new MySqlProvider     ());
		Register(providers, providersByName, new PostgreSQLProvider());
		Register(providers, providersByName, new SybaseAseProvider ());
		Register(providers, providersByName, new SQLiteProvider    ());
		Register(providers, providersByName, new SqlCeProvider     ());
		Register(providers, providersByName, new DB2Provider       ());
		Register(providers, providersByName, new InformixProvider  ());
		Register(providers, providersByName, new SapHanaProvider   ());
		Register(providers, providersByName, new OracleProvider    ());
		Register(providers, providersByName, new SqlServerProvider ());
		Register(providers, providersByName, new ClickHouseProvider());

		static void Register(
			Dictionary<string, IDatabaseProvider> providers,
			Dictionary<string, IDatabaseProvider> providersByName,
			IDatabaseProvider                     provider)
		{
			providers.Add(provider.Database, provider);

			foreach (var info in provider.Providers)
				providersByName.Add(info.Name, provider);
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
}
