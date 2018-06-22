using System;
using System.Diagnostics;

using LinqToDB.Data;
using LinqToDB.DataProvider.Firebird;

using LINQPad.Extensibility.DataContext;

namespace LinqToDB.LINQPad
{
	public class LINQPadDataConnection : DataConnection
	{
		static LINQPadDataConnection()
		{
			AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
			{
//				Console.WriteLine(args.Name);
//				Debug.WriteLine(args.Name);
				return null;
			};
		}

		public LINQPadDataConnection()
		{
			Init();
		}

		public LINQPadDataConnection(string providerName, string connectionString)
			: base(ProviderHelper.GetDataProvider(providerName, connectionString), connectionString)
		{
			Init();
		}

		public LINQPadDataConnection(IConnectionInfo cxInfo)
			: this(
				(string)(cxInfo.DriverData.Element("providerVersion") ?? cxInfo.DriverData.Element("providerName")),
				cxInfo.DatabaseInfo.CustomCxString)
		{
		}

		static void Init()
		{
			TurnTraceSwitchOn();
			FirebirdSqlBuilder.IdentifierQuoteMode = FirebirdIdentifierQuoteMode.Auto;
		}
	}
}
