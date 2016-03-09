using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace LinqToDB.LINQPad.Driver
{
	partial class ConnectionViewModel
	{
		public ConnectionViewModel()
		{
			_providers = new ObservableCollection<string>(new []
			{
				ProviderName.Access,
				ProviderName.DB2,
				ProviderName.DB2LUW,
				ProviderName.DB2zOS,
				ProviderName.Firebird,
				ProviderName.Informix,
				ProviderName.SqlServer,
				ProviderName.MySql,
				ProviderName.Oracle,
				ProviderName.PostgreSQL,
				ProviderName.SqlCe,
				ProviderName.SQLite,
				ProviderName.Sybase,
				ProviderName.SapHana,
			}.OrderBy(s => s.ToLower()));
		}
	}
}
