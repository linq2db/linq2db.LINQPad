using System.Windows;
using LINQPad.Extensibility.DataContext;
using LinqToDB.Data;

namespace LinqToDB.LINQPad;

/// <summary>
/// Implements LINQPad driver for static (pre-compiled) model.
/// </summary>
public sealed class LinqToDBStaticDriver : StaticDataContextDriver
{
	/// <summary>
	/// <inheritdoc cref="DataContextDriver.Name"/>
	/// </summary>
	public override string Name   => "LINQ to DB (DataConnection)";
	/// <summary>
	/// <inheritdoc cref="DataContextDriver.Author"/>
	/// </summary>
	public override string Author => DriverHelper.Author;

	static LinqToDBStaticDriver()
	{
		DriverHelper.Init();
	}

	/// <summary>
	/// <inheritdoc cref="DataContextDriver.GetConnectionDescription(IConnectionInfo)"/>
	/// </summary>
	public override string GetConnectionDescription(IConnectionInfo cxInfo) => DriverHelper.GetConnectionDescription(cxInfo);

	/// <summary>
	/// <inheritdoc cref="DataContextDriver.ShowConnectionDialog(IConnectionInfo, bool)"/>
	/// </summary>
	[Obsolete("base method obsoleted")]
	public override bool ShowConnectionDialog(IConnectionInfo cxInfo, bool isNewConnection) => DriverHelper.ShowConnectionDialog(cxInfo, isNewConnection, false);

	/// <summary>
	/// <inheritdoc cref="StaticDataContextDriver.GetSchema(IConnectionInfo, Type)"/>
	/// </summary>
	public override List<ExplorerItem> GetSchema(IConnectionInfo cxInfo, Type customType)
	{
		try
		{
			return SchemaGenerator.GetSchema(customType);
		}
		catch (Exception ex)
		{
			MessageBox.Show($"{ex}\n{ex.StackTrace}", "Schema Build Error", MessageBoxButton.OK, MessageBoxImage.Error);
			throw;
		}
	}

	/// <summary>
	/// <inheritdoc cref="DataContextDriver.GetContextConstructorParameters(IConnectionInfo)"/>
	/// </summary>
	public override ParameterDescriptor[] GetContextConstructorParameters(IConnectionInfo cxInfo)
	{
		var configuration = cxInfo.DriverData.Element(CX.CustomConfiguration)?.Value;

		if (configuration != null)
			return new[] { new ParameterDescriptor("configuration", typeof(string).FullName) };

		return base.GetContextConstructorParameters(cxInfo);
	}

	/// <summary>
	/// <inheritdoc cref="DataContextDriver.GetContextConstructorArguments(IConnectionInfo)"/>
	/// </summary>
	public override object[] GetContextConstructorArguments(IConnectionInfo cxInfo)
	{
		TryLoadAppSettingsJson(cxInfo);

		var configuration = cxInfo.DriverData.Element(CX.CustomConfiguration)?.Value;

		if (configuration != null)
			return new object[] { configuration };

		return base.GetContextConstructorArguments(cxInfo);
	}

	/// <summary>
	/// <inheritdoc cref="DataContextDriver.ClearConnectionPools(IConnectionInfo)"/>
	/// </summary>
	public override void ClearConnectionPools(IConnectionInfo cxInfo) => DriverHelper.ClearConnectionPools(cxInfo);

	/// <summary>
	/// <inheritdoc cref="DataContextDriver.InitializeContext(IConnectionInfo, object, QueryExecutionManager)"/>
	/// </summary>
	public override void InitializeContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager)
	{
		var optimizeJoins      = cxInfo.DriverData.Element(CX.OptimizeJoins)      == null || cxInfo.DriverData.Element(CX.OptimizeJoins)     ?.Value.ToLower() == "true";

		Common.Configuration.Linq.OptimizeJoins      = optimizeJoins;

		dynamic ctx = context;

		if (Extensions.HasProperty(ctx, nameof(DataConnection.OnTraceConnection)))
		{
			ctx.OnTraceConnection = DriverHelper.GetOnTraceConnection(executionManager);
			DataConnection.TurnTraceSwitchOn();
		}
	}

	/// <summary>
	/// <inheritdoc cref="DataContextDriver.TearDownContext(IConnectionInfo, object, QueryExecutionManager, object[])"/>
	/// </summary>
	public override void TearDownContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager,
		object[] constructorArguments)
	{
		dynamic ctx = context;
		ctx.Dispose();
	}

	private void TryLoadAppSettingsJson(IConnectionInfo cxInfo)
	{
#if LPX6
		if (cxInfo.AppConfigPath?.EndsWith(".json", StringComparison.OrdinalIgnoreCase) == true)
			DataConnection.DefaultSettings = AppJsonConfig.Load(cxInfo.AppConfigPath!);
#endif
	}
}
