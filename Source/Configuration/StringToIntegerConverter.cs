using System.Globalization;
using System.Windows.Data;

namespace LinqToDB.LINQPad;

sealed class StringToIntegerConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value != null)
			return int.Parse(value.ToString()!, culture);

		return 0;
	}

	public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value != null)
			return string.Format(culture, "{0}", value);

		return null;
	}
}
