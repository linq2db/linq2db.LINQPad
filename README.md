# LINQ to DB LINQPad Driver

[![NuGet Version and Downloads count](https://buildstats.info/nuget/linq2db.LINQPad?includePreReleases=true)](https://www.nuget.org/packages/linq2db.LINQPad) [![License](https://img.shields.io/github/license/linq2db/linq2db.LINQPad)](MIT-LICENSE.txt)

[![Master branch build](https://img.shields.io/azure-devops/build/linq2db/linq2db/8/master?label=build%20(master))](https://dev.azure.com/linq2db/linq2db/_build?definitionId=8&_a=summary) [![Latest build](https://img.shields.io/azure-devops/build/linq2db/linq2db/8?label=build%20(latest))](https://dev.azure.com/linq2db/linq2db/_build?definitionId=8&_a=summary)

linq2db.LINQPad is a driver for [LINQPad 5 (.NET Framework)](http://www.linqpad.net) and [LINQPad 6-7 (.NET/Core)](http://www.linqpad.net).

Following databases supported (by all LINQPad versions if other not noted):

- **ClickHouse**: Binary (LINQPad 6+), HTTP and MySQL interfaces
- **DB2** (LUW, z/OS) (LINQPad 6+ supports only 64-bit version)
- **DB2 iSeries** (using [3rd-party provider](https://github.com/LinqToDB4iSeries/Linq2DB4iSeries)) *(iAccess 7.1+ software must be installed)*. **IMPORTANT:** currently available only for LINQPad 5 using linq2db.LINQPad version 2.9.3 or earlier
- **Firebird**
- **Informix** (LINQPad 6+ supports only 64-bit version)
- **Microsoft Access** *(supports both OleDb and ODBC)*
- **Microsoft Sql Server** 2005+ *(including **Microsoft Sql Azure**)*
- **Microsoft Sql Server Compact (SqlCe)**
- **MariaDB**
- **MySql**
- **Oracle**
- **PostgreSQL**
- **SAP HANA** *(client software must be installed, supports both Native and ODBC providers)*
- **SAP/Sybase ASE**
- **SQLite**

## Download

Releases are hosted on [Github](https://github.com/linq2db/linq2db.LINQPad/releases) and on [Nuget](https://www.nuget.org/packages/linq2db.LINQPad) for LINQPad 6+ driver.

Latest build is hosted on [Azure Artifacts](https://dev.azure.com/linq2db/linq2db/_packaging?_a=package&feed=linq2db%40Local&package=linq2db.LINQPad&protocolType=NuGet). Feed [URL](https://pkgs.dev.azure.com/linq2db/linq2db/_packaging/linq2db/nuget/v3/index.json) ([how to use](https://docs.microsoft.com/en-us/nuget/consume-packages/install-use-packages-visual-studio#package-sources)).

## Installation

### LINQPad 6+ (NuGet)

- Click "Add connection" in LINQPad.
- In the "Choose Data Context" dialog, press the "View more drivers..." button.
- In the "LINQPad NuGet Manager" dialog, find LINQ To DB driver in list of drivers and click the "Install" button.
- Close "LINQPad NuGet Manager" dialog
- In the "Choose Data Context" dialog, select the "LINQ to DB" driver and click the "Next" button.
- In the "LINQ to DB connection" dialog, supply your connection information.
- You're done.

### LINQPad 6+ (Manual)

- Download latest **.lpx6** file from the link provided above.
- Click "Add connection" in LINQPad.
- In the "Choose Data Context" dialog, press the "View more drivers..." button.
- In the "LINQPad NuGet Manager" dialog, press the "Install driver from .LPX6 file..." button.
- Select the downloaded file and click the "Open" button.
- In the "Choose Data Context" dialog, select the "LINQ to DB" driver and click the "Next" button.
- In the "LINQ to DB connection" dialog, supply your connection information.
- You're done.

### LINQPad 5 (Choose a driver)

- Click "Add connection" in LINQPad.
- In the "Choose Data Context" dialog, press the "View more drivers..." button.
- In the "Choose a Driver" dialog, search for "LINQ to DB Driver".
- Click "Download & Enable Driver" link to install/update to latest driver release
- In the "Choose Data Context" dialog, select the "LINQ to DB" driver and click the "Next" button.
- In the "LINQ to DB connection" dialog, supply your connection information.
- You're done.

### LINQPad 5 (Manual)

- Download latest **.lpx** file from the link provided above.
- Click "Add connection" in LINQPad.
- In the "Choose Data Context" dialog, press the "View more drivers..." button.
- In the "Choose a Driver" dialog, press the "Browse..." button.
- Select the downloaded file and click the "Open" button.
- In the "Choose Data Context" dialog, select the "LINQ to DB" driver and click the "Next" button.
- In the "LINQ to DB connection" dialog, supply your connection information.
- You're done.
