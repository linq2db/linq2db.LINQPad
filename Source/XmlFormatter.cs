using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using LINQPad;
using LinqToDB.Extensions;
using LinqToDB.Mapping;

namespace LinqToDB.LINQPad
{
	// TODO: this class needs refactoring...
	static class XmlFormatter
	{
		class Total
		{
			public object?            Value;
			public Func<int,object?>? GetAverage;
			public bool               IsNumber;

			public void Add<T>(Func<T,T> add, Func<T,Func<int,object?>> avr)
			{
				if (Value == null)
				{
					Value    = add(default!);
					IsNumber = true;
				}
				else
				{
					Value = add((T)Value);
				}

				GetAverage = avr((T)Value!);
			}
		}

		static int _id;

		public static object? Format(MappingSchema mappingSchema, object? objectToWrite)
		{
			if (IsNull(objectToWrite))
				return null;

			if (objectToWrite is string || objectToWrite is XElement)
				return objectToWrite;

			if (objectToWrite is SqlDecimal value)
			{
				return Util.RawHtml($"<div class=\"n\">{value}</div>");
			}

			var type = objectToWrite.GetType();

			if (objectToWrite is IEnumerable enumerable)
			{
				var itemType = type.GetItemType();
				var items    = enumerable.Cast<object>().ToList();
				var tableID  = ++_id;

				var columns = mappingSchema.IsScalarType(itemType) ?
					new[]
					{
						new
						{
							MemberType = itemType,
							MemberName = "",
							GetValue = (Func<MappingSchema,object,object?>)((ms, v) => v),
							Total    = new Total(),
						}
					}
					:
					mappingSchema.GetEntityDescriptor(itemType).Columns
						.Select(c => new
						{
							c.MemberType,
							c.MemberName,
							GetValue = (Func<MappingSchema,object,object?>)((ms, v) => c.GetValue(v)),
							Total    = new Total(),
						})
						.ToArray();

				return Util.RawHtml(
					new XElement("div",
						new XAttribute("class", "spacer"),
						new XElement("table",
							new object[]
							{
								new XAttribute("id", $"t{tableID}"),
								new XElement("tr",
									new XElement("td",
										new XAttribute("class",   "typeheader"),
										new XAttribute("colspan", columns.Length),
										new XElement("a",
											new XAttribute("href",  ""),
											new XAttribute("class", "typeheader"),
											new XAttribute("onclick", $"return toggle('t{tableID}');"),
											new XElement("span",
												new XAttribute("class", "typeglyph"),
												new XAttribute("id",    $"t{tableID}ud"),
												5),
											$"{GetTypeName(itemType)} ({items.Count} items)"),
										new XElement("a",
											new XAttribute("href",  ""),
											new XAttribute("class", "extenser"),
											new XAttribute("onclick", "return window.external.CustomClick('0',false);"),
											new XElement("span",
												new XAttribute("class", "extenser"),
												4)))),
								new XElement("tr",
									columns.Select(c =>
										new XElement("th",
											new XAttribute("title", GetTypeName(c.MemberType)),
											$"{c.MemberName}")))
							}
							.Union(items
								.Select(i => new XElement("tr", columns.Select(c => FormatValue(c.Total, c.GetValue(mappingSchema, i)))))
								.ToList())
							.Union(
								new object[]
								{
									new XElement("tr", columns.Select(c =>
										new XElement("td",
											new XAttribute("title", c.Total.Value == null ? "Totals" : $"Total={c.Total.Value}\r\nAverage={c.Total.GetAverage!(items.Count)}"),
											new XAttribute("class", "columntotal"),
											new XAttribute("style", "font-size:100%;"),
											c.Total.Value))),
								}
								.Where(_ => columns.Any(c => c.Total.Value != null))
								))));
			}

			return objectToWrite;
		}

		static bool IsAnonymousType(Type type)
		{
			return
				Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false) &&
				type.IsGenericType && type.Name.Contains("AnonymousType")            &&
				(type.Name.StartsWith("<>") || type.Name.StartsWith("VB$"))          &&
				(type.Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic;
		}

		static string GetTypeName(Type type)
		{
			if (type.IsNullable())
				return type.ToNullableUnderlying().Name + "?";

			if (IsAnonymousType(type))
				return "new";
			
			return type.Name;
		}

		static bool DynamicCheckForNull(Type? baseTypeType, Type type, object value, ref bool isNull, string isNullProperty = "IsNull")
		{
			if (baseTypeType == null || !baseTypeType.IsSameOrParentOf(type))
				return false;

			var prop = baseTypeType.GetProperty(isNullProperty);
			if (prop == null)
				return false;

			isNull = (bool) prop.GetValue(value)!;
			return true;
		}

		static bool DynamicCheckForNull(Type type, object value, ref bool isNull)
		{
			return 
				DynamicCheckForNull(type.Assembly.GetType("IBM.Data.DB2Types.INullable", false), type, value, ref isNull);
		}

		static bool IsNull([NotNullWhen(false)] object? value)
		{
			if (value == null || value is DBNull)
				return true;

			if (value is INullable nullable)
				return nullable.IsNull;

			var isNull = false;
			if (DynamicCheckForNull(value.GetType(), value, ref isNull))
				return isNull;
			return false;
		}

		static NumberFormatter GenerateNumberFormatter<T>(Type valueType, bool checkForNull = true, Func<ParameterExpression, Expression>? convertFunc = null)
		{
			// value => v => (v.IsNull ? 0 : v) + value
			var innerType  = typeof(T);
			var paramValue = Expression.Parameter(innerType);
			var paramV     = Expression.Parameter(valueType);
			var addLamba =
				Expression.Lambda(
					Expression.Lambda(
						Expression.Add(
							checkForNull
								? (Expression) Expression.Condition(
									Expression.PropertyOrField(paramV, "IsNull"),
									Expression.Constant(default(T), innerType),
									Expression.Convert(convertFunc != null ? convertFunc(paramV) : paramV, innerType))
								: paramV,
							paramValue
						), paramValue
					), paramV);

			// average
			// v => n => v / n

			var paramNumber = Expression.Parameter(typeof(int));
			var avgLambda =
				Expression.Lambda(
					Expression.Lambda(
						Expression.Convert(Expression.Divide(paramValue, Expression.Convert(paramNumber, innerType)), typeof(object)),
						paramNumber
					), paramValue
				);

			// format
			// v => new XElement("span", v)
			var constructor = typeof(XElement).GetConstructor(new []{typeof(XName), typeof(object) });

			var formatExpr = Expression.Lambda(
				Expression.New(
					constructor ?? throw new InvalidOperationException(),
					Expression.Convert(Expression.Constant("span"), typeof(XName)),
					Expression.Convert(Expression.Convert(convertFunc != null ? convertFunc(paramV) : paramV, innerType), typeof(object))),
				paramV);

			var formatterType = typeof(NumberFormatter<,>).MakeGenericType(valueType, innerType);
			var formatter     = (NumberFormatter)Activator.CreateInstance(formatterType, addLamba.Compile(), avgLambda.Compile(), formatExpr.Compile())!;

			return formatter;
		}

		static ValueFormatter? GetValueFormatter(Type type)
		{
			var vf = _valueFormatters.GetOrAdd(type, t =>
			{
				switch (type.FullName)
				{
					case "IBM.Data.DB2Types.DB2Time":
						return GenerateValueFormatter<TimeSpan>(type, dt => Expression.PropertyOrField(dt, "Value"));
					case "IBM.Data.DB2Types.DB2RowId": // z/OS type
					case "IBM.Data.DB2Types.DB2Binary":
						return GenerateValueFormatter<byte[]>(type, dt => Expression.PropertyOrField(dt, "Value"));
					case "IBM.Data.DB2Types.DB2String":
						return GenerateValueFormatter<string>(type, dt => Expression.PropertyOrField(dt, "Value"));
					case "IBM.Data.DB2Types.DB2TimeStamp":
						// to avoid "This value of the DB2Type will be truncated." when taking Value
						return GenerateValueFormatter<string>(type, v => Expression.Call(v, "ToString", Array.Empty<Type>()));
					case "IBM.Data.DB2Types.DB2Date":
					case "IBM.Data.DB2Types.DB2DateTime": // z/OS and Informix type
						return GenerateValueFormatter<DateTime>(type, dt => Expression.PropertyOrField(dt, "Value"));
					case "MySql.Data.Types.MySqlDateTime":
						return GenerateValueFormatter<object>(type, dt => Expression.Condition(Expression.PropertyOrField(dt, "IsValidDateTime"), Expression.Convert(Expression.Call(dt, "GetDateTime", Array.Empty<Type>()), typeof(object)), Expression.Constant("invalid", typeof(object))));
				}

				return null;
			});

			return vf;
		}

		static NumberFormatter? GetNumberFormatter(Type type)
		{
			var nf = _numberFormatters.GetOrAdd(type, t =>
			{

				try
				{
					switch (type.FullName)
					{
						case "IBM.Data.DB2Types.DB2Int16":
						case "IBM.Data.DB2Types.DB2Int32":
						case "IBM.Data.DB2Types.DB2Int64":
							return GenerateNumberFormatter<long>(type, true, p => Expression.PropertyOrField(p, "Value"));

						case "IBM.Data.DB2Types.DB2Decimal":
						case "IBM.Data.DB2Types.DB2DecimalFloat":
							return GenerateNumberFormatter<decimal>(type, true, p => Expression.PropertyOrField(p, "Value"));

						case "IBM.Data.DB2Types.DB2Double":
						case "IBM.Data.DB2Types.DB2Real":
							return GenerateNumberFormatter<double>(type, true, p => Expression.PropertyOrField(p, "Value"));
					}
				}
				catch (Exception)
				{
					// ignored
				}

				return null;
			});

			return nf;
		}

		static XElement FormatValue(Total total, object? value)
		{
			try
			{
				if (IsNull(value))
					return new XElement("td", new XAttribute("style", "text-align:center;"), new XElement("i", new XAttribute("style", "font-style: italic"), "null"));

				var type = value.GetType();

				// multi-dimensional arrays not supported for now (and we don't expose them in schema)
				if (type.IsArray && !(value is byte[]))
					return new XElement("td", FormatArray((Array)value));

				if (value is IDictionary dict)
					return new XElement("td", FormatDictionary(dict));

				var nf = GetNumberFormatter(type);

				if (nf != null)
				{
					nf.AddTotal(total, value);
					return new XElement("td", new XAttribute("class", "n"), value);
				}

				var vf = GetValueFormatter(type);
				if (vf != null)
				{
					var list = new List<object>();

					if (vf.NoWrap)
						list.Add(new XAttribute("nowrap", "nowrap"));

					var style = "";

					if (vf.Font != null) style += $"font-family:{vf.Font};";
					if (vf.Size != null) style += $"font-size:{vf.Size};";

					if (style.Length > 0)
						list.Add(new XAttribute("style", style));

					list.Add(vf.FormatValue(value));

					return new XElement("td", list.ToArray());
				}

				return new XElement("td", value.ToString());
			}
			catch (Exception ex)
			{
				return new XElement("td",
					new XAttribute("style", "color:red"),
					ex.Message);
			}
		}

		static XElement FormatArray(Array array)
		{
			var fragments = new List<object>
			{
				$" Len:{array.Length} ["
			};

			int i;

			for (i = 0; i < array.Length && i < 10; i++)
			{
				if (i > 0)
					fragments.Add(", ");

				fragments.Add(FormatValueXml(array.GetValue(i)));
			}

			if (i < array.Length)
				fragments.Add("...");

			fragments.Add("]");

			return new XElement("span", fragments.ToArray());
		}

		static XElement FormatDictionary(IDictionary dict)
		{
			var fragments = new List<object>
			{
				$" Size:{dict.Count} {{"
			};

			int i = 0;

			foreach (var key in dict.Keys)
			{
				if (i > 0)
					fragments.Add(", ");

				fragments.Add("{");
				fragments.Add(FormatValueXml(key));
				fragments.Add(", ");
				fragments.Add(FormatValueXml(dict[key!]));
				fragments.Add("}");

				i++;

				if (i == 10)
					break;
			}

			if (i < dict.Count)
				fragments.Add("...");

			fragments.Add("}");

			return new XElement("span", fragments.ToArray());
		}

		public static object FormatValue(object? value)
		{
			return Util.RawHtml(FormatValueXml(value));
		}

		private static XElement FormatValueXml(object? value)
		{
			if (IsNull(value))
				return new XElement("span", new XAttribute("style", "text-align:center;"), new XElement("i", new XAttribute("style", "font-style: italic"), "null"));

			if (_numberFormatters.TryGetValue(value.GetType(), out var nf) && nf != null)
				return nf.GetElement(value);

			if (_valueFormatters.TryGetValue(value.GetType(), out var vf) && vf != null)
			{
				var style = "";

				if (vf.NoWrap) style += "white-space:nowrap;";
				if (vf.Font != null) style += $"font-family:{vf.Font};";
				if (vf.Size != null) style += $"font-size:{vf.Size};";

				return new XElement("span",
					new XAttribute("style", style),
					vf.FormatValue(value));
			}

			return new XElement("span", value);
		}

		static string Format(DateTime dt)
		{
			return dt.Hour == 0 && dt.Minute == 0 && dt.Second == 0
				? dt.ToString("yyyy-MM-dd")
				: dt.ToString("yyyy-MM-dd HH:mm:ss");
		}

		// used by reflection
		static string Format(TimeSpan ts)
		{
			return ts.ToString("c");
		}

		static object Format(object val)
		{
			if (val is string strVal)  return Format(strVal);
			if (val is DateTime dtVal) return Format(dtVal);
			
			throw new InvalidOperationException($"Unsupported value type: {val.GetType()}");
		}

		static object Format(BitArray value)
		{
			var sb = new StringBuilder($" Len:{value.Length} 0b");

			int i;

			for (i = 0; i < value.Length && i < 64; i++)
				sb.Append(value[i] ? '1' : '0');

			if (i < value.Length)
				sb.Append("...");

			return sb.ToString();
		}

		static object Format(char chr)
		{
			if (!XmlConvert.IsXmlChar(chr) && !char.IsHighSurrogate(chr) && !char.IsLowSurrogate(chr))
				return new XElement("span", new XElement("i", new XAttribute("style", "font-style: italic"), $"\\u{((short)chr):X4}"));
			else
				return chr.ToString();
		}

		static object Format(string str)
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

			return new XElement("span", components.ToArray());
		}

		static string Format(byte[] value)
		{
			var sb = new StringBuilder($" Len:{value.Length} ");

			int i;

			for (i = 0; i < value.Length && i < 10; i++)
				sb.Append($"{value[i]:X2}:");

			if (i > 0)
				sb.Length--;

			if (i < value.Length)
				sb.Append("...");

			return sb.ToString();
		}

		static ValueFormatter GenerateValueFormatter<T>(Type type, Func<ParameterExpression, Expression> dataExtractor, bool nowrap = true)
		{
			var param = Expression.Parameter(type);

			var extractedValue = dataExtractor(param);

			var methodInfo = typeof(XmlFormatter).GetMethodEx("Format", typeof(T));

			var expr = Expression.Lambda(
				Expression.Call(methodInfo, extractedValue),
				param);

			var formatterType = typeof(ValueFormatter<>).MakeGenericType(type);
			var formatter = (ValueFormatter)Activator.CreateInstance(formatterType, expr.Compile(), nowrap)!;

			return formatter;
		}

		static readonly ConcurrentDictionary<Type,ValueFormatter?> _valueFormatters = new ConcurrentDictionary<Type, ValueFormatter?>(new[]
		{
			VF<char>           (      Format),
			VF<string>         (      Format),
			VF<SqlString>      (v =>  Format(v.Value)),
			VF<SqlBoolean>     (v =>  Format(v.Value.ToString())),
			VF<DateTime>       (      Format),
			VF<SqlDateTime>    (dt => Format(dt.Value)),
			VF<byte[]>         (Format),
			VF<SqlBinary>      (v =>  Format(v.Value)),
			VF<Guid>           (v => v.      ToString("B").ToUpper(), font:"consolas", size:"110%"),
			VF<SqlGuid>        (v => v.Value.ToString("B").ToUpper(), font:"consolas", size:"110%"),
			VF<TimeSpan>       (v => v.ToString()),
			VF<SqlXml>         (v => v.Value),

			VF<BitArray>       (v => Format(v)),
			VF<IPAddress>      (v => Format(v.ToString())),
			VF<PhysicalAddress>(v => Format(v.GetAddressBytes())),

			// sql server types
			VF<Microsoft.SqlServer.Types.SqlHierarchyId>(v => Format(v.ToString())),
			VF<Microsoft.SqlServer.Types.SqlGeography  >(v => Format(v.ToString())),
			VF<Microsoft.SqlServer.Types.SqlGeometry   >(v => Format(v.ToString())),

			// npgsql types
			VF<NpgsqlTypes.NpgsqlTimeSpan>(v => Format((TimeSpan)v)),
			VF<NpgsqlTypes.NpgsqlDateTime>(v => Format((DateTime)v)),
			VF<NpgsqlTypes.NpgsqlDate    >(v => Format((DateTime)v)),
			VF<NpgsqlTypes.NpgsqlBox     >(v => Format(v.ToString())),
			VF<NpgsqlTypes.NpgsqlLSeg    >(v => Format(v.ToString())),
			VF<NpgsqlTypes.NpgsqlLine    >(v => Format(v.ToString())),
			VF<NpgsqlTypes.NpgsqlPoint   >(v => Format(v.ToString())),
			VF<NpgsqlTypes.NpgsqlPath    >(v => Format(v.ToString())),
			VF<NpgsqlTypes.NpgsqlPolygon >(v => Format(v.ToString())),
			VF<NpgsqlTypes.NpgsqlCircle  >(v => Format(v.ToString())),
#pragma warning disable CS0618 // NpgsqlInet obsolete
			VF<NpgsqlTypes.NpgsqlInet    >(v => Format(v.ToString())),
#pragma warning restore CS0618
			// additional formatters are generated dynamically
		}
		.ToDictionary(f => f.Type, f => (ValueFormatter?)f));

		static readonly ConcurrentDictionary<Type,NumberFormatter?> _numberFormatters = new ConcurrentDictionary<Type, NumberFormatter?>(new[]
		{
			NF<short  , long>   (value => v => v + value,                  v => n => v /       n),
			NF<int    , long>   (value => v => v + value,                  v => n => v /       n),
			NF<long   , long>   (value => v => v + value,                  v => n => v /       n),
			NF<ushort , ulong>  (value => v => v + value,                  v => n => v / (uint)n),
			NF<uint   , ulong>  (value => v => v + value,                  v => n => v / (uint)n),
			NF<ulong  , ulong>  (value => v => v + value,                  v => n => v / (uint)n),
			NF<sbyte  , int>    (value => v => v + value,                  v => n => v /       n),
			NF<byte   , long>   (value => v => v + value,                  v => n => v /       n),
			NF<decimal, decimal>(value => v => v + value,                  v => n => v /       n),
			NF<double , double> (value => v => v + value,                  v => n => v /       n),
			NF<float  , float>  (value => v => v + value,                  v => n => v /       n),

			NF<SqlInt16,long>   (value => v => v + value.Value,            v => n => v / n),
			NF<SqlInt32,long>   (value => v => v + value.Value,            v => n => v / n),
			NF<SqlInt64,long>   (value => v => v + value.Value,            v => n => v / n),
			NF<SqlByte, long>   (value => v => v + value.Value,            v => n => v / n),
			NF<SqlDecimal>      (value => v => (v.IsNull ? 0 : v) + value, v => n => v / n),
			NF<SqlMoney>        (value => v => (v.IsNull ? 0 : v) + value, v => n => v / n),
			NF<SqlDouble>       (value => v => (v.IsNull ? 0 : v) + value, v => n => v / n),
			NF<SqlSingle>       (value => v => (v.IsNull ? 0 : v) + value, v => n => v / n),

			//Dynamic types will be genareted later

		}
		.ToDictionary(f => f.Type, f => (NumberFormatter?)f));

		#region IsNullChecker

		abstract class IsNullChecker
		{
			public abstract bool IsNull(object value);
		}

		class IsNullChecker<T> : IsNullChecker
		{
			private readonly Func<T,bool> _isNullFunc;

			public IsNullChecker(Func<T,bool> isNullFunc)
			{
				_isNullFunc = isNullFunc;
			}

			public override bool IsNull(object value)
			{
				return _isNullFunc((T) value);
			}
		}

		#endregion

		#region ValueFormatter

		static ValueFormatter VF<T>(Func<T,object> format, string? font = null, string? size = null, bool nowrap = true)
		{
			return new ValueFormatter<T>(format, nowrap) { Font = font, Size = size };
		}

		abstract class ValueFormatter
		{
			public abstract Type   Type { get; }

			public string? Font;
			public string? Size;
			public bool    NoWrap;

			public abstract object FormatValue(object value);
		}

		class DynamicFormatter : ValueFormatter
		{
			private readonly Func<object, string> _formatFunc;

			public DynamicFormatter(Type type, Func<object, string> formatFunc)
			{
				Type = type;
				_formatFunc = formatFunc;
			}

			public override Type Type { get; }

			public override object FormatValue(object value)
			{
				return _formatFunc.DynamicInvoke(value)!;
			}
		}

		class ValueFormatter<T> : ValueFormatter
		{
			public ValueFormatter(Func<T, object> format, bool noWrap)
			{
				Format = format;
				NoWrap = noWrap;
			}

			public override Type Type => typeof(T);

			public readonly Func<T,object> Format;

			public override object FormatValue(object value)
			{
				return Format((T)value);
			}
		}

		#endregion

		#region NumberFormatter

		static NumberFormatter NF<T>(Func<T,Func<T,T>> add, Func<T,Func<int,object>> avr)
		{
			return new NumberFormatter<T>(add, avr, v => new XElement("span", v));
		}

		static NumberFormatter NF<T,TT>(Func<T,Func<TT,TT>> add, Func<TT,Func<int,object>> avr)
		{
			return new NumberFormatter<T,TT>(add, avr, v => new XElement("span", v));
		}

		abstract class NumberFormatter
		{
			public abstract Type Type { get; }

			public abstract void     AddTotal  (Total total, object value);
			public abstract XElement GetElement(object value);
		}

		class NumberFormatter<T> : NumberFormatter
		{
			public NumberFormatter(Func<T, Func<T, T>> add, Func<T, Func<int, object>> average, Func<T, XElement> format)
			{
				Add     = add;
				Average = average;
				Format  = format;
			}

			public override Type Type => typeof(T);

			public readonly Func<T,Func<T,T>>        Add;
			public readonly Func<T,Func<int,object>> Average;

			public override void AddTotal(Total total, object value)
			{
				total.Add(Add((T)value), Average);
			}

			public readonly Func<T,XElement> Format;

			public override XElement GetElement(object value)
			{
				return Format((T)value);
			}
		}

		class NumberFormatter<T,TT> : NumberFormatter
		{
			public NumberFormatter(Func<T, Func<TT, TT>> add, Func<TT, Func<int, object>> average, Func<T, XElement> format)
			{
				Add     = add;
				Average = average;
				Format  = format;
			}

			public override Type Type => typeof(T);

			public readonly Func<T,Func<TT,TT>>       Add;
			public readonly Func<TT,Func<int,object>> Average;
			public readonly Func<T, XElement>         Format;

			public override void AddTotal(Total total, object value)
			{
				total.Add(Add((T)value), Average);
			}

			public override XElement GetElement(object value)
			{
				return Format((T)value);
			}
		}

		#endregion
	}
}
