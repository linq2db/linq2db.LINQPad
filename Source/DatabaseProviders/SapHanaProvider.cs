using System.Data.Common;
using System.Data.Odbc;
using LinqToDB.Data;
using LinqToDB.DataProvider.SapHana;
#if LPX6
using System.IO;
#endif

namespace LinqToDB.LINQPad;

internal sealed class SapHanaProvider : DatabaseProviderBase
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new(ProviderName.SapHanaNative, "Native Provider (Sap.Data.Hana)"),
		new(ProviderName.SapHanaOdbc  , "ODBC Provider (HANAODBC/HANAODBC32)"),
	};

	public SapHanaProvider()
		: base(ProviderName.SapHana, "SAP HANA", _providers)
	{
	}

	public override string? GetProviderDownloadUrl(string? providerName)
	{
		return "https://tools.hana.ondemand.com/#hanatools";
	}

	public override void ClearAllPools(string providerName)
	{
		if (providerName == ProviderName.SapHanaOdbc)
			OdbcConnection.ReleaseObjectPool();
		else if (providerName == ProviderName.SapHanaNative)
		{
			var typeName = $"{SapHanaProviderAdapter.ClientNamespace}.HanaConnection, {SapHanaProviderAdapter.AssemblyName}";
			Type.GetType(typeName, false)?.GetMethod("ClearAllPools", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
		}
	}

	public override DateTime? GetLastSchemaUpdate(ConnectionSettings settings)
	{
		using var db = new LINQPadDataConnection(settings);
		return db.Query<DateTime?>("SELECT MAX(time) FROM (SELECT MAX(CREATE_TIME) AS time FROM M_CS_TABLES UNION SELECT MAX(MODIFY_TIME) FROM M_CS_TABLES UNION SELECT MAX(CREATE_TIME) FROM M_RS_TABLES UNION SELECT MAX(CREATE_TIME) FROM PROCEDURES UNION SELECT MAX(CREATE_TIME) FROM FUNCTIONS)").FirstOrDefault();
	}

	public override DbProviderFactory GetProviderFactory(string providerName)
	{
		if (providerName == ProviderName.SapHanaOdbc)
			return OdbcFactory.Instance;

		var typeName = $"{SapHanaProviderAdapter.ClientNamespace}.HanaFactory, {SapHanaProviderAdapter.AssemblyName}";
		return (DbProviderFactory)Type.GetType(typeName, false)?.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)!;
	}

#if LPX6
	public override bool IsProviderPathSupported(string providerName)
	{
		return providerName == ProviderName.SapHanaNative;
	}

	public override string? GetProviderAssemblyName(string providerName)
	{
		return providerName == ProviderName.SapHanaNative ? "Sap.Data.Hana.Core.v2.1.dll" : null;
	}

	public override string? TryGetDefaultPath(string providerName)
	{
		if (providerName == ProviderName.SapHanaNative)
		{
			var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

			if (!string.IsNullOrEmpty(programFiles))
			{

				var path = Path.Combine(programFiles, "sap\\hdbclient\\dotnetcore\\v2.1\\Sap.Data.Hana.Core.v2.1.dll");

				if (File.Exists(path))
					return path;
			}
		}

		return null;
	}

	private static bool _factoryRegistered;
	public override void RegisterProviderFactory(string providerName, string providerPath)
	{
		if (providerName == ProviderName.SapHanaNative && !_factoryRegistered)
		{
			if (!File.Exists(providerPath))
				throw new LinqToDBLinqPadException($"Cannot find SAP HANA provider assembly at '{providerPath}'");

			try
			{
				var sapHanaAssembly = Assembly.LoadFrom(providerPath);
				DbProviderFactories.RegisterFactory("Sap.Data.Hana", sapHanaAssembly.GetType("Sap.Data.Hana.HanaFactory")!);
				_factoryRegistered = true;
			}
			catch (Exception ex)
			{
				throw new LinqToDBLinqPadException($"Failed to initialize SAP HANA provider factory: ({ex.GetType().Name}) {ex.Message}");
			}

			// TODO: remove after testing
			//try
			//{
			//	var targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory!, Path.GetFileName(providerPath));

			//	if (File.Exists(providerPath))
			//	{
			//		// original path contains spaces which breaks broken native dlls discovery logic in SAP provider
			//		// (at least SPS04 040)
			//		// if you run tests from path with spaces - it will not help you
			//		File.Copy(providerPath, targetPath, true);
			//		var sapHanaAssembly = Assembly.LoadFrom(targetPath);
			//		DbProviderFactories.RegisterFactory("Sap.Data.Hana", sapHanaAssembly.GetType("Sap.Data.Hana.HanaFactory")!);
			//		_factoryRegistered = true;
			//	}
			//}
			//catch (Exception ex)
			//{
			//	throw new LinqToDBLinqPadException($"Failed to register SAP HANA factory: ({ex.GetType().Name}) {ex.Message}");
			//}
		}
	}
#endif
}
