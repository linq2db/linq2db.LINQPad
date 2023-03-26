using System.Data.Common;
using LinqToDB.Data;
using MySqlConnector;

namespace LinqToDB.LINQPad;

internal sealed class MySqlProvider : DatabaseProviderBase
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new (ProviderName.MySqlConnector, "MySql/MariaDB"),
	};

	public MySqlProvider()
		: base(ProviderName.MySql, "MySql/MariaDB", _providers)
	{
	}

	public override void ClearAllPools(string providerName)
	{
		MySqlConnection.ClearAllPools();
	}

	public override DateTime? GetLastSchemaUpdate(ConnectionSettings settings)
	{
		using var db = new LINQPadDataConnection(settings);
		return db.Query<DateTime?>("SELECT MAX(u.time) FROM (SELECT MAX(UPDATE_TIME) AS time FROM information_schema.TABLES UNION SELECT MAX(CREATE_TIME) FROM information_schema.TABLES UNION SELECT MAX(LAST_ALTERED) FROM information_schema.ROUTINES) as u").FirstOrDefault();
	}

	public override DbProviderFactory GetProviderFactory(string providerName)
	{
		return MySqlConnectorFactory.Instance;
	}
}
