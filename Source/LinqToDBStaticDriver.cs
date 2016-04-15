using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

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
			return new SchemaGenerator(cxInfo, customType).GetSchema().ToList();
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
			{
				var connection = db.Connection as SqlConnection;
				if (connection != null)
					SqlConnection.ClearPool(connection);
			}
		}

		public override void InitializeContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager)
		{
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
