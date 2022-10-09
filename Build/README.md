# LINQ to DB LINQPad 6 and 7 Driver

This nuget package is a driver for [LINQPad 6 and 7](http://www.linqpad.net).

Following databases supported:

- **ClickHouse** (Binary, HTTP and MySQL interfaces)
- **DB2** (LUW, z/OS) (only 64-bit version)
- **Firebird**
- **Informix** (only 64-bit version)
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

## Installation

- Click "Add connection" in LINQPad.
- In the "Choose Data Context" dialog, press the "View more drivers..." button.
- In the "LINQPad NuGet Manager" dialog, find LINQ To DB driver in list of drivers and click the "Install" button.
- Close "LINQPad NuGet Manager" dialog
- In the "Choose Data Context" dialog, select the "LINQ to DB" driver and click the "Next" button.
- In the "LINQ to DB connection" dialog, supply your connection information.
- You're done.
