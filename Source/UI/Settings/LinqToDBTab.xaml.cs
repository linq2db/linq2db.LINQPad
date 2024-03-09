﻿using System.Diagnostics;
using System.Windows.Navigation;

namespace LinqToDB.LINQPad.UI;

#pragma warning disable CA1812 // Remove unused type
internal sealed partial class LinqToDBTab
#pragma warning restore CA1812 // Remove unused type
{
	public LinqToDBTab()
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
