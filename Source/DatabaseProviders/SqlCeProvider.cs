#if LPX6
using System.Data.Common;
using System.IO;
#endif

namespace LinqToDB.LINQPad;

internal sealed class SqlCeProvider : IDatabaseProvider
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new (ProviderName.SqlCe, "Microsoft SQL Server Compact Edition")
	};

	string                      IDatabaseProvider.Database                    => ProviderName.SqlCe;
	string                      IDatabaseProvider.Description                 => "Microsoft SQL Server Compact Edition (SQL CE)";
	IReadOnlyList<ProviderInfo> IDatabaseProvider.Providers                   => _providers;
	bool                        IDatabaseProvider.SupportsSecondaryConnection => false;
	bool                        IDatabaseProvider.AutomaticProviderSelection  => false;

	ProviderInfo?                 IDatabaseProvider.GetProviderByConnectionString(string connectionString    ) => null;
	// no information in schema
	DateTime?                     IDatabaseProvider.GetLastSchemaUpdate          (ConnectionSettings settings) => null;
	IReadOnlyCollection<Assembly> IDatabaseProvider.GetAdditionalReferences      (string  providerName       ) => Array.Empty<Assembly>();
	string                        IDatabaseProvider.GetProviderFactoryName       (string  providerName       ) => "System.Data.SqlServerCe.4.0";
	string?                       IDatabaseProvider.GetProviderDownloadUrl       (string? providerName       ) => "https://www.microsoft.com/en-us/download/details.aspx?id=30709";

	// connection pooling not supported by provider
	void IDatabaseProvider.ClearAllPools(string providerName) { }

#if LPX6
	bool    IDatabaseProvider.IsProviderPathSupported(string providerName) => true;
	string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => "System.Data.SqlServerCe.dll";

	string? IDatabaseProvider.TryGetDefaultPath(string providerName)
	{
		var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
		if (!string.IsNullOrEmpty(programFiles))
		{

			var path = Path.Combine(programFiles, "Microsoft SQL Server Compact Edition\\v4.0\\Private\\System.Data.SqlServerCe.dll");

			if (File.Exists(path))
				return path;
		}

		return null;
	}

	private static bool _factoryRegistered;
	void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath)
	{
		if (_factoryRegistered)
			return;

		if (!File.Exists(providerPath))
			throw new LinqToDBLinqPadException($"Cannot find SQL CE provider assembly at '{providerPath}'");

		try
		{
			var assembly = Assembly.LoadFrom(providerPath);
			DbProviderFactories.RegisterFactory("System.Data.SqlServerCe.4.0", assembly.GetType("System.Data.SqlServerCe.SqlCeProviderFactory")!);
			_factoryRegistered = true;
		}
		catch (Exception ex)
		{
			throw new LinqToDBLinqPadException($"Failed to initialize SQL CE provider factory: ({ex.GetType().Name}) {ex.Message}");
		}
	}

#else
	bool    IDatabaseProvider.IsProviderPathSupported(string providerName) => false;
	string? IDatabaseProvider.GetProviderAssemblyName(string providerName) => null;
	string? IDatabaseProvider.TryGetDefaultPath      (string providerName) => null;

	void IDatabaseProvider.RegisterProviderFactory(string providerName, string providerPath) { }
#endif
}
