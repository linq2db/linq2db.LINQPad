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
			_providers = new ObservableCollection<ProviderInfo>(
				ProviderHelper.DynamicProviders.Select(p => new ProviderInfo { Name = p.ProviderName, Description = p.Description })
					.OrderBy(s => s.Description.ToLower()));

			_allowMultipleQuery = true;
			_optimizeJoins      = true;
		}
	}
}
