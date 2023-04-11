# LINQ to DB LINQPad 7 Driver

This nuget package is a driver for [LINQPad 7](http://www.linqpad.net). For LINQPad 6 you can use older 4.x version of driver.

Following databases supported:

- **ClickHouse**: using Binary, HTTP and MySQL interfaces
- **DB2** (LUW, z/OS): x64-bit version of LINQPad only
- **Firebird**
- **Informix**: x64-bit version of LINQPad only
- **Microsoft Access**: both OLE DB and ODBC drivers
- **Microsoft SQL Server** 2005+ *(including **Microsoft SQL Azure**)*
- **Microsoft SQL Server Compact (SQL CE)**
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
