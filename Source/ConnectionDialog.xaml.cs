using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Input;

using CodeJam;

using JetBrains.Annotations;

using LinqToDB.Data;

using LINQPad.Extensibility.DataContext;
using LINQPad.Extensibility.DataContext.UI;

namespace LinqToDB.LINQPad
{
	public partial class ConnectionDialog
	{
		public ConnectionDialog()
		{
			InitializeComponent();
		}

		[CanBeNull]
		readonly ConnectionViewModel _model;

		ConnectionDialog([NotNull] ConnectionViewModel model)
			: this()
		{
			DataContext = _model = model;
		}

		Func<ConnectionViewModel,Exception> _connectionTester;

		public static bool Show(ConnectionViewModel model, Func<ConnectionViewModel,Exception> connectionTester)
		{
			return new ConnectionDialog(model) { _connectionTester = connectionTester }.ShowDialog() == true;
		}

		void TestClick(object sender, RoutedEventArgs e)
		{
			if (_connectionTester != null)
			{
				var ex = _connectionTester(DataContext as ConnectionViewModel);

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
			var ex = _connectionTester?.Invoke(DataContext as ConnectionViewModel);

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

		void BrowseAssembly(object sender, RoutedEventArgs e)
		{
			if (_model == null)
				return;

			var dialog = new Microsoft.Win32.OpenFileDialog
			{
				Title      = "Choose custom assembly",
				DefaultExt = ".dll",
				FileName   = _model.CustomAssemblyPath,
			};

			if (dialog.ShowDialog() == true)
				_model.CustomAssemblyPath = dialog.FileName;
		}

		void ChooseType(object sender, RoutedEventArgs e)
		{
			if (_model != null)
			{
				var oldCursor = Cursor;

				try
				{
					Cursor = Cursors.Wait;

					_model.CustomAssemblyPath = _model.CustomAssemblyPath.Trim();

					var assembly    = DataContextDriver.LoadAssemblySafely(_model.CustomAssemblyPath);
					var customTypes = assembly.GetExportedTypes().Where(IsDataConnection).Select(t => t.FullName).ToArray();

					Cursor = oldCursor;

					var result = (string)Dialogs.PickFromList("Choose Custom Type", customTypes);

					if (result != null)
						_model.CustomTypeName = result;
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.Message, "Assembly load error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				finally
				{
					Cursor = oldCursor;
				}
			}
		}

		bool IsDataConnection(Type type)
		{
			var dcType = typeof(DataConnection);

			do
			{
				if (type.FullName == dcType.FullName)
					return true;
				type = type.BaseType;
			} while (type != null);

			return false;
		}

		void BrowseAppConfig(object sender, RoutedEventArgs e)
		{
			if (_model != null)
			{
				var dialog = new Microsoft.Win32.OpenFileDialog
				{
					Title      = "Choose application config file",
					DefaultExt = ".config",
					FileName   = _model.AppConfigPath,
				};

				if (dialog.ShowDialog() == true)
					_model.AppConfigPath = dialog.FileName;
			}
		}

		void ChooseConfiguration(object sender, RoutedEventArgs e)
		{
			if (_model != null && _model.AppConfigPath.NotNullNorWhiteSpace())
			{
				var oldCursor = Cursor;

				try
				{
					Cursor = Cursors.Wait;

					var config = ConfigurationManager.OpenExeConfiguration(_model.CustomAssemblyPath);

					var configurations = new List<object>();

					for (var i = 0; i < config.ConnectionStrings.ConnectionStrings.Count; i++)
					{
						configurations.Add(config.ConnectionStrings.ConnectionStrings[i].Name);
					}

					Cursor = oldCursor;

					var result = (string)Dialogs.PickFromList("Choose Custom Type", configurations.ToArray());

					if (result != null)
						_model.CustomConfiguration = result;
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.Message, "Config file open error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				finally
				{
					Cursor = oldCursor;
				}
			}
		}
	}
}
