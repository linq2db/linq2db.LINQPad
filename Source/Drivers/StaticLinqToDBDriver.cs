using System.Windows;
using LINQPad.Extensibility.DataContext;
using LinqToDB.Data;
using LinqToDB.Mapping;

namespace LinqToDB.LINQPad;

/// <summary>
/// Implements LINQPad driver for static (pre-compiled) model.
/// </summary>
internal sealed class StaticLinqToDBDriver : StaticDataContextDriver
{
	private MappingSchema? _mappingSchema;
	private bool           _useCustomFormatter;

	public override string Name   => DriverHelper.Name;
	public override string Author => DriverHelper.Author;

	static StaticLinqToDBDriver() => DriverHelper.Init();

	public override string GetConnectionDescription(IConnectionInfo cxInfo) => DriverHelper.GetConnectionDescription(new(cxInfo));

	public override bool ShowConnectionDialog(IConnectionInfo cxInfo, ConnectionDialogOptions dialogOptions) => DriverHelper.ShowConnectionDialog(new(cxInfo), dialogOptions, false);

	public override List<ExplorerItem> GetSchema(IConnectionInfo cxInfo, Type customType)
	{
		try
		{
			return StaticSchemaGenerator.GetSchema(customType);
		}
		catch (Exception ex)
		{
			MessageBox.Show($"{ex}\n{ex.StackTrace}", "Schema Build Error", MessageBoxButton.OK, MessageBoxImage.Error);
			throw;
		}
	}

	private static readonly ParameterDescriptor[] _contextParameters = new[]
	{
		new ParameterDescriptor("configuration", typeof(string).FullName)
	};

	public override ParameterDescriptor[] GetContextConstructorParameters(IConnectionInfo cxInfo)
	{
		var settings = new Settings(cxInfo);

		if (settings.CustomConfiguration != null)
			return _contextParameters;

		return base.GetContextConstructorParameters(cxInfo);
	}

	public override object[] GetContextConstructorArguments(IConnectionInfo cxInfo)
	{
		TryLoadAppSettingsJson(cxInfo.AppConfigPath);

		var configuration = new Settings(cxInfo).CustomConfiguration;

		if (configuration != null)
			return new object[] { configuration };

		return base.GetContextConstructorArguments(cxInfo);
	}

	public override IEnumerable<string> GetNamespacesToAdd(IConnectionInfo cxInfo) => DriverHelper.DefaultImports;

	public override void ClearConnectionPools(IConnectionInfo cxInfo) => DriverHelper.ClearConnectionPools(new(cxInfo));

	public override void InitializeContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager)
	{
		(_mappingSchema, _useCustomFormatter) = DriverHelper.InitializeContext(new(cxInfo), (DataConnection)context, executionManager);
	}

	public override void TearDownContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager, object[] constructorArguments)
	{
		if (context is IDisposable ctx)
			ctx.Dispose();
	}

	public override void PreprocessObjectToWrite(ref object? objectToWrite, ObjectGraphInfo info)
	{
		objectToWrite = _useCustomFormatter
			? XmlFormatter.Format(_mappingSchema!, objectToWrite)
			: XmlFormatter.FormatValue(objectToWrite);
	}

	private void TryLoadAppSettingsJson(string? appConfigPath)
	{
#if LPX6
		if (appConfigPath?.EndsWith(".json", StringComparison.OrdinalIgnoreCase) == true)
			DataConnection.DefaultSettings = AppJsonConfig.Load(appConfigPath);
#endif
	}
}
