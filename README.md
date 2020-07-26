TODO: add azure/nuget badges

# LINQ to DB LINQPad Driver

linq2db.LINQPad is a driver for [LINQPad](http://www.linqpad.net). Supported versions: LINQPad 5 and LINQPad 6.

Following databases supported (by both LINQPad 5 and LINQPad 6 if not noted):

- **DB2** (LUW, z/OS)
- **DB2 iSeries** (using [3rd-party provider](https://github.com/LinqToDB4iSeries/Linq2DB4iSeries)) *(iAccess 7.1+ software must be installed)*. **IMPORTANT:** currently available only for LINQPad 5 using lin2db.LINQPad version 2.9.3 or earlier
- **Firebird**
- **Informix**
- **Microsoft Access** *(supports both OleDb and ODBC)*
- **Microsoft Sql Server** 2000+ *(including **Microsoft Sql Azure**)*
- **Microsoft Sql Server Compact (SqlCe)**
- **MySql/MariaDB**
- **Oracle**
- **PostgreSQL**
- **SQLite**
- **SAP HANA** *(client software must be installed, supports both Native and ODBC providers)*
- **SAP/Sybase ASE**

## Download

Releases are hosted on [Github](https://github.com/linq2db/linq2db.LINQPad/releases) and on [Nuget]_(TODO:LINK) for LINQPad 6 driver.

Latest build is hosted on [Azure Artifacts]_(TODO_LINK).

## Installation

### LINQPad 5 (from file)

- Download latest **.lpx** file from the link provided above.
- Click "Add connection" in LINQPad.
- In the "Choose Data Context" dialog, press the "View more drivers..." button.
- In the "Choose a Driver" dialog, press "Browse..." button.
- Select the downloaded file and click "Open" button.
- In the "Choose Data Context" dialog, select "LINQ to DB" driver and click the next button.
- In the "LINQ to DB connection" dialog, supply your connection information.
- You're done.

### LINQPad 5 (latest version)

- Click "Add connection" in LINQPad.
- In the "Choose Data Context" dialog, press the "View more drivers..." button.
- In the "Choose a Driver" dialog, search for "LINQ to DB Driver".
- Click "Download & Enable Driver" link to install/update to latest driver release
- In the "Choose Data Context" dialog, select "LINQ to DB" driver and click the next button.
- In the "LINQ to DB connection" dialog, supply your connection information.
- You're done.

### LINQPad 6 (Manual)

- Download latest **.lpx6** file from the link provided above.
- Click "Add connection" in LINQPad.
- In the "Choose Data Context" dialog, press the "View more drivers..." button.
- In the "LINQPad NuGet Manager" dialog, press "Install driver from .LPX6 file..." button.
- Select the downloaded file and click "Open" button.
- In the "Choose Data Context" dialog, select "LINQ to DB" driver and click the next button.
- In the "LINQ to DB connection" dialog, supply your connection information.
- You're done.

### LINQPad 6 (NuGet)

- Download latest **.lpx6** file from the link provided above.
- Click "Add connection" in LINQPad.
- In the "Choose Data Context" dialog, press the "View more drivers..." button.
- In the "LINQPad NuGet Manager" dialog, find LINQ To DB driver in list of featured drivers (TODO: review if all correct here after release) and click Install button.
- Close "LINQPad NuGet Manager" dialog
- In the "Choose Data Context" dialog, select "LINQ to DB" driver and click the next button.
- In the "LINQ to DB connection" dialog, supply your connection information.
- You're done.
