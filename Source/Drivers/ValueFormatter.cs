using System.Data.SqlTypes;
using System.Globalization;
using System.Net;
using System.Numerics;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AdoNetCore.AseClient;
using ClickHouse.Client.Numerics;
using FirebirdSql.Data.Types;
using LINQPad;
using Microsoft.SqlServer.Types;
using MySqlConnector;

namespace LinqToDB.LINQPad;

internal static class ValueFormatter
{
	private static readonly object _null = Util.RawHtml(new XElement("span", new XAttribute("style", "text-align:center;"), new XElement("i", new XAttribute("style", "font-style: italic"), "null")));

	// don't use IDatabaseProvider interface as:
	// 1. some providers used by multiple databases
	// 2. user could use those types with any database
	private static readonly IReadOnlyDictionary<Type, Func<object, object>> _typeConverters;
	private static readonly IReadOnlyDictionary<Type, Func<object, object>> _baseTypeConverters;

	static ValueFormatter()
	{
		var typeConverters = new Dictionary<Type, Func<object, object>>();
		var baseTypeConverters = new Dictionary<Type, Func<object, object>>();
		_typeConverters   = typeConverters;
		_baseTypeConverters = baseTypeConverters;

		// generic types
		typeConverters.Add(typeof(BigInteger), ConvertToString);
		typeConverters.Add(typeof(IPAddress), ConvertToString);
		// IPAddress use internal derived type for instances
		baseTypeConverters.Add(typeof(IPAddress), ConvertToString);
		// SQLCE/SQLSERVER types
		typeConverters.Add(typeof(SqlXml), ConvertSqlXml);
		typeConverters.Add(typeof(SqlChars), ConvertSqlChars);
		typeConverters.Add(typeof(SqlBytes), ConvertSqlBytes);
		typeConverters.Add(typeof(SqlBinary), ConvertSqlBinary);
		// ClickHouse.Client
		typeConverters.Add(typeof(ClickHouseDecimal), ConvertToString);
		// Firebird
		typeConverters.Add(typeof(FbZonedTime), ConvertToString);
		typeConverters.Add(typeof(FbZonedDateTime), ConvertToString);
		typeConverters.Add(typeof(FbDecFloat), ConvertFbDecFloat);
		// Sybase ASE
		typeConverters.Add(typeof(AseDecimal), ConvertToString);
		// MySqlConnector
		typeConverters.Add(typeof(MySqlDateTime), ConvertToString);
		typeConverters.Add(typeof(MySqlDecimal), ConvertToString);
		typeConverters.Add(typeof(MySqlGeometry), ConvertMySqlGeometry);
		// sql server spatial types
		typeConverters.Add(typeof(SqlGeography), ConvertToString);
		typeConverters.Add(typeof(SqlGeometry), ConvertToString);
	}

	public static object Format(object value)
	{
		// handle special NULL values
		if (IsNull(value))
			return _null;

		// convert specialized type to simple value (e.g. string)
		var valueType = value.GetType();
		if (_typeConverters.TryGetValue(valueType, out var converter))
			value = converter(value);
		else
			foreach (var type in _baseTypeConverters.Keys)
				if (type.IsAssignableFrom(valueType))
				{
					value = _baseTypeConverters[type](value);
					break;
				}

		// apply simple values formatting
		if (value is string strVal)
			return Format(strVal);

		if (value is char[] chars)
			return Format(chars);

		if (value is byte[] binary)
			return Format(binary);

		return value;
	}

	private static bool IsNull(object? value)
	{
		// note that linqpad will call formatter only for non-primitive values.
		// It will not call it for null and DBNull values so we cannot change their formatting (technically we can do it by formatting owner object, but it doesn't make sense)

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

	private static object Format(byte[] value)
	{
		var sb = new StringBuilder($" Len:{value.Length} ");

		int i;

		for (i = 0; i < value.Length && i < 10; i++)
			sb.Append($"{value[i]:X2}:");

		if (i > 0)
			sb.Length--;

		if (i < value.Length)
			sb.Append("...");

		return Util.RawHtml(new XElement("span", sb.ToString()));
	}

	private static object Format(char chr)
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

	// for types that already implement rendering of all data using ToString
	private static object ConvertToString(object value) => value.ToString()!;
	private static object ConvertSqlXml(object value) => ((SqlXml)value).Value;
	private static object ConvertSqlChars(object value) => ((SqlChars)value).Value;
	private static object ConvertSqlBytes(object value) => ((SqlBytes)value).Value;
	private static object ConvertSqlBinary(object value) => ((SqlBinary)value).Value;

	private static object ConvertFbDecFloat(object value)
	{
		// type reders as {Coefficient}E{Exponent} which is not very noice
		var typedValue = (FbDecFloat)value!;
		var isNegative = typedValue.Coefficient < 0;
		var strValue   = (isNegative ? BigInteger.Negate(typedValue.Coefficient) : typedValue.Coefficient).ToString(CultureInfo.InvariantCulture);

		// semi-localized rendering...
		if (typedValue.Exponent < 0)
		{
			var exp = -typedValue.Exponent;
			if (exp < strValue.Length)
				strValue = strValue.Insert(strValue.Length - exp, CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
			else if (exp == strValue.Length)
				strValue = $"0{CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator}{strValue}";
			else // Exponent > len(Coefficient)
				strValue = $"0{CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator}{new string('0', exp - strValue.Length)}{strValue}";
		}
		else if (typedValue.Exponent > 0)
			strValue = $"{strValue}{new string('0', typedValue.Exponent)}";

		return isNegative ? $"-{strValue}" : strValue;
	}

	private static object ConvertMySqlGeometry(object value)
	{
		var val = (MySqlGeometry)value;
		return new { SRID = val.SRID, WKB = val.Value.Skip(4) };
	}


}
