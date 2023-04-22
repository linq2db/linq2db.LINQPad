using System.Data.Common;
using FirebirdSql.Data.FirebirdClient;

namespace LinqToDB.LINQPad;

internal sealed class FirebirdProvider : DatabaseProviderBase
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new (ProviderName.Firebird, "Firebird"),
	};

	public FirebirdProvider()
		: base(ProviderName.Firebird, "Firebird", _providers)
	{
	}

	public override void ClearAllPools(string providerName)
	{
		FbConnection.ClearAllPools();
	}

	public override DateTime? GetLastSchemaUpdate(ConnectionSettings settings)
	{
		// no information in schema
		return null;
	}

	public override DbProviderFactory GetProviderFactory(string providerName)
	{
		return FirebirdClientFactory.Instance;
	}
}
