using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using LinqToDB.Configuration;
using LinqToDB.Data;
using LinqToDB.DataProvider;
#if NETCORE
using System.Data.Common;
#endif

namespace LinqToDB.LINQPad
{
	internal class ProviderHelper
	{
		static readonly Dictionary<string, DynamicProviderRecord> _dynamicProviders = new ();

		public static DynamicProviderRecord[] DynamicProviders => _dynamicProviders.Values.ToArray();

		static readonly Dictionary<string, LoadProviderInfo> LoadedProviders = new ();

		static void AddDataProvider(DynamicProviderRecord providerInfo)
		{
			if (providerInfo == null) throw new ArgumentNullException(nameof(providerInfo));
			_dynamicProviders.Add(providerInfo.ProviderName, providerInfo);
		}

		static ProviderHelper()
		{
			InitializeDataProviders();
		}

		//static class DB2iSeriesProviderName
		//{
		//	public const string DB2         = "DB2.iSeries";
		//}

		static void InitializeDataProviders()
		{
			AddDataProvider(new DynamicProviderRecord(ProviderName.Access        , "Microsoft Access (OleDb)"       , "System.Data.OleDb.OleDbConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.AccessOdbc    , "Microsoft Access (ODBC)"        , "System.Data.Odbc.OdbcConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.Firebird      , "Firebird"                       , "FirebirdSql.Data.FirebirdClient.FbConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.MySqlConnector, "MySql"                          , "MySql.Data.MySqlClient.MySqlConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.PostgreSQL    , "PostgreSQL"                     , "Npgsql.NpgsqlConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.SybaseManaged , "SAP/Sybase ASE"                 , "AdoNetCore.AseClient.AseConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.SQLiteClassic , "SQLite"                         , "System.Data.SQLite.SQLiteConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.SqlCe         , "Microsoft SQL Server Compact"   , "System.Data.SqlServerCe.SqlCeConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.DB2LUW        , "DB2 for Linux, UNIX and Windows", "IBM.Data.DB2.DB2Connection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.DB2zOS        , "DB2 for z/OS"                   , "IBM.Data.DB2.DB2Connection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.InformixDB2   , "Informix (IDS)"                 , "IBM.Data.DB2.DB2Connection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.SapHanaNative , "SAP HANA (Native)"              , "Sap.Data.Hana.HanaConnection"));
			AddDataProvider(new DynamicProviderRecord(ProviderName.SapHanaOdbc   , "SAP HANA (ODBC)"                , "System.Data.Odbc.OdbcConnection"));

			AddDataProvider(new DynamicProviderRecord(ProviderName.OracleManaged , "Oracle (Managed)"               , "Oracle.ManagedDataAccess.Client.OracleConnection"));
			// we use System.Data.SqlClient to be able to use Microsoft.SqlServer.Types
			AddDataProvider(new DynamicProviderRecord(ProviderName.SqlServer     , "Microsoft SQL Server"           , "System.Data.SqlClient.SqlConnection"));

			//AddDataProvider(
			//	new DynamicProviderRecord(
			//		ProviderName.SqlServer,
			//		"Microsoft SQL Server",
			//		"System.Data.SqlClient.SqlConnection")
			//	{
			//		AdditionalNamespaces = new[] { "Microsoft.SqlServer.Types" },
			//		ProviderLibraries = "Microsoft.SqlServer.Types.dll"
			//	});

			//AddDataProvider(new DynamicProviderRecord(DB2iSeriesProviderName.DB2, "DB2 iSeries (Requires iAccess 7.1 .NET Provider)", "IBM.Data.DB2.iSeries.iDB2Connection")
			//{
			//	InitializationClassName = "LinqToDB.DataProvider.DB2iSeries.DB2iSeriesTools, LinqToDB.DataProvider.DB2iSeries",
			//	ProviderLibraries = "LinqToDB.DataProvider.DB2iSeries.dll;IBM.Data.DB2.iSeries.dll"
			//});
		}

		public class DynamicProviderRecord
		{
			public string                      ProviderName            { get; }
			public string                      Description             { get; }
			public string                      ConnectionTypeName      { get; }
			public IReadOnlyCollection<string> Libraries               { get; }
			public string?                     InitializationClassName { get; set; }
			public NamedValue[]?               ProviderNamedValues     { get; set; }
			public string[]?                   AdditionalNamespaces    { get; set; }

			public DynamicProviderRecord(string providerName, string description, string connectionTypeName, params string[] providerLibraries)
			{
				ProviderName       = providerName;
				Description        = description;
				ConnectionTypeName = connectionTypeName;
				Libraries          = providerLibraries ?? Array.Empty<string>();
			}
		}

		public class LoadProviderInfo
		{
			public LoadProviderInfo(DynamicProviderRecord provider)
			{
				Provider = provider;
			}

			public DynamicProviderRecord Provider         { get; }
			public bool                  IsLoaded         { get; private set; }
			public Assembly[]?           LoadedAssemblies { get; private set; }
			public Exception?            LoadException    { get; private set; }

			public void Load(string connectionString)
			{
				if (IsLoaded)
					return;

				if (LoadException != null)
					return;

				try
				{
					IEnumerable<Assembly> loadLibraries = Provider.Libraries
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
						.Where(l => l != null)!;

					var providerLibraries = loadLibraries
						.Concat(new[] { typeof(DataConnection).Assembly })
						.ToArray();


					var typeName = Provider.InitializationClassName;
					if (!string.IsNullOrEmpty(typeName))
					{
						var initType = Type.GetType(typeName, true)!;
						RuntimeHelpers.RunClassConstructor(initType.TypeHandle);
					}

					var provider = ProviderHelper.GetDataProvider(Provider.ProviderName, connectionString);

					var connectionAssemblies = new List<Assembly>() { provider.GetType().Assembly };
					try
					{
						using var connection = provider.CreateConnection(connectionString);
						connectionAssemblies.Add(connection.GetType().Assembly);
					}
					catch
					{
					}

					LoadedAssemblies = connectionAssemblies
						.Concat(providerLibraries)
						.Distinct()
						.ToArray();

					IsLoaded = true;
				}
				catch (Exception e)
				{
					LoadException = e;
					IsLoaded      = false;
				}
			}

			public IEnumerable<string> GetAssemblyLocation(string connectionString)
			{
				Load(connectionString);

				if (LoadedAssemblies != null)
					return LoadedAssemblies.Select(a => a.Location);

				var directory = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);

				if (directory == null)
					return Provider.Libraries;

				return Provider.Libraries.Select(l => Path.Combine(directory, l));
			}

			public string? GetConnectionNamespace()
			{
				var ns = Provider?.ConnectionTypeName?.Split(',').Last().TrimStart();
				if (!string.IsNullOrEmpty(ns))
					return ns!;

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

		public static LoadProviderInfo GetProvider(string? providerName, string? providerPath)
		{
#if NETCORE
			RegisterProviderFactory(providerName, providerPath);
#endif

			if (providerName == null)
				throw new ArgumentNullException(nameof(providerName), $"Provider name missing");

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
				throw new LinqToDBLinqPadException($"Can not activate provider \"{providerName}\"");
			return provider;
		}

#if NETCORE
		static void RegisterProviderFactory(string? providerName, string? providerPath)
		{
			if (!string.IsNullOrWhiteSpace(providerPath))
			{
				switch (providerName)
				{
					case ProviderName.SqlCe:
						RegisterSqlCEFactory(providerPath);
						break;
					case ProviderName.SapHanaNative:
						RegisterSapHanaFactory(providerPath);
						break;
				}
			}
		}

		private static bool _sqlceLoaded;
		static void RegisterSqlCEFactory(string providerPath)
		{
			if (_sqlceLoaded)
				return;

			try
			{
				var assembly = Assembly.LoadFrom(providerPath);
				DbProviderFactories.RegisterFactory("System.Data.SqlServerCe.4.0", assembly.GetType("System.Data.SqlServerCe.SqlCeProviderFactory")!);
				_sqlceLoaded = true;
			}
			catch { }
		}
		
		private static bool _sapHanaLoaded;
		static void RegisterSapHanaFactory(string providerPath)
		{
			if (_sapHanaLoaded)
				return;

			try
			{
				var targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory!, Path.GetFileName(providerPath));
				if (File.Exists(providerPath))
				{
					// original path contains spaces which breaks broken native dlls discovery logic in SAP provider
					// (at least SPS04 040)
					// if you run tests from path with spaces - it will not help you
					File.Copy(providerPath, targetPath, true);
					var sapHanaAssembly = Assembly.LoadFrom(targetPath);
					DbProviderFactories.RegisterFactory("Sap.Data.Hana", sapHanaAssembly.GetType("Sap.Data.Hana.HanaFactory")!);
					_sapHanaLoaded = true;
				}
			}
			catch { }
		}
#endif
	}
}
