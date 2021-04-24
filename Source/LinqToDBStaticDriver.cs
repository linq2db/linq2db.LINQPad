using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using LINQPad.Extensibility.DataContext;
using LinqToDB.Data;

namespace LinqToDB.LINQPad
{
	public class LinqToDBStaticDriver : StaticDataContextDriver
	{
		public override string Name   => "LINQ to DB (DataConnection)";
		public override string Author => DriverHelper.Author;

		static LinqToDBStaticDriver()
		{
			DriverHelper.Init();
		}

		public override string GetConnectionDescription(IConnectionInfo cxInfo) => DriverHelper.GetConnectionDescription(cxInfo);

		[Obsolete("base method obsoleted")]
		public override bool ShowConnectionDialog(IConnectionInfo cxInfo, bool isNewConnection) => DriverHelper.ShowConnectionDialog(cxInfo, isNewConnection, false);

		public override List<ExplorerItem> GetSchema(IConnectionInfo cxInfo, Type customType)
		{
			try
			{
				return new SchemaGenerator(cxInfo, customType).GetSchema().ToList();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"{ex}\n{ex.StackTrace}", "Schema Build Error", MessageBoxButton.OK, MessageBoxImage.Error);
				throw;
			}
		}

		public override ParameterDescriptor[] GetContextConstructorParameters(IConnectionInfo cxInfo)
		{
			var configuration = cxInfo.DriverData.Element(CX.CustomConfiguration)?.Value;

			if (configuration != null)
				return new[] { new ParameterDescriptor("configuration", typeof(string).FullName) };

			return base.GetContextConstructorParameters(cxInfo);
		}

		public override object[] GetContextConstructorArguments(IConnectionInfo cxInfo)
		{
			TryLoadAppSettingsJson(cxInfo);

			var configuration = cxInfo.DriverData.Element(CX.CustomConfiguration)?.Value;

			if (configuration != null)
				return new object[] { configuration };

			return base.GetContextConstructorArguments(cxInfo);
		}

		public override void ClearConnectionPools(IConnectionInfo cxInfo) => DriverHelper.ClearConnectionPools(cxInfo);

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

		public override void TearDownContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager,
			object[] constructorArguments)
		{
			dynamic ctx = context;
			ctx.Dispose();
		}

		private void TryLoadAppSettingsJson(IConnectionInfo cxInfo)
		{
			#if NETCORE
			if (cxInfo.AppConfigPath?.EndsWith(".json", StringComparison.OrdinalIgnoreCase) == true)
				DataConnection.DefaultSettings = AppJsonConfig.Load(cxInfo.AppConfigPath!);
			#endif
		}
	}
}
