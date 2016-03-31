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

---

Releases are hosted on [Github](https://github.com/linq2db/linq2db.LINQPad/releases).
Latest build is hosted on [AppVeyor](https://ci.appveyor.com/project/igor-tkachev/linq2db-linqpad/build/artifacts).

---

Installation:

1. Download latest **.lpx** file from the link provided above.
2. Click "Add connection" in LINQPad.
3. In the "Choose Data Context" dialog, press the "View more drivers..." button.
4. In the "Choose a Driver" dialog, press "Browse..." button.
5. Select the downloaded "linq2db.LINQPad.lpx" and click "Open" button.
6. In the "Choose Data Context" dialog, select "LINQ to DB" driver and click the next button.
7. In the "LINQ to DB connection" dialog, supply your connection information.
8. You're done.
