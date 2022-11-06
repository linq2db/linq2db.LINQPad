namespace LinqToDB.LINQPad.UI;

internal sealed class ScaffoldModel : OptionalTabModelBase
{
	public ScaffoldModel(ConnectionSettings settings, bool enabled)
		: base(settings, enabled)
	{
	}

	public bool Capitalize
	{
		get => Settings.Scaffold.Capitalize;
		set => Settings.Scaffold.Capitalize = value;
	}

	public bool Pluralize
	{
		get => Settings.Scaffold.Pluralize;
		set => Settings.Scaffold.Pluralize = value;
	}

	public bool UseProviderTypes
	{
		get => Settings.Scaffold.UseProviderTypes;
		set => Settings.Scaffold.UseProviderTypes = value;
	}
}
