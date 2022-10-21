using System.Collections.ObjectModel;

namespace LinqToDB.LINQPad;

sealed partial class ConnectionViewModel
{
	public ConnectionViewModel()
	{
		_providers = new ObservableCollection<IDatabaseProvider>(DatabaseProviders.Providers.Values.OrderBy(p => p.Description.ToLowerInvariant()));
	}
}
