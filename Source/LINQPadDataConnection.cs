using System;

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
			InitMappingSchema();
		}

		public LINQPadDataConnection(string providerName, string connectionString)
			: base(ProviderHelper.GetProvider(providerName).GetDataProvider(connectionString), connectionString)
		{
			Init();
			InitMappingSchema();
		}

		public LINQPadDataConnection(IConnectionInfo cxInfo)
			: this(
				(string)cxInfo.DriverData.Element("providerName"),
				cxInfo.DatabaseInfo.CustomCxString)
		{
		}

		protected virtual void InitMappingSchema()
		{
		}

		static void Init()
		{
			TurnTraceSwitchOn();
			FirebirdSqlBuilder.IdentifierQuoteMode = FirebirdIdentifierQuoteMode.Auto;
		}
	}
}
