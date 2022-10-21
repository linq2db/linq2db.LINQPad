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
	/// Constructor for use from code directly.
	/// </summary>
	internal LINQPadDataConnection(Settings settings)
		: this(
			settings.Provider,
			settings.ProviderPath,
			settings.ConnectionString)
	{
		CommandTimeout = settings.CommandTimeout;
	}
}
