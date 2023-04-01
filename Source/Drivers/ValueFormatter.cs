using System.Data.SqlTypes;
using System.Numerics;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using LINQPad;

namespace LinqToDB.LINQPad;

internal static class ValueFormatter
{
	private static readonly object _null = Util.RawHtml(new XElement("span", new XAttribute("style", "text-align:center;"), new XElement("i", new XAttribute("style", "font-style: italic"), "null")));

	public static object? Format(object? value)
	{
		if (IsNull(value))
			return _null;

		// post-formatting
		if (value is string strVal)
			return Format(strVal);

		if (value is char[] chars)
			return Format(chars);

		// no custom formatting
		return value;
	}

	// note that linqpad will call formatter only for non-primitive values.
	// It will not call it for null and DBNull values so we cannot change their formatting (technically we can do it by formatting owner object, but it doesn't make sense)
	private static bool IsNull(object? value)
	{
		// INullable implemented by System.Data.SqlTypes.Sql* types
		return value is INullable nullable && nullable.IsNull;
	}

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

	private static object Format(char[] chars)
	{
		var components = new List<object>();
		var sb = new StringBuilder();

		// encode invalid characters as C# escape sequence
		var first = true;
		foreach (var chr in chars)
		{
			if (first)
				first = false;
			else
				sb.Append(' ');

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
			if (chr <= 255)
				return new XElement("span", new XElement("i", new XAttribute("style", "font-style: italic"), $"\\x{((short)chr):X2}"));
			else
				return new XElement("span", new XElement("i", new XAttribute("style", "font-style: italic"), $"\\u{((short)chr):X4}"));
		else
			return chr.ToString();
	}

	// renderers for shared types (used by more than one provider)

	public static void RegisterSharedRenderers(Dictionary<Type, TypeRenderer> typeRenderers)
	{
		typeRenderers.Add(typeof(BigInteger), RenderToString);
		typeRenderers.Add(typeof(SqlXml)    , RenderSqlXml);
		typeRenderers.Add(typeof(SqlChars)  , RenderSqlChars);
		typeRenderers.Add(typeof(SqlBytes)  , RenderSqlBytes);
		typeRenderers.Add(typeof(SqlBinary) , RenderSqlBinary);
	}

	private static void RenderToString(ref object? value)
	{
		// for types that already implement rendering of all data using ToString
		value = value!.ToString();
	}

	private static void RenderSqlXml(ref object? value)
	{
		var val = (SqlXml)value!;
		if (!val.IsNull)
			value = val.Value;
	}

	private static void RenderSqlChars(ref object? value)
	{
		var val = (SqlChars)value!;
		if (!val.IsNull)
			value = val.Value;
	}

	private static void RenderSqlBytes(ref object? value)
	{
		var val = (SqlBytes)value!;
		if (!val.IsNull)
			value = val.Value;
	}

	private static void RenderSqlBinary(ref object? value)
	{
		var val = (SqlBinary)value!;
		if (!val.IsNull)
			value = val.Value;
	}
}
