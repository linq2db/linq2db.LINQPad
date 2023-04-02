using System.Data.Common;
using ClickHouse.Client.ADO;
using LinqToDB.Data;
using MySqlConnector;
#if LPX6
using Octonica.ClickHouseClient;
#endif

namespace LinqToDB.LINQPad;

internal sealed class ClickHouseProvider : DatabaseProviderBase
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new (ProviderName.ClickHouseClient  , "HTTP(S) Interface"     ),
		new (ProviderName.ClickHouseMySql   , "MySQL Interface"       ),
#if LPX6
		// octonica provider doesn't support NETFX or NESTANDARD
		new (ProviderName.ClickHouseOctonica, "Binary (TCP) Interface"),
#endif
	};

	public ClickHouseProvider()
		: base(ProviderName.ClickHouse, "ClickHouse", _providers)
	{
	}

	public override void ClearAllPools(string providerName)
	{
		// octonica provider doesn't implement connection pooling
		// client provider use http connections pooling
		if (providerName == ProviderName.ClickHouseMySql)
			MySqlConnection.ClearAllPools();
	}

	public override DateTime? GetLastSchemaUpdate(ConnectionSettings settings)
	{
		using var db = new LINQPadDataConnection(settings);
		return db.Query<DateTime?>("SELECT MAX(metadata_modification_time) FROM system.tables WHERE database = database()").FirstOrDefault();
	}

	public override DbProviderFactory GetProviderFactory(string providerName)
	{
		if (providerName == ProviderName.ClickHouseClient)
			return new ClickHouseConnectionFactory();
#if LPX6
		if (providerName == ProviderName.ClickHouseOctonica)
			return new ClickHouseDbProviderFactory();
#endif

		return MySqlConnectorFactory.Instance;
	}
}
