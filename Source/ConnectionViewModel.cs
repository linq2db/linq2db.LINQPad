using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace LinqToDB.LINQPad
{
	partial class ConnectionViewModel
	{
		public class ProviderInfo
		{
			public string Name        { get; set; }
			public string Description { get; set; }
		}

		public ConnectionViewModel()
		{
			_providers = new ObservableCollection<ProviderInfo>(new []
			{
				new ProviderInfo { Name = ProviderName.Access,        Description = "Microsoft Access", },
				new ProviderInfo { Name = ProviderName.Firebird,      Description = "Firebird", },
				new ProviderInfo { Name = ProviderName.SqlServer,     Description = "Microsoft SQL Server", },
				new ProviderInfo { Name = ProviderName.MySql,         Description = "MySql", },
				//new ProviderInfo { Name = ProviderName.OracleNative,  Description = "Oracle ODP.NET", },
				new ProviderInfo { Name = ProviderName.OracleManaged, Description = "Oracle Managed Driver", },
				new ProviderInfo { Name = ProviderName.PostgreSQL,    Description = "PostgreSQL", },
				new ProviderInfo { Name = ProviderName.SqlCe,         Description = "Microsoft SQL Server Compact", },
				new ProviderInfo { Name = ProviderName.SQLite,        Description = "SQLite", },
				new ProviderInfo { Name = ProviderName.Sybase,        Description = "SAP Sybase ASE", },
				new ProviderInfo { Name = ProviderName.SapHana,       Description = "SAP HANA", },
			}.OrderBy(s => s.Description.ToLower()));

			if (IntPtr.Size == 4)
			{
				_providers.Add(new ProviderInfo { Name = ProviderName.DB2LUW, Description = "DB2 for Linux, UNIX and Windows", });
				_providers.Add(new ProviderInfo { Name = ProviderName.DB2zOS, Description = "DB2 for z/OS", });
				_providers.Add(new ProviderInfo { Name = ProviderName.Informix, Description = "IBM Informix", });
			}

			_allowMultipleQuery = true;
			_optimizeJoins      = true; 
		}
	}
}
