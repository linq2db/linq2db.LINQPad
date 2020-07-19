using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LINQPad.Extensibility.DataContext;
using LINQPad.Extensibility.DataContext.UI;
using LinqToDB.Data;

namespace LinqToDB.LINQPad
{
	public partial class ConnectionDialog
	{
		public ConnectionDialog()
		{
			InitializeComponent();
		}

		private ConnectionViewModel? Model => DataContext as ConnectionViewModel;

		ConnectionDialog(ConnectionViewModel model)
			: this()
		{
			DataContext = model;

			((INotifyPropertyChanged)model).PropertyChanged += (sender, args) =>
			{
				if (args.PropertyName == nameof(ConnectionViewModel.IncludeRoutines) && model.IncludeRoutines)
				{
					MessageBox.Show(this,
						"Including Stored Procedures may be dangerous in production if the selected database driver does not support CommandBehavior.SchemaOnly option.",
						"Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
				}
			};
		}

		Func<ConnectionViewModel?, Exception?>? _connectionTester;

		public static bool Show(ConnectionViewModel model, Func<ConnectionViewModel?, Exception?>? connectionTester)
		{
			return new ConnectionDialog(model) { _connectionTester = connectionTester }.ShowDialog() == true;
		}

		void TestClick(object sender, RoutedEventArgs e)
		{
			if (_connectionTester != null)
			{
				Exception? ex;

				try
				{
					Mouse.OverrideCursor = Cursors.Wait;
					ex = _connectionTester(Model);
				}
				finally
				{
					Mouse.OverrideCursor = null;
				}


				if (ex == null)
				{
					MessageBox.Show(this, "Successful!", "Connection Test", MessageBoxButton.OK, MessageBoxImage.Information);
				}
				else
				{
					MessageBox.Show(this, ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		}

		void OKClick(object sender, RoutedEventArgs e)
		{
			Exception? ex;

			try
			{
				Mouse.OverrideCursor = Cursors.Wait;
				ex = _connectionTester?.Invoke(Model);
			}
			finally
			{
				Mouse.OverrideCursor = null;
			}

			if (ex == null)
			{
				DialogResult = true;
			}
			else
			{
				if (MessageBox.Show(
					this,
					$"{ex.Message}\r\n\r\nDo you want to continue?",
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
			if (Model == null)
				return;

			var dialog = new Microsoft.Win32.OpenFileDialog
			{
				Title      = "Choose custom assembly",
				DefaultExt = ".dll",
				FileName   = Model.CustomAssemblyPath,
			};

			if (dialog.ShowDialog() == true)
				Model.CustomAssemblyPath = dialog.FileName;
		}

		void ChooseType(object sender, RoutedEventArgs e)
		{
			if (Model != null)
			{
				var oldCursor = Cursor;

				try
				{
					Mouse.OverrideCursor = Cursors.Wait;

					Model.CustomAssemblyPath = Model.CustomAssemblyPath!.Trim();

					var assembly    = DataContextDriver.LoadAssemblySafely(Model.CustomAssemblyPath);
					var customTypes = assembly.GetExportedTypes().Where(IsDataConnection).Select(t => t.FullName).Cast<object>().ToArray();

					Mouse.OverrideCursor = oldCursor;

					var result = (string?)Dialogs.PickFromList("Choose Custom Type", customTypes);

					if (result != null)
						Model.CustomTypeName = result;
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, ex.Message, "Assembly load error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				finally
				{
					Mouse.OverrideCursor = oldCursor;
				}
			}
		}

		bool IsDataConnection(Type type)
		{
			var dcType = typeof(DataConnection);

			Type? currentType = type;
			do
			{
				if (currentType.FullName == dcType.FullName)
					return true;
				currentType = currentType.BaseType;
			} while (currentType != null);

			return false;
		}

		void BrowseAppConfig(object sender, RoutedEventArgs e)
		{
			if (Model != null)
			{
				var dialog = new Microsoft.Win32.OpenFileDialog
				{
					Title      = "Choose application config file",
					DefaultExt = ".config",
					FileName   = Model.AppConfigPath,
				};

				if (dialog.ShowDialog() == true)
					Model.AppConfigPath = dialog.FileName;
			}
		}

		void ChooseConfiguration(object sender, RoutedEventArgs e)
		{
			if (Model != null && !string.IsNullOrWhiteSpace(Model.AppConfigPath))
			{
				var oldCursor = Cursor;

				try
				{
					Mouse.OverrideCursor = Cursors.Wait;

					var config = ConfigurationManager.OpenExeConfiguration(Model.CustomAssemblyPath);

					var configurations = new List<object>();

					for (var i = 0; i < config.ConnectionStrings.ConnectionStrings.Count; i++)
					{
						configurations.Add(config.ConnectionStrings.ConnectionStrings[i].Name);
					}

					Mouse.OverrideCursor = oldCursor;

					var result = (string?)Dialogs.PickFromList("Choose Custom Type", configurations.ToArray());

					if (result != null)
						Model.CustomConfiguration = result;
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, ex.Message, "Config file open error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				finally
				{
					Mouse.OverrideCursor = oldCursor;
				}
			}
		}
	}
}
