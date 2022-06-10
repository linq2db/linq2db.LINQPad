using System.Collections.ObjectModel;
using System.Linq;

namespace LinqToDB.LINQPad
{
	partial class ConnectionViewModel
	{
		public class ProviderInfo
		{
			public ProviderInfo(string name, string description)
			{
				Name        = name;
				Description = description;
			}
			public string Name        { get; }
			public string Description { get; }
		}

		public ConnectionViewModel()
		{
			_providers = new ObservableCollection<ProviderInfo>(
				ProviderHelper.DynamicProviders.Select(p => new ProviderInfo(p.Name, p.Description))
					.OrderBy(s => s.Description.ToLower()));

			_optimizeJoins      = true;
		}
	}
}
