namespace LinqToDB.LINQPad.UI
{
	internal abstract class TabModelBase
	{
		protected readonly ConnectionSettings Settings;

		protected TabModelBase(ConnectionSettings settings)
		{
			Settings = settings;
		}
	}
}
