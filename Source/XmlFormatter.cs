using System;
using System.Collections;
using System.Data.SqlTypes;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Xml.Linq;

using LinqToDB.Extensions;
using LinqToDB.Mapping;

using LINQPad;

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

				if (!mappingSchema.IsScalarType(itemType))
				{
					var items = ((IEnumerable)objectToWrite).Cast<object>().ToList();
					var ed    = mappingSchema.GetEntityDescriptor(itemType);

					var headers = ed.Columns
						.Select(c =>
							new XElement("th",
								new XAttribute("title", c.MemberType),
								$"{c.MemberName}"));

					var totals   = Enumerable.Range(0, ed.Columns.Count).Select(_ => new Total()).ToList();
					var typeName = IsAnonymousType(itemType) ? "new" : itemType.Name;

					return Util.RawHtml(
						new XElement("div",
							new XAttribute("class", "spacer"),
							new XElement("table",
								new object[]
								{
									new XElement("tr",
										new XElement("td",
											new XAttribute("class",   "typeheader"),
											new XAttribute("colspan", ed.Columns.Count),
											$"{typeName} ({items.Count} items)")),
									new XElement("tr", headers)
								}
								.Union(items
									.Select(i => new XElement("tr", ed.Columns.Select((c,n) => FormatValue(totals[n], c.GetValue(i)))))
									.ToList())
								.Union(
									new object[]
									{
										new XElement("tr", totals.Select(t =>
											new XElement("td",
												new XAttribute("title", t.Value == null ? "Totals" : $"Total={t.Value}\r\nAverage={t.GetAverage(items.Count)}"),
												new XAttribute("class", "columntotal"),
												new XAttribute("style", "font-size:100%;"),
												t.Value))),
									}
									.Where(_ => totals.Any(v => v.Value != null))
									))));
			}}

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

		static XElement FormatValue(Total total, object value)
		{
			if (value == null || value is DBNull || value is INullable && ((INullable)value).IsNull)
				return new XElement("td", new XElement("i", "null"));

			var found = true;

			switch (Type.GetTypeCode(value.GetType()))
			{
				case TypeCode.Int16    : total.Add<Int64>  (v => v + (Int16)  value, v => n => v /       n); break;
				case TypeCode.Int32    : total.Add<Int64>  (v => v + (Int32)  value, v => n => v /       n); break;
				case TypeCode.Int64    : total.Add<Int64>  (v => v + (Int64)  value, v => n => v /       n); break;
				case TypeCode.UInt16   : total.Add<UInt64> (v => v + (UInt16) value, v => n => v / (uint)n); break;
				case TypeCode.UInt32   : total.Add<UInt64> (v => v + (UInt32) value, v => n => v / (uint)n); break;
				case TypeCode.UInt64   : total.Add<UInt64> (v => v + (UInt64) value, v => n => v / (uint)n); break;
				case TypeCode.SByte    : total.Add<Int32>  (v => v + (SByte)  value, v => n => v /       n); break;
				case TypeCode.Byte     : total.Add<Int64>  (v => v + (Byte)   value, v => n => v /       n); break;
				case TypeCode.Decimal  : total.Add<Decimal>(v => v + (Decimal)value, v => n => v /       n); break;
				case TypeCode.Double   : total.Add<Double> (v => v + (Double) value, v => n => v /       n); break;
				case TypeCode.Single   : total.Add<Single> (v => v + (Single) value, v => n => v /       n); break;
				default                : found = false; break;
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
	}
}
