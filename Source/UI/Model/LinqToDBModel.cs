namespace LinqToDB.LINQPad.UI;

internal sealed class LinqToDBModel : TabModelBase
{
	public LinqToDBModel(ConnectionSettings settings)
		: base(settings)
	{
	}

	public bool OptimizeJoins
	{
		get => Settings.LinqToDB.OptimizeJoins;
		set => Settings.LinqToDB.OptimizeJoins = value;
	}
}
