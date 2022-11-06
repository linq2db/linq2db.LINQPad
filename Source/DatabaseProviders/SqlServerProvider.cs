using LinqToDB.Data;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Types;

namespace LinqToDB.LINQPad;

internal sealed class SqlServerProvider : IDatabaseProvider
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

	private static readonly IReadOnlyCollection<Assembly> _additionalAssemblies = new[] { typeof(SqlHierarchyId).Assembly };

	string                      IDatabaseProvider.Database                    => ProviderName.SqlServer;
	string                      IDatabaseProvider.Description                 => "Microsoft SQL Server";
	bool                        IDatabaseProvider.SupportsSecondaryConnection => false;
	IReadOnlyList<ProviderInfo> IDatabaseProvider.Providers                   => _providers;
	bool                        IDatabaseProvider.AutomaticProviderSelection  => false;

	ProviderInfo?                 IDatabaseProvider.GetProviderByConnectionString(string  connectionString) => null;
	void                          IDatabaseProvider.ClearAllPools                (string  providerName    ) => SqlConnection.ClearAllPools();
	IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences      (string  providerName    ) => _additionalAssemblies;
	bool                          IDatabaseProvider.IsProviderPathSupported      (string  providerName    ) => false;
	string?                       IDatabaseProvider.GetProviderAssemblyName      (string  providerName    ) => null;
	string?                       IDatabaseProvider.GetProviderDownloadUrl       (string? providerName    ) => null;
	string?                       IDatabaseProvider.TryGetDefaultPath            (string  providerName    ) => null;
	string                        IDatabaseProvider.GetProviderFactoryName       (string  providerName    ) => "Microsoft.Data.SqlClient";

	void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath) { }

	DateTime? IDatabaseProvider.GetLastSchemaUpdate(ConnectionSettings settings)
	{
		using var db = new LINQPadDataConnection(settings);
		return db.Query<DateTime?>("SELECT MAX(modify_date) FROM sys.objects").FirstOrDefault();
	}
}
