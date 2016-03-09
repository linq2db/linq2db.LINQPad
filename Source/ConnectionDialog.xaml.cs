using System;
using System.Windows;

namespace LinqToDB.LINQPad.Driver
{
	public partial class ConnectionDialog : Window
	{
		public ConnectionDialog()
		{
			InitializeComponent();
		}

		LinqToDBDriver _driver;

		public static bool Show(LinqToDBDriver driver, ConnectionViewModel model)
		{
			return new ConnectionDialog { DataContext = model, _driver = driver }.ShowDialog() == true;
		}

		void TestClick(object sender, RoutedEventArgs e)
		{
			if (_driver != null)
			{
				var ex = _driver.TestConnection(DataContext as ConnectionViewModel);

				if (ex == null)
				{
					MessageBox.Show("Successful!", "Connection Test", MessageBoxButton.OK, MessageBoxImage.Information);
				}
				else
				{
					MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		}

		void OKClick(object sender, RoutedEventArgs e)
		{
			if (_driver != null)
			{
				var ex = _driver.TestConnection(DataContext as ConnectionViewModel);

				if (ex == null)
				{
					DialogResult = true;
				}
				else
				{
					if (MessageBox.Show(
						ex.Message + "\r\n\r\nDo you want to continue?",
						"Error",
						MessageBoxButton.YesNo,
						MessageBoxImage.Stop) == MessageBoxResult.Yes)
					{
						DialogResult = true;
					}
				}
			}
		}
	}
}
