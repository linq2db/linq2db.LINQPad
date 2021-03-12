# Release 3.2.0
- codebase switched to C# 9
- [ALL] "Allow multiple queries" option removed from connection configuration screen (has no effect with recent linq2db versions)
- [ALL] fixed issue with typed data context load (https://github.com/linq2db/linq2db.LINQPad/issues/39)
- [LINQPAD5] .net framework version updated from 4.6.0 to 4.6.1
- [LINQPAD6] fixed issue with wrong TFM used for provider assembly
- [ALL] dependency update: linq2db 3.1.4 -> 3.2.3
- [ALL] dependency update: CodeJam 3.2.0 -> 3.3.1
- [ALL] dependency update: AdoNetCore.AseClient 0.18.0 -> 0.19.2
- [ALL] dependency update: FirebirdSql.Data.FirebirdClient 7.5.0 -> 7.10.1
- [ALL] dependency update: MySqlConnector 1.0.1 -> 1.3.1
- [ALL] dependency update: System.Data.SQLite.Core 1.0.113.1 -> 1.0.113.7
- [LINQPAD5] dependency update: System.Runtime.CompilerServices.Unsafe 4.7.1 -> 5.0.0
- [LINQPAD5] dependency update: IBM.Data.DB.Provider 11.5.4000.4 -> 11.5.4000.4861
- [LINQPAD5] dependency update: Oracle.ManagedDataAccess 19.9.0 -> 19.10.1
- [LINQPAD5] dependency update: Npgsql 4.0.11 -> 4.1.8
- [LINQPAD6] dependency update: Microsoft.CodeAnalysis.CSharp 3.7.0 -> 3.9.0
- [LINQPAD6] dependency update: System.Configuration.ConfigurationManager 4.7.0 -> 5.0.0
- [LINQPAD6] dependency update: Oracle.ManagedDataAccess.Core 2.19.90 -> 3.21.1
- [LINQPAD6] dependency update: System.Data.Odbc 4.7.0 -> 5.0.0
- [LINQPAD6] dependency update: System.Data.OleDb 4.7.1 -> 5.0.0
- [LINQPAD6] dependency update: IBM.Data.DB2.Core 3.1.0.300 -> 3.1.0.400
- [LINQPAD6] dependency update: dotMorten.Microsoft.SqlServer.Types 1.2.0 -> 1.3.0
- [LINQPAD6] dependency update: Npgsql 4.1.5 -> 5.0.3


# Release 3.1.0
- [ALL] dependency update: linq2db 3.0.1 -> 3.1.4
- [ALL] dependency update: CodeJam 3.1.0 -> 3.2.0
- [ALL] dependency update: System.Data.SqlClient 4.8.1 -> 4.8.2
- [ALL] dependency update: MySqlConnector 0.69.8 -> 1.0.1
- [ALL] dependency update: IBM.Data.DB.Provider 11.1.4040.4 -> 11.5.4000.4
- [LINQPAD5] dependency update: Oracle.ManagedDataAccess.Core 19.8.0 -> 19.9.0
- [LINQPAD6] bumped target framework netcoreapp3.0 -> netcoreapp3.1
- [LINQPAD6] dependency update: IBM.Data.DB2.Core 1.3.0.100 -> 3.1.0.300
- [LINQPAD6] dependency update: Oracle.ManagedDataAccess.Core 2.19.80 -> 2.19.90
- [LINQPAD6] dependency update: Npgsql 4.1.4 -> 4.1.5
- [LINQPAD6] dependency update: Microsoft.CodeAnalysis.CSharp 3.6.0 -> 3.7.0
- [LINQPAD6] dependency update: dotMorten.Microsoft.SqlServer.Types 1.1.0 -> 1.2.0

# Release 3.0.1
- fixed [nuget package](https://www.nuget.org/packages/linq2db.LINQPad) fo LINQPad 6

# Release 3.0.0

- [linqpad] added support for LINQPad 6 including new [nuget package](https://www.nuget.org/packages/linq2db.LINQPad)  ([#29](https://github.com/linq2db/linq2db.LINQPad/issues/29))
- [tools] driver updated to use recent linq2db 3.0.1 release
- [tools] switched to [Humanizer.Core](https://www.nuget.org/packages/Humanizer.Core) for model pluralization
- [providers] removed ODP.NET Native Oracle provider (we still support the managed provider, which doesn't require client installation)
- [providers] added support for MS Access ODBC provider
- [providers] replaced MySql.Data provider with [MySqlConnector](https://www.nuget.org/packages/MySqlConnector) provider
- [providers] replaced native SAP/Sybase ASE provider with managed [AdoNetCore.AseClient](https://www.nuget.org/packages/AdoNetCore.AseClient) provider
- [providers] removed IBM DB2 iSeries support temporarily because required [provider](https://github.com/LinqToDB4iSeries/Linq2DB4iSeries) lacks linq2db v3 support
- [providers] added SAP HANA ODBC provider
- [providers] IBM Informix provider switched to use [IBM.Data.DB](https://www.nuget.org/packages/IBM.Data.DB2.Core/) IDS provider (SQLI provider could be added on request)
- [providers] there is no need to pre-install client for IBM Informix anymore
- [providers] there is no need to pre-install client for IBM DB LUW/zOS anymore
- [providers] there is no need to pre-install client for SAP/Sybase ASE anymore ([#33](https://github.com/linq2db/linq2db.LINQPad/issues/33))
- [providers] fixed multiple issues with provider-specific types support
- [providers] added support for array and dictionary types (e.g. PostgreSQL array and hstore types)
- [ui] fixed database tree to not create duplicate/unnecessary schema nodes
- [ui] fixed connection dialog aligment issues
- [ui] tables without columns not shown anymore (e.g. Access system tables without read permissions)
- [ui] added provider path setting for SqlCe and SAP HANA Native providers for LINQPad 6 version
- [ui] fixed non-xml characters rendering in custom formatter
- [ui] always use custom formatters for provider-specific types
- [misc] added license file (MIT) to repository
- [misc] copyrights updated to include major contributors
- [misc] migrated to Azure Pipelines from Appveyor for builds
- [misc] codebase updated to C# 8.0 with nullable reference types
