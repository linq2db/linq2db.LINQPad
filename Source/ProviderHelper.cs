using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using LinqToDB.Common;
using LinqToDB.Configuration;
using LinqToDB.Data;
using LinqToDB.DataProvider;

namespace LinqToDB.LINQPad
{
	internal class ProviderHelper
	{
		static Dictionary<string, DynamicProviderRecord> _dynamicProviders = new Dictionary<string, DynamicProviderRecord>();

		public static DynamicProviderRecord[] DynamicProviders => _dynamicProviders.Values.ToArray();

		static readonly Dictionary<string, LoadProviderInfo> LoadedProviders = new Dictionary<string, LoadProviderInfo>();

		static void AddDataProvider([NotNull] DynamicProviderRecord providerInfo)
		{
			if (providerInfo == null) throw new ArgumentNullException(nameof(providerInfo));
			_dynamicProviders.Add(providerInfo.ProviderName, providerInfo);
		}

		static ProviderHelper()
		{
			InitializeDataProviders();
		}

		public static class DB2iSeriesProviderName
		{
			public const string DB2         = "DB2.iSeries";
		}

		static void InitializeDataProviders()
		{
			AddDataProvider(new DynamicProviderRecord(ProviderName.Access,        "Microsoft Access",                "System.Data.OleDb.OleDbConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.DB2LUW,        "DB2 for Linux, UNIX and Windows", "IBM.Data.DB2.DB2Connection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.DB2zOS,        "DB2 for z/OS",                    "IBM.Data.DB2.DB2Connection, IBM.Data.DB2"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.Firebird,      "Firebird",                        "FirebirdSql.Data.FirebirdClient.FbConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.OracleNative,  "Oracle ODP.NET",                  "Oracle.DataAccess.Client.OracleConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.OracleManaged, "Oracle Managed Driver",           "Oracle.ManagedDataAccess.Client.OracleConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.MySql,         "MySql",                           "MySql.Data.MySqlClient.MySqlConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.SqlCe,         "Microsoft SQL Server Compact",    "System.Data.SqlServerCe.SqlCeConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.SQLite,        "SQLite",                          "System.Data.SQLite.SQLiteConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.Informix,      "Informix",                        "IBM.Data.Informix.IfxConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.Sybase,        "SAP Sybase ASE",                  "IBM.Data.Informix.IfxConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.SapHana,       "SAP HANA",                        "Sap.Data.Hana.HanaConnection"));

			AddDataProvider(new DynamicProviderRecord(ProviderName.PostgreSQL,    "PostgreSQL", "Npgsql.NpgsqlConnection"));

			AddDataProvider(
				new DynamicProviderRecord(
					ProviderName.SqlServer,
					"Microsoft SQL Server",
					"System.Data.SqlClient.SqlConnection")
				{
					AdditionalNamespaces = new[] { "Microsoft.SqlServer.Types" },
					ProviderLibraries = "Microsoft.SqlServer.Types.dll"
				});

			AddDataProvider(new DynamicProviderRecord(DB2iSeriesProviderName.DB2, "DB2 iSeries (Requires iAccess 7.1 .NET Provider)", "IBM.Data.DB2.iSeries.iDB2Connection")
			{
				InitalizationClassName = "LinqToDB.DataProvider.DB2iSeries.DB2iSeriesTools, LinqToDB.DataProvider.DB2iSeries",
				ProviderLibraries = "LinqToDB.DataProvider.DB2iSeries.dll;IBM.Data.DB2.iSeries.dll"
			});
		}

		public class ProviderRecord
		{
			public ProviderRecord(string libraries, string connectionType, string description)
			{
				Libraries = libraries;
				ConnectionType = connectionType;
				Description = description;
			}

			/// <summary>
			/// Semicolon separated DLL names
			/// </summary>
			public string Libraries     { get; }
			public string ConnectionType { get; }
			public string Description    { get; }

			public IEnumerable<string> GetLibraries() => Libraries.Split(';');
			public string[] AdditionalNamespaces { get; set; }

		}

		public class DynamicProviderRecord
		{
			public string ProviderName     { get; set; }
			public string Description      { get; set; }
			public string InitalizationClassName { get; set; }

			public NamedValue[] ProviderNamedValues { get; set; }
			public string ProviderLibraries  { get; set; }

			public string ConnectionTypeName { get; set; }
			public string[] AdditionalNamespaces { get; set; }

			public IEnumerable<string> GetLibraries() => ProviderLibraries.Split(';');

			public DynamicProviderRecord(string providerName, string description, string connectionTypeName)
			{
				ProviderName = providerName;
				Description = description;
				ConnectionTypeName = connectionTypeName;
			}

		}

		public class LoadProviderInfo
		{
			public LoadProviderInfo(DynamicProviderRecord provider)
			{
				Provider = provider;
			}

			public DynamicProviderRecord Provider          { get; }
			public bool           IsLoaded          { get; private set; }
			public Assembly[]     LoadedAssemblies  { get; private set; }
			public Exception      LoadException     { get; private set; }

			public void Load(string connectionString)
			{
				if (IsLoaded)
					return;

				if (LoadException != null)
					return;

				try
				{
					var loadLibraries = (Provider.ProviderLibraries?.Split(';') ?? Array<string>.Empty)
						.Where(l => !string.IsNullOrEmpty(l))
						.Select(l =>
							{
								try
								{
									return Assembly.Load(Path.GetFileNameWithoutExtension(l));
								}
								catch (Exception e)
								{
									Debug.WriteLine(e.Message);
									return null;
								}
							}
						)
						.Where(l => l != null);

					var providerLibraries = loadLibraries
						.Concat(new[] { typeof(DataConnection).Assembly })
						.ToArray();


					var typeName = Provider.InitalizationClassName;
					if (!string.IsNullOrEmpty(typeName))
					{
						var initType = Type.GetType(typeName, true);
						RuntimeHelpers.RunClassConstructor(initType.TypeHandle);
					}

					var provider = ProviderHelper.GetDataProvider(Provider.ProviderName, connectionString);

					var connectionAssemblies = new List<Assembly> { provider.GetType().Assembly };
					try
					{
						using (var connection = provider.CreateConnection(connectionString))
						{
							connectionAssemblies.Add(connection.GetType().Assembly);
						}
					}
					catch
					{
					}

					LoadedAssemblies = connectionAssemblies
						.Concat(providerLibraries)
						.Distinct().ToArray();

					IsLoaded = true;
				}
				catch (Exception e)
				{
					LoadException = e;
					IsLoaded = false;
				}
			}

			public IEnumerable<string> GetAssemblyLocation(string connectionString)
			{
				Load(connectionString);

				if (LoadedAssemblies != null)
					return LoadedAssemblies.Select(a => a.Location);

				var directory = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);

				if (directory == null)
					return Provider.GetLibraries();
				return Provider.GetLibraries().Select(l => Path.Combine(directory, l));
			}

			public string GetConnectionNamespace()
			{
				var ns = Provider?.ConnectionTypeName?.Split(',').Last().TrimStart();
				if (!string.IsNullOrEmpty(ns)) 
					return ns;

				var path = Provider?.ConnectionTypeName?.Split('.').ToArray();
				if (path?.Length > 1)
				{
					return string.Join(".", path.Take(path.Length - 1));
				}

				return null;
			}

			public IDataProvider GetDataProvider(string connectionString)
			{
				Load(connectionString);
				return ProviderHelper.GetDataProvider(Provider.ProviderName, connectionString);
			}

		}

		public static LoadProviderInfo GetProvider(string providerName)
		{
			if (LoadedProviders.TryGetValue(providerName, out var info))
				return info;

			if (!_dynamicProviders.TryGetValue(providerName, out var providerRecord))
				throw new ArgumentException($"Unknown provider: {providerName}");

			info = new LoadProviderInfo(providerRecord);
			LoadedProviders.Add(providerName, info);

			return info;
		}

		static IDataProvider GetDataProvider(string providerName, string connectionString)
		{
			var provider = DataConnection.GetDataProvider(providerName, connectionString);
			if (provider == null)
				throw new Exception($"Can not activate provider \"{providerName}\"");
			return provider;
		}
	}
}
