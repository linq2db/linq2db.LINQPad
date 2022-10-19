using LinqToDB.Data;

namespace LinqToDB.LINQPad;

/// <summary>
/// Base class for generated contexts and context for use directly.
/// </summary>
public class LINQPadDataConnection : DataConnection
{
	/// <summary>
	/// Constructor for inherited context.
	/// </summary>
	protected LINQPadDataConnection()
	{
		Init();
	}

	/// <summary>
	/// Constructor for inherited context.
	/// </summary>
	protected LINQPadDataConnection(string? providerName, string? providerPath, string connectionString)
		: base(ProviderHelper.GetProvider(providerName, providerPath).GetDataProvider(connectionString), connectionString)
	{
		Init();
	}

	/// <summary>
	/// Constructor for use from code directly.
	/// </summary>
	internal LINQPadDataConnection(Settings settings)
		: this(
			settings.Provider,
			settings.ProviderPath,
			settings.ConnectionInfo.DatabaseInfo.CustomCxString)
	{
		CommandTimeout = settings.CommandTimeout;
	}

	static void Init()
	{
		TurnTraceSwitchOn();
	}
}
