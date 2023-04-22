using System.Windows;

namespace LinqToDB.LINQPad.UI;

internal abstract class OptionalTabModelBase : TabModelBase
{
	protected OptionalTabModelBase(ConnectionSettings settings, bool enabled)
		: base(settings)
	{
		Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
	}

	public Visibility Visibility { get; }
}
