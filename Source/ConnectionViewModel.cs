using System.Collections.ObjectModel;

namespace LinqToDB.LINQPad;

sealed partial class ConnectionViewModel
{
	public sealed class ProviderInfo
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
