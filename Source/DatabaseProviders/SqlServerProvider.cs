using System.Data.Common;
using LinqToDB.Data;
using LinqToDB.DataProvider;
using LinqToDB.DataProvider.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Types;

namespace LinqToDB.LINQPad;

internal sealed class SqlServerProvider : DatabaseProviderBase
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new (ProviderName.SqlServer2005, "SQL Server 2005 Dialect"),
		new (ProviderName.SqlServer2008, "SQL Server 2008 Dialect"),
		new (ProviderName.SqlServer2012, "SQL Server 2012 Dialect"),
		new (ProviderName.SqlServer2014, "SQL Server 2014 Dialect"),
		new (ProviderName.SqlServer2016, "SQL Server 2016 Dialect"),
		new (ProviderName.SqlServer2017, "SQL Server 2017 Dialect"),
		new (ProviderName.SqlServer2019, "SQL Server 2019 Dialect"),
		new (ProviderName.SqlServer2022, "SQL Server 2022 Dialect"),
	};

	public SqlServerProvider()
		: base(ProviderName.SqlServer, "Microsoft SQL Server", _providers)
	{
	}

	private static readonly IReadOnlyCollection<Assembly> _additionalAssemblies = new[] { typeof(SqlHierarchyId).Assembly };

	public override void ClearAllPools(string providerName)
	{
		SqlConnection.ClearAllPools();
	}

	public override IReadOnlyCollection<Assembly> GetAdditionalReferences(string providerName)
	{
		return _additionalAssemblies;
	}

	public override DateTime? GetLastSchemaUpdate(ConnectionSettings settings)
	{
		using var db = new LINQPadDataConnection(settings);
		return db.Query<DateTime?>("SELECT MAX(modify_date) FROM sys.objects").FirstOrDefault();
	}

	public override DbProviderFactory GetProviderFactory(string providerName)
	{
		return SqlClientFactory.Instance;
	}

	public override IDataProvider GetDataProvider(string providerName, string connectionString)
	{
		// provider detector fails to detect Microsoft.Data.SqlClient
		// kinda regression in linq2db v5
		return providerName switch
		{
			ProviderName.SqlServer2005 => SqlServerTools.GetDataProvider(SqlServerVersion.v2005, DataProvider.SqlServer.SqlServerProvider.MicrosoftDataSqlClient, connectionString),
			ProviderName.SqlServer2008 => SqlServerTools.GetDataProvider(SqlServerVersion.v2008, DataProvider.SqlServer.SqlServerProvider.MicrosoftDataSqlClient, connectionString),
			ProviderName.SqlServer2012 => SqlServerTools.GetDataProvider(SqlServerVersion.v2012, DataProvider.SqlServer.SqlServerProvider.MicrosoftDataSqlClient, connectionString),
			ProviderName.SqlServer2014 => SqlServerTools.GetDataProvider(SqlServerVersion.v2014, DataProvider.SqlServer.SqlServerProvider.MicrosoftDataSqlClient, connectionString),
			ProviderName.SqlServer2016 => SqlServerTools.GetDataProvider(SqlServerVersion.v2016, DataProvider.SqlServer.SqlServerProvider.MicrosoftDataSqlClient, connectionString),
			ProviderName.SqlServer2017 => SqlServerTools.GetDataProvider(SqlServerVersion.v2017, DataProvider.SqlServer.SqlServerProvider.MicrosoftDataSqlClient, connectionString),
			ProviderName.SqlServer2019 => SqlServerTools.GetDataProvider(SqlServerVersion.v2019, DataProvider.SqlServer.SqlServerProvider.MicrosoftDataSqlClient, connectionString),
			ProviderName.SqlServer2022 => SqlServerTools.GetDataProvider(SqlServerVersion.v2022, DataProvider.SqlServer.SqlServerProvider.MicrosoftDataSqlClient, connectionString),
			_                          => base.GetDataProvider(providerName, connectionString)
		};
	}
}
