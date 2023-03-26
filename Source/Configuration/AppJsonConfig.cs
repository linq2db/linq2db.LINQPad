using System.IO;
using System.Text.Json;
using LinqToDB.Configuration;

namespace LinqToDB.LINQPad;

/// <summary>
/// Implements Linq To DB connection settings provider, which use data from JSON config.
/// Used as settings source for static data context.
/// </summary>
internal sealed class AppJsonConfig : ILinqToDBSettings
{
	public static ILinqToDBSettings Load(string configPath)
	{
		var config = JsonSerializer.Deserialize<JsonConfig>(File.ReadAllText(configPath));

		return new AppJsonConfig(config?.ConnectionStrings?.Select(static entry => (IConnectionStringSettings)new ConnectionStringSettings(entry.Key, entry.Value)).ToArray()
			?? Array.Empty<IConnectionStringSettings>());
	}

	private readonly IConnectionStringSettings[] _connectionStrings;

	public AppJsonConfig(IConnectionStringSettings[] connectionStrings)
	{
		_connectionStrings = connectionStrings;
	}

	IEnumerable<IDataProviderSettings>     ILinqToDBSettings.DataProviders        => Array.Empty<IDataProviderSettings>();
	string?                                ILinqToDBSettings.DefaultConfiguration => null;
	string?                                ILinqToDBSettings.DefaultDataProvider  => null;
	IEnumerable<IConnectionStringSettings> ILinqToDBSettings.ConnectionStrings    => _connectionStrings;

	private sealed class JsonConfig
	{
		public IDictionary<string, string>? ConnectionStrings { get; set; }
	}

	private sealed class ConnectionStringSettings : IConnectionStringSettings
	{
		private readonly string _name;
		private readonly string _connectionString;

		public ConnectionStringSettings(string name, string connectionString)
		{
			_name             = name;
			_connectionString = connectionString;
		}

		string  IConnectionStringSettings.ConnectionString => _connectionString;
		string  IConnectionStringSettings.Name             => _name;
		string? IConnectionStringSettings.ProviderName     => null;
		bool    IConnectionStringSettings.IsGlobal         => false;
	}
}
