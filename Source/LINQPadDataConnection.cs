using LinqToDB.Data;

namespace LinqToDB.LINQPad;

/// <summary>
/// Base class for generated contexts and context for direct use.
/// </summary>
public class LINQPadDataConnection : DataConnection
{
	/// <summary>
	/// Constructor for inherited context.
	/// </summary>
	protected LINQPadDataConnection(string? providerName, string? providerPath, string? connectionString)
		: base(
			DatabaseProviders.GetDataProvider(providerName, connectionString, providerPath),
			connectionString ?? throw new LinqToDBLinqPadException("Connection string missing"))
	{
	}

	/// <summary>
	/// Constructor for use from driver code directly.
	/// </summary>
	internal LINQPadDataConnection(ConnectionSettings settings)
		: this(
			settings.Connection.Provider,
			settings.Connection.ProviderPath,
			settings.Connection.ConnectionString)
	{
		if (settings.Connection.CommandTimeout != null)
			CommandTimeout = settings.Connection.CommandTimeout.Value;
	}
}
