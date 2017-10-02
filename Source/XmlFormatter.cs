using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using IBM.Data.DB2Types;
using LinqToDB.Extensions;
using LinqToDB.Mapping;

using LINQPad;
using LINQPad.Extensibility.DataContext;

namespace LinqToDB.LINQPad
{
	static class XmlFormatter
	{
		class Total
		{
			public object           Value;
			public Func<int,object> GetAverage;
			public bool             IsNumber;

			public void Add<T>(Func<T,T> add, Func<T,Func<int,object>> avr)
			{
				if (Value == null)
				{
					Value    = add(default(T));
					IsNumber = true;
				}
				else
				{
					Value = add((T)Value);
				}

				GetAverage = avr((T)Value);
			}
		}

		static int _id;

		public static object Format(MappingSchema mappingSchema, object objectToWrite)
		{
			if (objectToWrite == null || objectToWrite is string || objectToWrite is XElement)
				return objectToWrite;

			if (IsNull(objectToWrite))
				return null;

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
							GetValue = (Func<MappingSchema,object,object>)((ms, v) => v),
							Total    = new Total(),
						}
					}
					:
					mappingSchema.GetEntityDescriptor(itemType).Columns
						.Select(c => new
						{
							c.MemberType,
							c.MemberName,
							GetValue = (Func<MappingSchema,object,object>)c.GetValue,
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
											new XAttribute("title", c.Total.Value == null ? "Totals" : $"Total={c.Total.Value}\r\nAverage={c.Total.GetAverage(items.Count)}"),
											new XAttribute("class", "columntotal"),
											new XAttribute("style", "font-size:100%;"),
											c.Total.Value))),
								}
								.Where(_ => columns.Any(c => c.Total.Value != null))
								))));
			}

//			if (!_mappingSchema.IsScalarType(objectToWrite.GetType()))
//			{
//				objectToWrite = Util.RawHtml(new XElement("div", objectToWrite.GetType()));
//				return;
//			}

			//MessageBox.Show($"{objectToWrite.GetType()}\r\n{objectToWrite}");

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

		static bool IsNull(object value)
		{
			return
				value == null   ||
				value is DBNull ||
				value is System.Data.SqlTypes.          INullable && ((System.Data.SqlTypes.          INullable)value).IsNull ||
				value is Oracle.DataAccess.Types.       INullable && ((Oracle.DataAccess.Types.       INullable)value).IsNull ||
				value is Oracle.ManagedDataAccess.Types.INullable && ((Oracle.ManagedDataAccess.Types.INullable)value).IsNull ||
				value is IBM.Data.DB2Types.             INullable && ((IBM.Data.DB2Types.             INullable)value).IsNull ||

				value is NpgsqlTypes.NpgsqlDateTime       && ((NpgsqlTypes.NpgsqlDateTime)value).Kind == DateTimeKind.Unspecified ||

				value is Sybase.Data.AseClient.AseDecimal && ((Sybase.Data.AseClient.AseDecimal)value).IsNull ||

				value is MySql.Data.Types.MySqlDecimal    && ((MySql.Data.Types.MySqlDecimal) value).IsNull ||
				value is MySql.Data.Types.MySqlDateTime   && ((MySql.Data.Types.MySqlDateTime)value).IsNull ||
				value is MySql.Data.Types.MySqlGeometry   && ((MySql.Data.Types.MySqlGeometry)value).IsNull
				;
		}

		static bool IsValue(object value)
		{
			return
				value is System.Data.SqlTypes.          INullable ||
				value is Oracle.DataAccess.Types.       INullable ||
				value is Oracle.ManagedDataAccess.Types.INullable ||
				value is IBM.Data.DB2Types.             INullable ||
				value is MySql.Data.Types.MySqlGeometry
				;
		}

		static XElement FormatValue(Total total, object value)
		{
			try
			{
				if (IsNull(value))
					return new XElement("td", new XAttribute("style", "text-align:center;"), new XElement("i", "null"));

				if (_numberFormatters.TryGetValue(value.GetType(), out var nf))
				{
					nf.AddTotal(total, value);
					return new XElement("td", new XAttribute("class", "n"), value);
				}

				if (_valueFormatters.TryGetValue(value.GetType(), out var vf))
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

		public static object FormatValue(object value, ObjectGraphInfo info)
		{
			if (IsNull(value))
				return Util.RawHtml(new XElement("span", new XAttribute("style", "text-align:center;"), new XElement("i", "null")));

			if (value is DB2Xml xml)
			{
				var doc = XDocument.Parse(xml.GetString());
				return doc;
			}

			if (_numberFormatters.TryGetValue(value.GetType(), out var nf))
				return Util.RawHtml(nf.GetElement(value));

			if (_valueFormatters.TryGetValue(value.GetType(), out var vf))
			{
				var style = "";

				if (vf.NoWrap) style += "white-space:nowrap;";
				if (vf.Font != null) style += $"font-family:{vf.Font};";
				if (vf.Size != null) style += $"font-size:{vf.Size};";

				return Util.RawHtml(new XElement("span",
					new XAttribute("style", style),
					vf.FormatValue(value)));
			}

			if (value is Microsoft.SqlServer.Types.SqlHierarchyId)
				return value.ToString();

			//Debug.WriteLine($"{value.GetType()}: {value} {IsValue(value)}");

			return IsValue(value) ? Util.RawHtml(new XElement("span", value)) : value;
		}

		static string Format(DateTime dt)
		{
			return dt.Hour == 0 && dt.Minute == 0 && dt.Second == 0
				? dt.ToString("yyyy-MM-dd")
				: dt.ToString("yyyy-MM-dd HH:mm:ss");
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

		static readonly Dictionary<Type,ValueFormatter> _valueFormatters = new Dictionary<Type, ValueFormatter>(new[]
		{
			VF                                     <DateTime>(      Format),
			VF             <System.Data.SqlTypes.SqlDateTime>(dt => Format(dt.Value)),
			VF       <Oracle.DataAccess.Types.OracleDate>    (dt => Format(dt.Value)),
			VF<Oracle.ManagedDataAccess.Types.OracleDate>    (dt => Format(dt.Value)),
			VF               <MySql.Data.Types.MySqlDateTime>(dt => Format(dt.Value)),
			VF                   <NpgsqlTypes.NpgsqlDateTime>(dt => Format((DateTime)dt)),
			VF                <IBM.Data.DB2Types.DB2DateTime>(dt => Format(dt.Value)),
			VF                <IBM.Data.DB2Types.DB2Date>    (dt => Format(dt.Value)),

			VF<byte[]>(Format),
			VF<IBM.Data.DB2Types.DB2Blob>(v => Format(v.Value)),

			VF<                        Guid>(v => v.      ToString("B").ToUpper(), font:"consolas", size:"110%"),
			VF<System.Data.SqlTypes.SqlGuid>(v => v.Value.ToString("B").ToUpper(), font:"consolas", size:"110%"),
		}
		.ToDictionary(f => f.Type));

		static readonly Dictionary<Type,NumberFormatter> _numberFormatters = new Dictionary<Type, NumberFormatter>(new[]
		{
			NF<Int16,  Int64>                               (value => v => v + value,                  v => n => v /       n),
			NF<Int32,  Int64>                               (value => v => v + value,                  v => n => v /       n),
			NF<Int64,  Int64>                               (value => v => v + value,                  v => n => v /       n),
			NF<UInt16, UInt64>                              (value => v => v + value,                  v => n => v / (uint)n),
			NF<UInt32, UInt64>                              (value => v => v + value,                  v => n => v / (uint)n),
			NF<UInt64, UInt64>                              (value => v => v + value,                  v => n => v / (uint)n),
			NF<SByte,  Int32>                               (value => v => v + value,                  v => n => v /       n),
			NF<Byte,   Int64>                               (value => v => v + value,                  v => n => v /       n),
			NF<Decimal,Decimal>                             (value => v => v + value,                  v => n => v /       n),
			NF<Double, Double>                              (value => v => v + value,                  v => n => v /       n),
			NF<Single, Single>                              (value => v => v + value,                  v => n => v /       n),

			NF<System.Data.SqlTypes.SqlInt16,Int64>         (value => v => v + value.Value,            v => n => v / n),
			NF<System.Data.SqlTypes.SqlInt32,Int64>         (value => v => v + value.Value,            v => n => v / n),
			NF<System.Data.SqlTypes.SqlInt64,Int64>         (value => v => v + value.Value,            v => n => v / n),
			NF<System.Data.SqlTypes.SqlByte, Int64>         (value => v => v + value.Value,            v => n => v / n),
			NF<System.Data.SqlTypes.SqlDecimal>             (value => v => (v.IsNull ? 0 : v) + value, v => n => v / n),
			NF<System.Data.SqlTypes.SqlDouble>              (value => v => (v.IsNull ? 0 : v) + value, v => n => v / n),
			NF<System.Data.SqlTypes.SqlSingle>              (value => v => (v.IsNull ? 0 : v) + value, v => n => v / n),

			NF<Oracle.DataAccess.       Types.OracleDecimal>(value => v => (v.IsNull ? 0 : v) + value, v => n => v / n),
			NF<Oracle.ManagedDataAccess.Types.OracleDecimal>(value => v => (v.IsNull ? 0 : v) + value, v => n => v / n),

			NF<IBM.Data.DB2Types.DB2Int16,       Int64>     (value => v => v + value.Value,            v => n => v / n),
			NF<IBM.Data.DB2Types.DB2Int32,       Int64>     (value => v => v + value.Value,            v => n => v / n),
			NF<IBM.Data.DB2Types.DB2Int64,       Int64>     (value => v => v + value.Value,            v => n => v / n),
			NF<IBM.Data.DB2Types.DB2Decimal,     Decimal>   (value => v => v + value.Value,            v => n => v / n),
			NF<IBM.Data.DB2Types.DB2DecimalFloat,Decimal>   (value => v => v + value.Value,            v => n => v / n),
			NF<IBM.Data.DB2Types.DB2Double,      Double>    (value => v => v + value.Value,            v => n => v / n),
			NF<IBM.Data.DB2Types.DB2Real,        Double>    (value => v => v + value.Value,            v => n => v / n),

			NF<Sybase.Data.AseClient.AseDecimal, Double>    (value => v => v + value.ToDouble(),       v => n => v / n),
			NF<Sap.Data.Hana.HanaDecimal,        Decimal>   (value => v => v + value.ToDecimal(),      v => n => v / n),

			NF<MySql.Data.Types.MySqlDecimal,Decimal>       (value => v => v + value.Value,            v => n => v / n),
		}
		.ToDictionary(f => f.Type));

		#region ValueFormatter

		static ValueFormatter VF<T>(Func<T,string> format, string font = null, string size = null, bool nowrap = true)
		{
			return new ValueFormatter<T> { Format = format, NoWrap = nowrap };
		}

		abstract class ValueFormatter
		{
			public abstract Type   Type { get; }

			public string Font;
			public string Size;
			public bool   NoWrap;

			public abstract string FormatValue(object value);
		}

		class ValueFormatter<T> : ValueFormatter
		{
			public override Type Type => typeof(T);

			public Func<T,string> Format;

			public override string FormatValue(object value)
			{
				return Format((T)value);
			}
		}

		#endregion

		#region NumberFormatter

		static NumberFormatter NF<T>(Func<T,Func<T,T>> add, Func<T,Func<int,object>> avr)
		{
			return new NumberFormatter<T> { Add = add, Avarege = avr, Format = v => new XElement("span", v) };
		}

		static NumberFormatter NF<T,TT>(Func<T,Func<TT,TT>> add, Func<TT,Func<int,object>> avr)
		{
			return new NumberFormatter<T,TT> { Add = add, Avarege = avr, Format = v => new XElement("span", v) };
		}

		abstract class NumberFormatter
		{
			public abstract Type Type { get; }

			public abstract void     AddTotal  (Total total, object value);
			public abstract XElement GetElement(object value);
		}

		class NumberFormatter<T> : NumberFormatter
		{
			public override Type Type => typeof(T);

			public Func<T,Func<T,T>>        Add;
			public Func<T,Func<int,object>> Avarege;

			public override void AddTotal(Total total, object value)
			{
				total.Add(Add((T)value), Avarege);
			}

			public Func<T,XElement> Format;

			public override XElement GetElement(object value)
			{
				return Format((T)value);
			}
		}

		class NumberFormatter<T,TT> : NumberFormatter
		{
			public override Type Type => typeof(T);

			public Func<T,Func<TT,TT>>       Add;
			public Func<TT,Func<int,object>> Avarege;

			public override void AddTotal(Total total, object value)
			{
				total.Add<TT>(Add((T)value), Avarege);
			}

			public Func<T,XElement> Format;

			public override XElement GetElement(object value)
			{
				return Format((T)value);
			}
		}

		#endregion
	}
}
