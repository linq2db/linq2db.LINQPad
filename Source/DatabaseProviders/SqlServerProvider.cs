using System.Data.Common;
using LinqToDB.Data;
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
}
