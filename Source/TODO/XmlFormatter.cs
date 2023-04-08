//using System.Collections;
//using System.Collections.Concurrent;
//using System.Data.SqlTypes;
//using System.Diagnostics.CodeAnalysis;
//using System.Linq.Expressions;
//using System.Net;
//using System.Net.NetworkInformation;
//using System.Runtime.CompilerServices;
//using System.Text;
//using System.Xml;
//using System.Xml.Linq;
//using LINQPad;
//using LinqToDB.Extensions;
//using LinqToDB.Mapping;

//namespace LinqToDB.LINQPad;

//// TODO: this class needs refactoring...
//static class XmlFormatter
//{
//	sealed class Total
//	{
//		public object?            Value;
//		public Func<int,object?>? GetAverage;
//		public bool               IsNumber;

//		public void Add<T>(Func<T, T> add, Func<T, Func<int, object?>> avr)
//		{
//			if (Value == null)
//			{
//				Value = add(default!);
//				IsNumber = true;
//			}
//			else
//			{
//				Value = add((T)Value);
//			}

//			GetAverage = avr((T)Value!);
//		}
//	}

//	static int _id;

//	public static object? Format(MappingSchema mappingSchema, object? objectToWrite)
//	{
//		if (IsNull(objectToWrite))
//			return null;

//		if (objectToWrite is string || objectToWrite is XElement)
//			return objectToWrite;

//		var type = objectToWrite.GetType();

//		if (objectToWrite is IEnumerable enumerable)
//		{
//			var itemType = type.GetItemType()!;
//			var items    = enumerable.Cast<object>().ToList();
//			var tableID  = ++_id;

//			var columns = mappingSchema.IsScalarType(itemType)
//				? new[]
//				{
//					new
//					{
//						MemberType = itemType,
//						MemberName = "",
//						GetValue   = (Func<MappingSchema,object,object?>)(static (ms, v) => v),
//						Total      = new Total(),
//					}
//				}
//				: mappingSchema.GetEntityDescriptor(itemType).Columns
//					.Select(static c => new
//					{
//						c.MemberType,
//						c.MemberName,
//						GetValue = (Func<MappingSchema,object,object?>)(static (ms, v) => c.GetProviderValue(v)),
//						Total    = new Total(),
//					})
//					.ToArray();

//			return Util.RawHtml(
//				new XElement("div",
//					new XAttribute("class", "spacer"),
//					new XElement("table",
//						new object[]
//						{
//							new XAttribute("id", $"t{tableID}"),
//							new XElement("tr",
//								new XElement("td",
//									new XAttribute("class",   "typeheader"),
//									new XAttribute("colspan", columns.Length),
//									new XElement("a",
//										new XAttribute("href",  ""),
//										new XAttribute("class", "typeheader"),
//										new XAttribute("onclick", $"return toggle('t{tableID}');"),
//										new XElement("span",
//											new XAttribute("class", "typeglyph"),
//											new XAttribute("id",    $"t{tableID}ud"),
//											5),
//										$"{GetTypeName(itemType)} ({items.Count} items)"),
//									new XElement("a",
//										new XAttribute("href",  ""),
//										new XAttribute("class", "extenser"),
//										new XAttribute("onclick", "return window.external.CustomClick('0',false);"),
//										new XElement("span",
//											new XAttribute("class", "extenser"),
//											4)))),
//							new XElement("tr",
//								columns.Select(static c =>
//									new XElement("th",
//										new XAttribute("title", GetTypeName(c.MemberType)),
//										$"{c.MemberName}")))
//						}
//						.Union(items
//							.Select(static i => new XElement("tr", columns.Select(static c => FormatValue(c.Total, c.GetValue(mappingSchema, i)))))
//							.ToList())
//						.Union(
//							new object[]
//							{
//								new XElement("tr", columns.Select(static c =>
//									new XElement("td",
//										new XAttribute("title", c.Total.Value == null ? "Totals" : $"Total={c.Total.Value}\r\nAverage={c.Total.GetAverage!(items.Count)}"),
//										new XAttribute("class", "columntotal"),
//										new XAttribute("style", "font-size:100%;"),
//										c.Total.Value))),
//							}
//							.Where(static _ => columns.Any(static c => c.Total.Value != null))
//							))));
//		}

//		return objectToWrite;
//	}

//	static bool IsAnonymousType(Type type)
//	{
//		return
//			Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false) &&
//			type.IsGenericType && type.Name.Contains("AnonymousType") &&
//			(type.Name.StartsWith("<>") || type.Name.StartsWith("VB$")) &&
//			(type.Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic;
//	}

//	static string GetTypeName(Type type)
//	{
//		if (type.IsNullable())
//			return type.ToNullableUnderlying().Name + "?";

//		if (IsAnonymousType(type))
//			return "new";

//		return type.Name;
//	}

//	static bool DynamicCheckForNull(Type? baseTypeType, Type type, object value, ref bool isNull, string isNullProperty = "IsNull")
//	{
//		if (baseTypeType == null || !baseTypeType.IsSameOrParentOf(type))
//			return false;

//		var prop = baseTypeType.GetProperty(isNullProperty);
//		if (prop == null)
//			return false;

//		isNull = (bool)prop.GetValue(value)!;
//		return true;
//	}

//	static bool DynamicCheckForNull(Type type, object value, ref bool isNull)
//	{
//		return DynamicCheckForNull(type.Assembly.GetType("IBM.Data.DB2Types.INullable", false), type, value, ref isNull);
//	}

//	static bool IsNull([NotNullWhen(false)] object? value)
//	{
//		if (value == null || value is DBNull)
//			return true;

//		if (value is INullable nullable)
//			return nullable.IsNull;

//		var isNull = false;
//		if (DynamicCheckForNull(value.GetType(), value, ref isNull))
//			return isNull;
//		return false;
//	}

//	static NumberFormatter GenerateNumberFormatter<T>(Type valueType, bool checkForNull = true, Func<ParameterExpression, Expression>? convertFunc = null)
//	{
//		// value => v => (v.IsNull ? 0 : v) + value
//		var innerType  = typeof(T);
//		var paramValue = Expression.Parameter(innerType);
//		var paramV     = Expression.Parameter(valueType);
//		var addLamba =
//			Expression.Lambda(
//				Expression.Lambda(
//					Expression.Add(
//						checkForNull
//							? Expression.Condition(
//								Expression.PropertyOrField(paramV, "IsNull"),
//								Expression.Constant(default(T), innerType),
//								Expression.Convert(convertFunc != null ? convertFunc(paramV) : paramV, innerType))
//							: paramV,
//						paramValue
//					), paramValue
//				), paramV);

//		// average
//		// v => n => v / n

//		var paramNumber = Expression.Parameter(typeof(int));
//		var avgLambda =
//			Expression.Lambda(
//				Expression.Lambda(
//					Expression.Convert(Expression.Divide(paramValue, Expression.Convert(paramNumber, innerType)), typeof(object)),
//					paramNumber
//				), paramValue
//			);

//		// format
//		// v => new XElement("span", v)
//		var constructor = typeof(XElement).GetConstructor(new []{typeof(XName), typeof(object) });

//		var formatExpr = Expression.Lambda(
//			Expression.New(
//				constructor ?? throw new InvalidOperationException(),
//				Expression.Convert(Expression.Constant("span"), typeof(XName)),
//				Expression.Convert(Expression.Convert(convertFunc != null ? convertFunc(paramV) : paramV, innerType), typeof(object))),
//			paramV);

//		var formatterType = typeof(NumberFormatter<,>).MakeGenericType(valueType, innerType);
//		var formatter     = (NumberFormatter)Activator.CreateInstance(formatterType, addLamba.Compile(), avgLambda.Compile(), formatExpr.Compile())!;

//		return formatter;
//	}

//	static ValueFormatter? GetValueFormatter(Type type)
//	{
//		var vf = _valueFormatters.GetOrAdd(type, static t =>
//		{
//			switch (type.FullName)
//			{
//				case "IBM.Data.DB2Types.DB2Time":
//					return GenerateValueFormatter<TimeSpan>(type, static dt => Expression.PropertyOrField(dt, "Value"));
//				case "IBM.Data.DB2Types.DB2RowId": // z/OS type
//				case "IBM.Data.DB2Types.DB2Binary":
//					return GenerateValueFormatter<byte[]>(type, static dt => Expression.PropertyOrField(dt, "Value"));
//				case "IBM.Data.DB2Types.DB2String":
//					return GenerateValueFormatter<string>(type, static dt => Expression.PropertyOrField(dt, "Value"));
//				case "IBM.Data.DB2Types.DB2TimeStamp":
//					// to avoid "This value of the DB2Type will be truncated." when taking Value
//					return GenerateValueFormatter<string>(type, static v => Expression.Call(v, "ToString", Array.Empty<Type>()));
//				case "IBM.Data.DB2Types.DB2Date":
//				case "IBM.Data.DB2Types.DB2DateTime": // z/OS and Informix type
//					return GenerateValueFormatter<DateTime>(type, static dt => Expression.PropertyOrField(dt, "Value"));
//			}

//			return null;
//		});

//		return vf;
//	}

//	static NumberFormatter? GetNumberFormatter(Type type)
//	{
//		var nf = _numberFormatters.GetOrAdd(type, static t =>
//		{
//			switch (type.FullName)
//			{
//				case "IBM.Data.DB2Types.DB2Int16":
//				case "IBM.Data.DB2Types.DB2Int32":
//				case "IBM.Data.DB2Types.DB2Int64":
//					return GenerateNumberFormatter<long>(type, true, static p => Expression.PropertyOrField(p, "Value"));

//				case "IBM.Data.DB2Types.DB2Decimal":
//				case "IBM.Data.DB2Types.DB2DecimalFloat":
//					return GenerateNumberFormatter<decimal>(type, true, static p => Expression.PropertyOrField(p, "Value"));

//				case "IBM.Data.DB2Types.DB2Double":
//				case "IBM.Data.DB2Types.DB2Real":
//					return GenerateNumberFormatter<double>(type, true, static p => Expression.PropertyOrField(p, "Value"));
//			}

//			return null;
//		});

//		return nf;
//	}

//	static XElement FormatValue(Total total, object? value)
//	{
//		try
//		{
//			if (IsNull(value))
//				return new XElement("td", new XAttribute("style", "text-align:center;"), new XElement("i", new XAttribute("style", "font-style: italic"), "null"));

//			var type = value.GetType();

//			// multi-dimensional arrays not supported for now (and we don't expose them in schema)
//			if (type.IsArray && !(value is byte[]))
//				return new XElement("td", FormatArray((Array)value));

//			if (value is IDictionary dict)
//				return new XElement("td", FormatDictionary(dict));

//			var nf = GetNumberFormatter(type);

//			if (nf != null)
//			{
//				nf.AddTotal(total, value);
//				return new XElement("td", new XAttribute("class", "n"), value);
//			}

//			var vf = GetValueFormatter(type);
//			if (vf != null)
//			{
//				var list = new List<object>();

//				if (vf.NoWrap)
//					list.Add(new XAttribute("nowrap", "nowrap"));

//				var style = "";

//				if (vf.Font != null) style += $"font-family:{vf.Font};";
//				if (vf.Size != null) style += $"font-size:{vf.Size};";

//				if (style.Length > 0)
//					list.Add(new XAttribute("style", style));

//				list.Add(vf.FormatValue(value));

//				return new XElement("td", list.ToArray());
//			}

//			return new XElement("td", value.ToString());
//		}
//		catch (Exception ex)
//		{
//			return new XElement("td",
//				new XAttribute("style", "color:red"),
//				ex.Message);
//		}
//	}

//	static XElement FormatArray(Array array)
//	{
//		var fragments = new List<object>
//		{
//			$" Len:{array.Length} ["
//		};

//		int i;

//		for (i = 0; i < array.Length && i < 10; i++)
//		{
//			if (i > 0)
//				fragments.Add(", ");

//			fragments.Add(FormatValueXml(array.GetValue(i))!);
//		}

//		if (i < array.Length)
//			fragments.Add("...");

//		fragments.Add("]");

//		return new XElement("span", fragments.ToArray());
//	}

//	static XElement FormatDictionary(IDictionary dict)
//	{
//		var fragments = new List<object>
//		{
//			$" Size:{dict.Count} {{"
//		};

//		int i = 0;

//		foreach (var key in dict.Keys)
//		{
//			if (i > 0)
//				fragments.Add(", ");

//			fragments.Add("{");
//			fragments.Add(FormatValueXml(key)!);
//			fragments.Add(", ");
//			fragments.Add(FormatValueXml(dict[key!])!);
//			fragments.Add("}");

//			i++;

//			if (i == 10)
//				break;
//		}

//		if (i < dict.Count)
//			fragments.Add("...");

//		fragments.Add("}");

//		return new XElement("span", fragments.ToArray());
//	}

//	public static object FormatValue(object? value)
//	{
//		// not sure why, but LINQPad doesn't call formatter for this type properly (only this type)
//		if (value is Microsoft.SqlServer.Types.SqlHierarchyId)
//			return value.ToString()!;

//		var val = FormatValueXml(value, true);
//		if (val != null)
//			return Util.RawHtml(val);

//		return value!;
//	}

//	private static XElement? FormatValueXml(object? value, bool allowNull = false)
//	{
//		if (IsNull(value))
//			return new XElement("span", new XAttribute("style", "text-align:center;"), new XElement("i", new XAttribute("style", "font-style: italic"), "null"));

//		var type = value.GetType();

//		var nf = GetNumberFormatter(type);
//		if (nf != null)
//			return nf.GetElement(value);

//		var vf = GetValueFormatter(type);
//		if (vf != null)
//		{
//			var style = "";

//			if (vf.NoWrap) style += "white-space:nowrap;";
//			if (vf.Font != null) style += $"font-family:{vf.Font};";
//			if (vf.Size != null) style += $"font-size:{vf.Size};";

//			return new XElement("span",
//				new XAttribute("style", style),
//				vf.FormatValue(value));
//		}

//		if (allowNull)
//			return null;

//		throw new LinqToDBLinqPadException($"Unsupported value type: {type}");
//	}

//	static string Format(DateTime dt)
//	{
//		return dt.Hour == 0 && dt.Minute == 0 && dt.Second == 0
//			? dt.ToString("yyyy-MM-dd")
//			: dt.ToString("yyyy-MM-dd HH:mm:ss");
//	}

//	static string Format(TimeSpan ts)
//	{
//		return ts.ToString("c");
//	}

//	static string Format(NpgsqlTypes.NpgsqlInterval interval)
//	{
//		var sb = new StringBuilder();

//		if (interval.Months != 0)
//		{
//			if (interval.Months != 1 && interval.Months != -1)
//				sb.Append($"{interval.Months} months");
//			else
//				sb.Append($"{interval.Months} month");
//		}

//		if (interval.Days != 0)
//		{
//			if (sb.Length > 0)
//				sb.Append(' ');

//			if (interval.Days != 1 && interval.Days != -1)
//				sb.Append($"{interval.Days} days");
//			else
//				sb.Append($"{interval.Days} day");
//		}

//		if (interval.Time != 0)
//		{
//			if (sb.Length > 0)
//				sb.Append(' ');

//			sb.Append(Format(TimeSpan.FromTicks(interval.Time * 10)));
//		}

//		return sb.Length > 0 ? sb.ToString() : "0 days";
//	}

//	static string Format(BitArray value)
//	{
//		var sb = new StringBuilder($" Len:{value.Length} 0b");

//		int i;

//		for (i = 0; i < value.Length && i < 64; i++)
//			sb.Append(value[i] ? '1' : '0');

//		if (i < value.Length)
//			sb.Append("...");

//		return sb.ToString();
//	}

//	static ValueFormatter GenerateValueFormatter<T>(Type type, Func<ParameterExpression, Expression> dataExtractor, bool nowrap = true)
//	{
//		var param = Expression.Parameter(type);

//		var extractedValue = dataExtractor(param);

//		var methodInfo = typeof(XmlFormatter).GetMethodEx("Format", typeof(T))
//			?? throw new LinqToDBLinqPadException($"XmlFormatter.Format({typeof(T)}) method not found");

//		var expr = Expression.Lambda(
//			Expression.Call(methodInfo, extractedValue),
//			param);

//		var formatterType = typeof(ValueFormatter<>).MakeGenericType(type);
//		var formatter = (ValueFormatter)Activator.CreateInstance(formatterType, expr.Compile(), nowrap)!;

//		return formatter;
//	}

//	static readonly ConcurrentDictionary<Type,ValueFormatter?> _valueFormatters = new (new[]
//	{
//		VF<char>           (      Format),
//		VF<string>         (      Format),
//		VF<SqlString>      (static v =>  Format(v.Value)),
//		VF<SqlBoolean>     (static v =>  Format(v.Value.ToString())),
//		VF<DateTime>       (      Format),
//		VF<SqlDateTime>    (static dt => Format(dt.Value)),
//		VF<byte[]>         (Format),
//		VF<SqlBinary>      (static v =>  Format(v.Value)),
//		VF<Guid>           (static v => v.      ToString("B").ToUpperInvariant(), font:"consolas", size:"110%"),
//		VF<SqlGuid>        (static v => v.Value.ToString("B").ToUpperInvariant(), font:"consolas", size:"110%"),
//		VF<TimeSpan>       (static v => v.ToString()),
//		VF<SqlXml>         (static v => v.Value),

//		VF<BitArray>       (     Format),
//		VF<IPAddress>      (static v => Format(v.ToString())),
//		VF<PhysicalAddress>(static v => Format(v.GetAddressBytes())),

//		// mysql types
//		VF<MySqlConnector.MySqlDateTime>(static v => FormatValueXml(v.IsValidDateTime ? v.GetDateTime() : "invalid")!),

//		// sql server types
//		VF<Microsoft.SqlServer.Types.SqlHierarchyId>(static v => Format(v.ToString())),
//		VF<Microsoft.SqlServer.Types.SqlGeography  >(static v => Format(v.ToString())),
//		VF<Microsoft.SqlServer.Types.SqlGeometry   >(static v => Format(v.ToString())),

//		// oracle managed types
//		VF<Oracle.ManagedDataAccess.Types.OracleClob   >(static v => FormatValueXml(v.IsNull ? null : v.Value)!),
//		VF<Oracle.ManagedDataAccess.Types.OracleDate   >(static v => Format(v.Value)),
//		VF<Oracle.ManagedDataAccess.Types.OracleBinary >(static v => Format(v.Value)),
//		VF<Oracle.ManagedDataAccess.Types.OracleBFile  >(static v => FormatValueXml(v.IsNull ? null : v.FileName)!), // value is not accessible and file name is better
//		VF<Oracle.ManagedDataAccess.Types.OracleBlob   >(static v => FormatValueXml(v.IsNull ? null : v.Value)!),
//		VF<Oracle.ManagedDataAccess.Types.OracleString >(static v => Format(v.Value)),
//		VF<Oracle.ManagedDataAccess.Types.OracleXmlType>(static v => FormatValueXml(v.IsNull ? null : v.Value)!),

//		// npgsql types
//#pragma warning disable CS0618 // Type or member is obsolete
//		VF<NpgsqlTypes.NpgsqlInterval>(     Format),
//#pragma warning restore CS0618 // Type or member is obsolete
//		VF<NpgsqlTypes.NpgsqlBox     >(static v => Format(v.ToString())),
//		VF<NpgsqlTypes.NpgsqlLSeg    >(static v => Format(v.ToString())),
//		VF<NpgsqlTypes.NpgsqlLine    >(static v => Format(v.ToString())),
//		VF<NpgsqlTypes.NpgsqlPoint   >(static v => Format(v.ToString())),
//		VF<NpgsqlTypes.NpgsqlPath    >(static v => Format(v.ToString())),
//		VF<NpgsqlTypes.NpgsqlPolygon >(static v => Format(v.ToString())),
//		VF<NpgsqlTypes.NpgsqlCircle  >(static v => Format(v.ToString())),
//#pragma warning disable CS0618 // NpgsqlInet obsolete
//		VF<NpgsqlTypes.NpgsqlInet    >(static v => Format(v.ToString())),
//#pragma warning restore CS0618
//		// additional formatters are generated dynamically
//	}
//	.ToDictionary(static f => f.Type, static f => (ValueFormatter?)f));

//	static readonly ConcurrentDictionary<Type,NumberFormatter?> _numberFormatters = new (new[]
//	{
//		NF<short  , long>   (static value => static v => v + value,                  static v => static n => v /       n),
//		NF<int    , long>   (static value => static v => v + value,                  static v => static n => v /       n),
//		NF<long   , long>   (static value => static v => v + value,                  static v => static n => v /       n),
//		NF<ushort , ulong>  (static value => static v => v + value,                  static v => static n => v / (uint)n),
//		NF<uint   , ulong>  (static value => static v => v + value,                  static v => static n => v / (uint)n),
//		NF<ulong  , ulong>  (static value => static v => v + value,                  static v => static n => v / (uint)n),
//		NF<sbyte  , int>    (static value => static v => v + value,                  static v => static n => v /       n),
//		NF<byte   , long>   (static value => static v => v + value,                  static v => static n => v /       n),
//		NF<decimal, decimal>(static value => static v => v + value,                  static v => static n => v /       n),
//		NF<double , double> (static value => static v => v + value,                  static v => static n => v /       n),
//		NF<float  , float>  (static value => static v => v + value,                  static v => static n => v /       n),

//		NF<SqlInt16,long>   (static value => static v => v + value.Value,            static v => static n => v / n),
//		NF<SqlInt32,long>   (static value => static v => v + value.Value,            static v => static n => v / n),
//		NF<SqlInt64,long>   (static value => static v => v + value.Value,            static v => static n => v / n),
//		NF<SqlByte, long>   (static value => static v => v + value.Value,            static v => static n => v / n),
//		NF<SqlDecimal>      (static value => static v => (v.IsNull ? 0 : v) + value, static v => static n => v / n),
//		NF<SqlMoney>        (static value => static v => (v.IsNull ? 0 : v) + value, static v => static n => v / n),
//		NF<SqlDouble>       (static value => static v => (v.IsNull ? 0 : v) + value, static v => static n => v / n),
//		NF<SqlSingle>       (static value => static v => (v.IsNull ? 0 : v) + value, static v => static n => v / n),

//		// another option is to use BigInteger class
//		NF<Oracle.ManagedDataAccess.Types.OracleDecimal, decimal>(static value => static v => v + Oracle.ManagedDataAccess.Types.OracleDecimal.SetPrecision(value, 27).Value, static v => static n => v / n, static v => new XElement("span", v.ToString())),

//		//Dynamic types will be genareted later

//	}
//	.ToDictionary(static f => f.Type, static f => (NumberFormatter?)f));

//	#region IsNullChecker

//	abstract class IsNullChecker
//	{
//		public abstract bool IsNull(object value);
//	}

//	sealed class IsNullChecker<T> : IsNullChecker
//	{
//		private readonly Func<T,bool> _isNullFunc;

//		public IsNullChecker(Func<T, bool> isNullFunc)
//		{
//			_isNullFunc = isNullFunc;
//		}

//		public override bool IsNull(object value)
//		{
//			return _isNullFunc((T)value);
//		}
//	}

//	#endregion

//	#region ValueFormatter

//	static ValueFormatter VF<T>(Func<T, object> format, string? font = null, string? size = null, bool nowrap = true)
//	{
//		return new ValueFormatter<T>(format, nowrap) { Font = font, Size = size };
//	}

//	abstract class ValueFormatter
//	{
//		public abstract Type Type { get; }

//		public string? Font;
//		public string? Size;
//		public bool    NoWrap;

//		public abstract object FormatValue(object value);
//	}

//	sealed class DynamicFormatter : ValueFormatter
//	{
//		private readonly Func<object, string> _formatFunc;

//		public DynamicFormatter(Type type, Func<object, string> formatFunc)
//		{
//			Type = type;
//			_formatFunc = formatFunc;
//		}

//		public override Type Type { get; }

//		public override object FormatValue(object value)
//		{
//			return _formatFunc.DynamicInvoke(value)!;
//		}
//	}

//	sealed class ValueFormatter<T> : ValueFormatter
//	{
//		public ValueFormatter(Func<T, object> format, bool noWrap)
//		{
//			Format = format;
//			NoWrap = noWrap;
//		}

//		public override Type Type => typeof(T);

//		public readonly Func<T,object> Format;

//		public override object FormatValue(object value)
//		{
//			return Format((T)value);
//		}
//	}

//	#endregion

//	#region NumberFormatter

//	static NumberFormatter NF<T>(Func<T, Func<T, T>> add, Func<T, Func<int, object>> avr)
//	{
//		return new NumberFormatter<T>(add, avr, static v => new XElement("span", v));
//	}

//	static NumberFormatter NF<T, TT>(Func<T, Func<TT, TT>> add, Func<TT, Func<int, object>> avr)
//	{
//		return new NumberFormatter<T, TT>(add, avr, static v => new XElement("span", v));
//	}

//	static NumberFormatter NF<T, TT>(Func<T, Func<TT, TT>> add, Func<TT, Func<int, object>> avr, Func<T, XElement> format)
//	{
//		return new NumberFormatter<T, TT>(add, avr, format);
//	}

//	abstract class NumberFormatter
//	{
//		public abstract Type Type { get; }

//		public abstract void AddTotal(Total total, object value);
//		public abstract XElement GetElement(object value);
//	}

//	sealed class NumberFormatter<T> : NumberFormatter
//	{
//		public NumberFormatter(Func<T, Func<T, T>> add, Func<T, Func<int, object>> average, Func<T, XElement> format)
//		{
//			Add = add;
//			Average = average;
//			Format = format;
//		}

//		public override Type Type => typeof(T);

//		public readonly Func<T,Func<T,T>>        Add;
//		public readonly Func<T,Func<int,object>> Average;

//		public override void AddTotal(Total total, object value)
//		{
//			total.Add(Add((T)value), Average);
//		}

//		public readonly Func<T,XElement> Format;

//		public override XElement GetElement(object value)
//		{
//			return Format((T)value);
//		}
//	}

//	sealed class NumberFormatter<T, TT> : NumberFormatter
//	{
//		public NumberFormatter(Func<T, Func<TT, TT>> add, Func<TT, Func<int, object>> average, Func<T, XElement> format)
//		{
//			Add = add;
//			Average = average;
//			Format = format;
//		}

//		public override Type Type => typeof(T);

//		public readonly Func<T,Func<TT,TT>>       Add;
//		public readonly Func<TT,Func<int,object>> Average;
//		public readonly Func<T, XElement>         Format;

//		public override void AddTotal(Total total, object value)
//		{
//			total.Add(Add((T)value), Average);
//		}

//		public override XElement GetElement(object value)
//		{
//			return Format((T)value);
//		}
//	}

//	#endregion
//}
