using LINQPad.Extensibility.DataContext;
using LinqToDB.Data;

namespace LinqToDB.LINQPad
{
	public class LINQPadDataConnection : DataConnection
	{
		public LINQPadDataConnection()
		{
			Init();
			InitMappingSchema();
		}

		public LINQPadDataConnection(string providerName, string? providerPath, string connectionString)
			: base(ProviderHelper.GetProvider(providerName, providerPath).GetDataProvider(connectionString), connectionString)
		{
			Init();
			InitMappingSchema();
		}

		public LINQPadDataConnection(IConnectionInfo cxInfo)
			: this(
				(string)cxInfo.DriverData.Element(CX.ProviderName),
				(string?)cxInfo.DriverData.Element(CX.ProviderPath),
				cxInfo.DatabaseInfo.CustomCxString)
		{
		}

		protected virtual void InitMappingSchema()
		{
		}

		static void Init()
		{
			TurnTraceSwitchOn();
		}
	}
}
