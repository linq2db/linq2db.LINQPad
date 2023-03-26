using System.Data.Common;
using System.Data.OleDb;
using System.Data.Odbc;
using System.Data;
using System.Runtime.InteropServices;
using LinqToDB.DataProvider;

namespace LinqToDB.LINQPad;

internal sealed class AccessProvider : DatabaseProviderBase
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new (ProviderName.Access    , "OLE DB"),
		new (ProviderName.AccessOdbc, "ODBC"  ),
	};

	public AccessProvider()
		: base(ProviderName.Access, "Microsoft Access", _providers)
	{
	}

	public override bool SupportsSecondaryConnection => true;
	public override bool AutomaticProviderSelection  => true;

	public override string? GetProviderDownloadUrl(string? providerName)
	{
		return "https://www.microsoft.com/en-us/download/details.aspx?id=54920";
	}

	public override void ClearAllPools(string providerName)
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && providerName == ProviderName.Access)
			OleDbConnection.ReleaseObjectPool();

		if (providerName == ProviderName.AccessOdbc)
			OdbcConnection.ReleaseObjectPool();
	}

	public override DateTime? GetLastSchemaUpdate(ConnectionSettings settings)
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

		var dt1 = cn.GetSchema("Tables"    ).Rows.Cast<DataRow>().Max(static r => (DateTime)r["DATE_MODIFIED"]);
		var dt2 = cn.GetSchema("Procedures").Rows.Cast<DataRow>().Max(static r => (DateTime)r["DATE_MODIFIED"]);
		return dt1 > dt2 ? dt1 : dt2;
	}

	public override ProviderInfo? GetProviderByConnectionString(string connectionString)
	{
		var isOleDb = connectionString.IndexOf("Microsoft.Jet.OLEDB", StringComparison.OrdinalIgnoreCase) != -1
			|| connectionString.IndexOf("Microsoft.ACE.OLEDB", StringComparison.OrdinalIgnoreCase) != -1;

		// we don't check for ODBC provider marker - it will fail on connection test if wrong
		return _providers[isOleDb ? 0 : 1];
	}

	public override DbProviderFactory GetProviderFactory(string providerName)
	{
		if (providerName == ProviderName.AccessOdbc)
			return OdbcFactory.Instance;

		return OleDbFactory.Instance;
	}
}
