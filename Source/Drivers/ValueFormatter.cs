using System.Numerics;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using LINQPad;

namespace LinqToDB.LINQPad;

internal static class ValueFormatter
{
	//private static readonly object _null = Util.RawHtml(new XElement("span", new XAttribute("style", "text-align:center;"), new XElement("i", new XAttribute("style", "font-style: italic"), "gnull")));

	public static object? Format(object? value)
	{
		//if (IsNull(value))
		//	return Util.RawHtml(new XElement("span", new XAttribute("style", "text-align:center;"), new XElement("i", new XAttribute("style", "font-style: italic"), "xnull")));

		if (value is BigInteger bi)
			value = bi.ToString();

		if (value is string strVal)
			return Format(strVal);

		// no custom formatting
		return value;
	}

	// note that linqpad will call formatter only for non-primitive values.
	// It will not call it for null and DBNull values so we cannot change their formatting (technically we can do it by formatting owner object, but it doesn't make sense)
	//private static bool IsNull(object? value)
	//{
	//	return value is null or DBNull;
	//}

	private static object Format(string str)
	{
		var components = new List<object>();
		var sb = new StringBuilder();

		// encode invalid characters as C# escape sequence
		foreach (var chr in str)
		{
			var formattedChar = Format(chr);
			if (formattedChar is string chrStr)
				sb.Append(chrStr);
			else
			{
				if (sb.Length > 0)
				{
					components.Add(sb.ToString());
					sb.Clear();
				}

				components.Add(formattedChar);
			}
		}

		if (sb.Length > 0)
			components.Add(sb.ToString());

		return Util.RawHtml(new XElement("span", components.ToArray()));
	}

	static object Format(char chr)
	{
		if (!XmlConvert.IsXmlChar(chr) && !char.IsHighSurrogate(chr) && !char.IsLowSurrogate(chr))
			return new XElement("span", new XElement("i", new XAttribute("style", "font-style: italic"), $"\\u{((short)chr):X4}"));
		else
			return chr.ToString();
	}
}
