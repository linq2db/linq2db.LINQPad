using System.Data.OleDb;
using System.Data.Odbc;
using System.Data;
using System.Runtime.InteropServices;
using LinqToDB.DataProvider;

namespace LinqToDB.LINQPad;

internal sealed class AccessProvider : IDatabaseProvider
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new (ProviderName.Access    , "OLE DB"),
		new (ProviderName.AccessOdbc, "ODBC"  ),
	};

	string                      IDatabaseProvider.Database                    => ProviderName.Access;
	string                      IDatabaseProvider.Description                 => "Microsoft Access";
	IReadOnlyList<ProviderInfo> IDatabaseProvider.Providers                   => _providers;
	bool                        IDatabaseProvider.SupportsSecondaryConnection => true;
	bool                        IDatabaseProvider.AutomaticProviderSelection  => true;

	IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences(string  providerName) => Array.Empty<Assembly>();
	bool                          IDatabaseProvider.IsProviderPathSupported(string  providerName) => false;
	string?                       IDatabaseProvider.GetProviderAssemblyName(string  providerName) => null;
	string?                       IDatabaseProvider.GetProviderDownloadUrl (string? providerName) => "https://www.microsoft.com/en-us/download/details.aspx?id=54920";
	string?                       IDatabaseProvider.TryGetDefaultPath      (string  providerName) => null;
	string                        IDatabaseProvider.GetProviderFactoryName (string  providerName) => providerName == ProviderName.Access ? "System.Data.OleDb" : "System.Data.Odbc";

	void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath) { }

	void IDatabaseProvider.ClearAllPools(string providerName)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && providerName == ProviderName.Access)
			OleDbConnection.ReleaseObjectPool();

		if (providerName == ProviderName.AccessOdbc)
			OdbcConnection.ReleaseObjectPool();
	}

	DateTime? IDatabaseProvider.GetLastSchemaUpdate(ConnectionSettings settings)
	{
		var connectionString = settings.Connection.Provider == ProviderName.Access
			? settings.Connection.ConnectionString
			: settings.Connection.SecondaryProvider == ProviderName.Access
				? settings.Connection.SecondaryConnectionString
				: null;

		if (connectionString == null || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return null;

		// only OLE DB schema has required information
		IDataProvider provider;
		if (settings.Connection.Provider == ProviderName.Access)
			provider = DatabaseProviders.GetDataProvider(settings);
		else
			provider = DatabaseProviders.GetDataProvider(settings.Connection.SecondaryProvider, settings.Connection.SecondaryConnectionString, null);

		using var cn = (OleDbConnection)provider.CreateConnection(connectionString);
		cn.Open();

		var dt1 = cn.GetSchema("Tables"    ).Rows.Cast<DataRow>().Max(r => (DateTime)r["DATE_MODIFIED"]);
		var dt2 = cn.GetSchema("Procedures").Rows.Cast<DataRow>().Max(r => (DateTime)r["DATE_MODIFIED"]);
		return dt1 > dt2 ? dt1 : dt2;
	}

	ProviderInfo? IDatabaseProvider.GetProviderByConnectionString(string connectionString)
	{
		var isOleDb = connectionString.IndexOf("Microsoft.Jet.OLEDB", StringComparison.OrdinalIgnoreCase) != -1
			|| connectionString.IndexOf("Microsoft.ACE.OLEDB", StringComparison.OrdinalIgnoreCase) != -1;

		// we don't check for ODBC provider marker - it will fail on connection test if wrong
		return _providers[isOleDb ? 0 : 1];
	}
}
