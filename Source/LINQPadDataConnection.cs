using CodeJam.Strings;
using CodeJam.Xml;
using LINQPad.Extensibility.DataContext;
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
	internal LINQPadDataConnection(IConnectionInfo cxInfo)
		: this(
			(string?)cxInfo.DriverData.Element(CX.ProviderName),
			(string?)cxInfo.DriverData.Element(CX.ProviderPath),
			cxInfo.DatabaseInfo.CustomCxString)
	{
		CommandTimeout = cxInfo.DriverData.ElementValueOrDefault(CX.CommandTimeout, str => str.ToInt32() ?? 0, 0);
	}

	static void Init()
	{
		TurnTraceSwitchOn();
	}
}
