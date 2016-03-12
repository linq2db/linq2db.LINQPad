using System;
using System.Collections;
using System.Data.SqlTypes;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

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

			if (objectToWrite is DBNull || objectToWrite is INullable && ((INullable)objectToWrite).IsNull)
			{
				return null;
			}

			if (objectToWrite is SqlDecimal)
			{
				var value = (SqlDecimal)objectToWrite;
				return Util.RawHtml($"<div class=\"n\">{value}</div>");
			}

			var type = objectToWrite.GetType();

			if (objectToWrite is IEnumerable)
			{
				var itemType = type.GetItemType();
				var items    = ((IEnumerable)objectToWrite).Cast<object>().ToList();
				var tableID  = ++_id;

				var columns = mappingSchema.IsScalarType(itemType) ?
					new[]
					{
						new
						{
							MemberType = itemType,
							MemberName = "",
							GetValue = (Func<object,object>)(v => v),
							Total    = new Total(),
						}
					}
					:
					mappingSchema.GetEntityDescriptor(itemType).Columns
						.Select(c => new
						{
							c.MemberType,
							c.MemberName,
							GetValue = (Func<object,object>)c.GetValue,
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
								.Select(i => new XElement("tr", columns.Select(c => FormatValue(c.Total, c.GetValue(i)))))
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

		static XElement FormatValue(Total total, object value)
		{
			if (value == null || value is DBNull || value is INullable && ((INullable)value).IsNull)
				return new XElement("td", new XAttribute("style", "text-align:center;"), new XElement("i", "null"));

			var found = true;

			switch (Type.GetTypeCode(value.GetType()))
			{
				case TypeCode.Int16   : total.Add<Int64>  (v => v + (Int16)  value, v => n => v /       n); break;
				case TypeCode.Int32   : total.Add<Int64>  (v => v + (Int32)  value, v => n => v /       n); break;
				case TypeCode.Int64   : total.Add<Int64>  (v => v + (Int64)  value, v => n => v /       n); break;
				case TypeCode.UInt16  : total.Add<UInt64> (v => v + (UInt16) value, v => n => v / (uint)n); break;
				case TypeCode.UInt32  : total.Add<UInt64> (v => v + (UInt32) value, v => n => v / (uint)n); break;
				case TypeCode.UInt64  : total.Add<UInt64> (v => v + (UInt64) value, v => n => v / (uint)n); break;
				case TypeCode.SByte   : total.Add<Int32>  (v => v + (SByte)  value, v => n => v /       n); break;
				case TypeCode.Byte    : total.Add<Int64>  (v => v + (Byte)   value, v => n => v /       n); break;
				case TypeCode.Decimal : total.Add<Decimal>(v => v + (Decimal)value, v => n => v /       n); break;
				case TypeCode.Double  : total.Add<Double> (v => v + (Double) value, v => n => v /       n); break;
				case TypeCode.Single  : total.Add<Single> (v => v + (Single) value, v => n => v /       n); break;
				default               : found = false; break;
			}

			if (!found)
			{
				if (value is SqlDateTime)
					value = ((SqlDateTime)value).Value;

				if (value is DateTime)
				{
					var dt = (DateTime)value;

					return new XElement("td",
						new XAttribute("nowrap", "nowrap"),
						dt.Hour == 0 && dt.Minute == 0 && dt.Second == 0
							? dt.ToString("yyyy-MM-dd")
							: dt.ToString("yyyy-MM-dd HH:mm:ss"));
				}

				if (value is SqlGuid)
					value = ((SqlGuid)value).Value;

				if (value is Guid)
				{
					return new XElement("td",
						new XAttribute("nowrap", "nowrap"),
						new XAttribute("style",  "font-family:consolas;font-size:110%;"),
						((Guid)value).ToString("B").ToUpper());
				}

				     if (value is SqlDecimal) total.Add<SqlDecimal>(v => (v.IsNull ? 0 : v) + (SqlDecimal)value, v => n => v / n);
				else if (value is SqlDouble)  total.Add<SqlDouble> (v => (v.IsNull ? 0 : v) + (SqlDouble) value, v => n => v / n);
				else if (value is SqlSingle)  total.Add<SqlSingle> (v => (v.IsNull ? 0 : v) + (SqlSingle) value, v => n => v / n);
				else if (value is SqlInt16)   total.Add<SqlInt64>  (v => (v.IsNull ? 0 : v) + (SqlInt16)  value, v => n => v / n);
				else if (value is SqlInt32)   total.Add<SqlInt64>  (v => (v.IsNull ? 0 : v) + (SqlInt32)  value, v => n => v / n);
				else if (value is SqlInt64)   total.Add<SqlInt64>  (v => (v.IsNull ? 0 : v) + (SqlInt64)  value, v => n => v / n);
				else if (value is SqlByte)    total.Add<SqlInt64>  (v => (v.IsNull ? 0 : v) + (SqlByte)   value, v => n => v / n);
			}

			return total.IsNumber ?
				new XElement("td", new XAttribute("class", "n"), value) :
				new XElement("td", value);
		}

		public static object FormatValue(object value, ObjectGraphInfo info)
		{
			if (value == null || value is DBNull || value is INullable && ((INullable)value).IsNull)
				return Util.RawHtml(new XElement("span", new XAttribute("style", "text-align:center;"), new XElement("i", "null")));

			if (value is SqlDateTime)
				value = ((SqlDateTime)value).Value;

			if (value is DateTime)
			{
				var dt = (DateTime)value;

				return Util.RawHtml(new XElement("span",
					new XAttribute("style", "white-space:nowrap;"),
					dt.Hour == 0 && dt.Minute == 0 && dt.Second == 0
						? dt.ToString("yyyy-MM-dd")
						: dt.ToString("yyyy-MM-dd HH:mm:ss")));
			}

			if (value is SqlGuid)
				value = ((SqlGuid)value).Value;

			if (value is Guid)
			{
				return Util.RawHtml(new XElement("span",
					new XAttribute("style", "white-space:nowrap;font-family:consolas;font-size:110%;"),
					((Guid)value).ToString("B").ToUpper()));
			}

			if (value is SqlDecimal)
			{
//				var sb = new StringBuilder();
//
//				sb
//					.AppendLine(info.ToString())
//					.AppendLine(info.Heading);
//
//				foreach (var parent in info.ParentHierarchy)
//				{
//					sb.AppendLine(parent.ToString());
//				}
//
//				MessageBox.Show(sb.ToString());

				return Util.RawHtml(new XElement("span", value));
			}

			if (value is SqlDouble)  return Util.RawHtml(new XElement("span", value));
			if (value is SqlSingle)  return Util.RawHtml(new XElement("span", value));
			if (value is SqlInt16)   return Util.RawHtml(new XElement("span", value));
			if (value is SqlInt32)   return Util.RawHtml(new XElement("span", value));
			if (value is SqlInt64)   return Util.RawHtml(new XElement("span", value));
			if (value is SqlByte)    return Util.RawHtml(new XElement("span", value));

			return value;
		}
	}
}
