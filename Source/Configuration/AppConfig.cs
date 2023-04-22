using System.IO;
using System.Text.Json;
using System.Xml;
using LinqToDB.Configuration;

namespace LinqToDB.LINQPad;

/// <summary>
/// Implements Linq To DB connection settings provider, which use data from JSON config.
/// Used as settings source for static data context.
/// </summary>
internal sealed class AppConfig : ILinqToDBSettings
{
	public static ILinqToDBSettings LoadJson(string configPath)
	{
		var config = JsonSerializer.Deserialize<JsonConfig>(File.ReadAllText(configPath));

		if (config?.ConnectionStrings?.Count is null or 0)
			return new AppConfig(Array.Empty<IConnectionStringSettings>());

		var connections = new Dictionary<string, ConnectionStringSettings>(StringComparer.InvariantCultureIgnoreCase);
		foreach (var cn in config.ConnectionStrings)
		{
			if (cn.Key.EndsWith("_ProviderName", StringComparison.InvariantCultureIgnoreCase))
				continue;

			connections.Add(cn.Key, new ConnectionStringSettings(cn.Key, cn.Value));
		}

		foreach (var cn in config.ConnectionStrings)
		{
			if (!cn.Key.EndsWith("_ProviderName", StringComparison.InvariantCultureIgnoreCase))
				continue;

			var key = cn.Key.Substring(0, cn.Key.Length - "_ProviderName".Length);
			if (connections.TryGetValue(key, out var cs))
				cs.ProviderName = cn.Value;
		}

		return new AppConfig(connections.Values.ToArray());
	}

	public static ILinqToDBSettings LoadAppConfig(string configPath)
	{
		var xml = new XmlDocument() { XmlResolver = null };
		xml.Load(XmlReader.Create(new StringReader(File.ReadAllText(configPath)), new XmlReaderSettings() { XmlResolver = null }));
		
		var connections = xml.SelectNodes("/configuration/connectionStrings/add");

		if (connections?.Count is null or 0)
			return new AppConfig(Array.Empty<IConnectionStringSettings>());

		var settings = new List<ConnectionStringSettings>();

		foreach (XmlElement node in connections)
		{
			var name             = node.Attributes["name"            ]?.Value;
			var connectionString = node.Attributes["connectionString"]?.Value;
			var providerName     = node.Attributes["providerName"    ]?.Value;

			if (name != null && connectionString != null)
				settings.Add(new ConnectionStringSettings(name, connectionString) { ProviderName = providerName });
		}

		return new AppConfig(settings.ToArray());
	}

	private readonly IConnectionStringSettings[] _connectionStrings;

	public AppConfig(IConnectionStringSettings[] connectionStrings)
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
		bool    IConnectionStringSettings.IsGlobal         => false;

		public string? ProviderName { get; set; }
	}
}
