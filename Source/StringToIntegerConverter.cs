using System;
using System.Globalization;
using System.Windows.Data;

namespace LinqToDB.LINQPad
{
	public class StringToIntegerConverter : IValueConverter
	{
		public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value != null)
				return int.Parse(value.ToString()!);

			return 0;
		}

		public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value != null)
				return value.ToString();

			return null;
		}
	}
}
