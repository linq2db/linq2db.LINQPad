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

		[Obsolete]
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
			var configuration = cxInfo.DriverData.Element("customConfiguration")?.Value;

			if (configuration != null)
				return new[] { new ParameterDescriptor("configuration", typeof(string).FullName) };

			return base.GetContextConstructorParameters(cxInfo);
		}

		public override object[] GetContextConstructorArguments(IConnectionInfo cxInfo)
		{
			var configuration = cxInfo.DriverData.Element("customConfiguration")?.Value;

			if (configuration != null)
				return new object[] { configuration };

			return base.GetContextConstructorArguments(cxInfo);
		}

		public override void ClearConnectionPools(IConnectionInfo cxInfo) => DriverHelper.ClearConnectionPools(cxInfo);

		public override void InitializeContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager)
		{
			var allowMultipleQuery = cxInfo.DriverData.Element("allowMultipleQuery") == null || cxInfo.DriverData.Element("allowMultipleQuery")?.Value.ToLower() == "true";
			var optimizeJoins      = cxInfo.DriverData.Element("optimizeJoins")      == null || cxInfo.DriverData.Element("optimizeJoins")     ?.Value.ToLower() == "true";

			Common.Configuration.Linq.OptimizeJoins      = optimizeJoins;
			Common.Configuration.Linq.AllowMultipleQuery = allowMultipleQuery;

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

//		public override void PreprocessObjectToWrite(ref object objectToWrite, ObjectGraphInfo info)
//		{
//			objectToWrite = XmlFormatter.FormatValue(objectToWrite, info);
//		}
	}
}
