#if NETCORE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LinqToDB.Configuration;

namespace LinqToDB.LINQPad
{
	internal class AppJsonConfig : ILinqToDBSettings
	{
		public static ILinqToDBSettings Load(string configPath)
		{
			var config = JsonSerializer.Deserialize<JsonConfig>(File.ReadAllText(configPath));

			return new AppJsonConfig(config?.ConnectionStrings?.Select(entry => (IConnectionStringSettings)new ConnectionStringSettings(entry.Key, entry.Value)).ToArray()
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

		private class JsonConfig
		{
			public IDictionary<string, string>? ConnectionStrings { get; set; }
		}

		private class ConnectionStringSettings : IConnectionStringSettings
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
}
#endif
