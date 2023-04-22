using System.Diagnostics;
using System.Windows.Navigation;

namespace LinqToDB.LINQPad.UI;

internal sealed partial class AboutTab
{
	public AboutTab()
	{
		InitializeComponent();
	}

	private void Url_Click(object sender, RequestNavigateEventArgs e)
	{
		Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
		{
			UseShellExecute = true
		});

		e.Handled = true;
	}
}
