namespace LinqToDB.LINQPad.UI;

internal sealed class SettingsModel
{
	// Don't remove. Design-time .ctor
	public SettingsModel()
		: this(new ConnectionSettings(), false)
	{
	}

	public SettingsModel(ConnectionSettings settings, bool staticConnection)
	{
		StaticConnection  = new StaticConnectionModel (settings, staticConnection );
		DynamicConnection = new DynamicConnectionModel(settings, !staticConnection);
		Scaffold          = new ScaffoldModel         (settings, !staticConnection);
		Schema            = new SchemaModel           (settings, !staticConnection);
		LinqToDB          = new LinqToDBModel         (settings                   );
	}

	public StaticConnectionModel  StaticConnection  { get; }
	public DynamicConnectionModel DynamicConnection { get; }
	public ScaffoldModel          Scaffold          { get; }
	public SchemaModel            Schema            { get; }
	public LinqToDBModel          LinqToDB          { get; }
	public AboutModel             About             => AboutModel.Instance;

	public void Save()
	{
		// save settings that is not saved automatically by tab models
		Schema.Save();
	}
}
