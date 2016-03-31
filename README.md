# LINQ to DB LINQPad Driver [![build status](https://ci.appveyor.com/api/projects/status/github/linq2db/linq2db.LINQPad)](https://ci.appveyor.com/project/igor-tkachev/linq2db-linqpad)

linq2db.LINQPad is a driver for [LINQPad](http://www.linqpad.net) that supports the following databases:

- **DB2** (LUW, z/OS) *(client software must be installed)*
- **Firebird**
- **Informix** *(client software must be installed)*
- **Microsoft Access**
- **Microsoft Sql Azure**
- **Microsoft Sql Server** 2000+
- **Microsoft SqlCe**
- **MySql**
- **Oracle** *(client software must be installed if native driver is used)*
- **PostgreSQL**
- **SQLite**
- **SAP HANA** *(client software must be installed)*
- **Sybase ASE**

## Download

Releases are hosted on [Github](https://github.com/linq2db/linq2db.LINQPad/releases).

Latest build is hosted on [AppVeyor](https://ci.appveyor.com/project/igor-tkachev/linq2db-linqpad/build/artifacts).

## Installation:

- Download latest **.lpx** file from the link provided above.
- Click "Add connection" in LINQPad.
- In the "Choose Data Context" dialog, press the "View more drivers..." button.
- In the "Choose a Driver" dialog, press "Browse..." button.
- Select the downloaded "linq2db.LINQPad.lpx" and click "Open" button.
- In the "Choose Data Context" dialog, select "LINQ to DB" driver and click the next button.
- In the "LINQ to DB connection" dialog, supply your connection information.
- You're done.

## Project Build Status

[![Build status](https://ci.appveyor.com/api/projects/status/gn51dtu4378xnte2/branch/master?svg=true)](https://ci.appveyor.com/project/igor-tkachev/linq2db-linqpad/branch/master)
