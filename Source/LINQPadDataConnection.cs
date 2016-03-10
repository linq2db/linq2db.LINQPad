using System;

using LinqToDB.Data;

using LINQPad.Extensibility.DataContext;

namespace LinqToDB.LINQPad
{
	public class LINQPadDataConnection : DataConnection
	{
		public LINQPadDataConnection()
		{
			TurnTraceSwitchOn();
		}

		public LINQPadDataConnection(string provaderName, string connectionString)
			: base(provaderName, connectionString)
		{
			TurnTraceSwitchOn();
		}

		public LINQPadDataConnection(IConnectionInfo cxInfo)
			: base((string)cxInfo.DriverData.Element("providerName"), cxInfo.DatabaseInfo.CustomCxString)
		{
			TurnTraceSwitchOn();
		}
	}
}
