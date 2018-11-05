using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;

using JetBrains.Annotations;

using LinqToDB.Data;

using LINQPad.Extensibility.DataContext;

namespace LinqToDB.LINQPad
{
	[UsedImplicitly]
	public class LinqToDBStaticDriver : StaticDataContextDriver
	{
		public override string Name   => "LINQ to DB (DataConnection)";
		public override string Author => "Igor Tkachev";

		static LinqToDBStaticDriver()
		{
			DriverHelper.ConfigureRedirects();
		}

		public override string GetConnectionDescription(IConnectionInfo cxInfo)
		{
			var providerName = (string)cxInfo.DriverData.Element("providerName");
			var dbInfo       = cxInfo.DatabaseInfo;

			return $"[{providerName}] {dbInfo.Server}\\{dbInfo.Database} (v.{dbInfo.DbVersion})";
		}

		public override bool ShowConnectionDialog(IConnectionInfo cxInfo, bool isNewConnection)
		{
			return DriverHelper.ShowConnectionDialog(this, cxInfo, isNewConnection, false);
		}

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

		public override void ClearConnectionPools(IConnectionInfo cxInfo)
		{
			using (var db = new LINQPadDataConnection(cxInfo))
				if (db.Connection is SqlConnection connection)
					SqlConnection.ClearPool(connection);
		}

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
