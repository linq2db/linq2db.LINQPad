using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using LinqToDB.Extensions;
using LinqToDB.Mapping;
using LinqToDB.Reflection;

using LINQPad.Extensibility.DataContext;

namespace LinqToDB.LINQPad
{
	class SchemaGenerator
	{
		public SchemaGenerator(IConnectionInfo cxInfo, Type customType)
		{
			_cxInfo     = cxInfo;
			_customType = customType;
		}

		readonly IConnectionInfo _cxInfo;
		readonly Type            _customType;

		class TableInfo
		{
			public TableInfo(PropertyInfo propertyInfo)
			{
				PropertyInfo = propertyInfo;
				Name         = propertyInfo.Name;
				Type         = propertyInfo.PropertyType.GetItemType();
				TypeAccessor = TypeAccessor.GetAccessor(Type);

				var tableAttr = Type.GetCustomAttributeLike<TableAttribute>();

				if (tableAttr != null)
				{
					IsColumnAttributeRequired = tableAttr.IsColumnAttributeRequired;

					if (Extensions.HasProperty(tableAttr, "IsView"))
						IsView = tableAttr.IsView;
				}
			}

			public readonly PropertyInfo PropertyInfo;
			public readonly string       Name;
			public readonly Type         Type;
			public readonly bool         IsColumnAttributeRequired;
			public readonly TypeAccessor TypeAccessor;
			public readonly bool         IsView;
		}

		public IEnumerable<ExplorerItem> GetSchema()
		{
			var tables = _customType.GetProperties()
				.Where(p =>
					p.GetCustomAttributeLike<ObsoleteAttribute>() == null &&
					p.PropertyType.MaybeChildOf(typeof(IQueryable<>)))
				.Select(p => new TableInfo(p))
				.ToList();

			var items = new List<ExplorerItem>();

			if (tables.Any(t => !t.IsView)) items.Add(GetTables("Tables", ExplorerIcon.Table, tables.Where(t => !t.IsView)));
			if (tables.Any(t => t.IsView))  items.Add(GetTables("Views",  ExplorerIcon.View,  tables.Where(t =>  t.IsView)));

			return items;
		}

		ExplorerItem GetTables(string header, ExplorerIcon icon, IEnumerable<TableInfo> tableSource)
		{
			var tables = tableSource.ToList();
			var dic    = tables.ToDictionary(t => t.Type, t => new { table = t, item = GetTable(icon, t) });

			var items = new ExplorerItem(header, ExplorerItemKind.Category, icon)
			{
				Children = dic.Values.OrderBy(t => t.table.Name).Select(t => t.item).ToList()
			};

			foreach (var table in dic.Values)
			{
				var entry        = table.item;
				var typeAccessor = table.table.TypeAccessor;

				foreach (var ma in typeAccessor.Members)
				{
					var aa = ma.MemberInfo.GetCustomAttributeLike<AssociationAttribute>();

					if (aa != null)
					{
						var relationship = Extensions.HasProperty(aa, "Relationship") ? aa.Relationship : Relationship.OneToOne;
						var otherType    = relationship == Relationship.OneToMany ? ma.Type.GetItemType() : ma.Type;
						var otherTable   = dic.ContainsKey(otherType) ? dic[otherType] : null;
						var typeName     = relationship == Relationship.OneToMany ? $"List<{otherType.Name}>" : otherType.Name;

						entry.Children.Add(
							new ExplorerItem(
								ma.Name,
								relationship == Relationship.OneToMany
									? ExplorerItemKind.CollectionLink
									: ExplorerItemKind.ReferenceLink,
								relationship == Relationship.OneToMany
									? ExplorerIcon.OneToMany
									: relationship == Relationship.ManyToOne
										? ExplorerIcon.ManyToOne
										: ExplorerIcon.OneToOne)
							{
								DragText        = ma.Name,
								ToolTipText     = typeName + (aa.IsBackReference == true ? " // Back Reference" : ""),
								SqlName         = aa.KeyName,
								IsEnumerable    = ma.Type.MaybeChildOf(typeof(IEnumerable<>)) && !ma.Type.MaybeEqualTo(typeof(string)),
								HyperlinkTarget = otherTable?.item,
							});
					}
				}
			}

			return items;
		}

		ExplorerItem GetTable(ExplorerIcon icon, TableInfo table)
		{
			var columns =
			(
				from ma in table.TypeAccessor.Members
				let aa = ma.MemberInfo.GetCustomAttributeLike<AssociationAttribute>()
				where aa == null
				let ca = ma.MemberInfo.GetCustomAttributeLike<ColumnAttribute>()
				let id = ma.MemberInfo.GetCustomAttributeLike<IdentityAttribute>()
				let pk = ma.MemberInfo.GetCustomAttributeLike<PrimaryKeyAttribute>()
				where
					ca != null && ca.IsColumn ||
					pk != null ||
					id != null ||
					ca == null && !table.IsColumnAttributeRequired && MappingSchema.Default.IsScalarType(ma.Type)
				select new ExplorerItem(
					ma.Name,
					ExplorerItemKind.Property,
					pk != null || ca?.IsPrimaryKey ? ExplorerIcon.Key : ExplorerIcon.Column)
				{
					Text = $"{ma.Name} : {GetTypeName(ma.Type)}",
//					ToolTipText        = $"{sqlName} {column.ColumnType} {(column.IsNullable ? "NULL" : "NOT NULL")}{(column.IsIdentity ? " IDENTITY" : "")}",
					DragText = ma.Name,
//					SqlName            = sqlName,
//					SqlTypeDeclaration = $"{column.ColumnType} {(column.IsNullable ? "NULL" : "NOT NULL")}{(column.IsIdentity ? " IDENTITY" : "")}",
				}
			).ToList();

			var ret = new ExplorerItem(table.Name, ExplorerItemKind.QueryableObject, icon)
			{
				DragText = table.Name,
//				ToolTipText  = $"ITable<{t.TypeName}>",
				IsEnumerable = true,
				Children     = columns.ToList(),
			};

			return ret;
		}

		string GetTypeName(Type type)
		{
			switch (type.FullName)
			{
				case "System.Boolean" : return "bool";
				case "System.Byte"    : return "byte";
				case "System.SByte"   : return "sbyte";
				case "System.Byte[]"  : return "byte[]";
				case "System.Int16"   : return "short";
				case "System.Int32"   : return "int";
				case "System.Int64"   : return "long";
				case "System.UInt16"  : return "ushort";
				case "System.UInt32"  : return "uint";
				case "System.UInt64"  : return "ulong";
				case "System.Decimal" : return "decimal";
				case "System.Single"  : return "float";
				case "System.Double"  : return "double";
				case "System.String"  : return "string";
				case "System.Char"    : return "char";
				case "System.Object"  : return "object";
			}

			if (type.IsNullable())
				return GetTypeName(type.ToNullableUnderlying()) + '?';

			return type.Name;
		}

		/*public IEnumerable<ExplorerItem> GetItemsAndCode(string nameSpace, string typeName)
		{
			var schemas =
			(
				from t in
					(
						from t in _schema.Tables
						select new { t.IsDefaultSchema, t.SchemaName, Table = t, Procedure = (ProcedureSchema)null }
					)
					.Union
					(
						from p in _schema.Procedures
						select new { p.IsDefaultSchema, p.SchemaName, Table = (TableSchema)null, Procedure = p }
					)
				group t by new { t.IsDefaultSchema, t.SchemaName } into gr
				orderby !gr.Key.IsDefaultSchema, gr.Key.SchemaName
				select new
				{
					gr.Key,
					Tables     = gr.Where(t => t.Table     != null).Select(t => t.Table).    ToList(),
					Procedures = gr.Where(t => t.Procedure != null).Select(t => t.Procedure).ToList(),
				}
			)
			.ToList();

			foreach (var s in schemas)
			{
				var items = new List<ExplorerItem>();

				if (s.Tables.Any(t => !t.IsView && !t.IsProcedureResult))
					items.Add(GetTables("Tables", ExplorerIcon.Table, s.Tables.Where(t => !t.IsView && !t.IsProcedureResult)));

				if (s.Tables.Any(t => t.IsView))
					items.Add(GetTables("Views", ExplorerIcon.View, s.Tables.Where(t => t.IsView)));

				if (!_cxInfo.DynamicSchemaOptions.ExcludeRoutines && s.Procedures.Any(p => p.IsLoaded && !p.IsFunction))
					items.Add(GetProcedures(
						"Stored Procs",
						ExplorerIcon.StoredProc,
						s.Procedures.Where(p => p.IsLoaded && !p.IsFunction).ToList()));

				if (s.Procedures.Any(p => p.IsLoaded && p.IsTableFunction))
					items.Add(GetProcedures(
						"Table Functions",
						ExplorerIcon.TableFunction,
						s.Procedures.Where(p => p.IsLoaded && p.IsTableFunction).ToList()));

				if (s.Procedures.Any(p => p.IsFunction && !p.IsTableFunction))
					items.Add(GetProcedures(
						"Scalar Functions",
						ExplorerIcon.ScalarFunction,
						s.Procedures.Where(p => p.IsFunction && !p.IsTableFunction).ToList()));

				if (schemas.Count == 1)
				{
					foreach (var item in items)
						yield return item;
				}
				else
				{
					yield return new ExplorerItem(
						s.Key.SchemaName.IsNullOrEmpty() ? s.Key.IsDefaultSchema ? "(default)" : "empty" : s.Key.SchemaName,
						ExplorerItemKind.Schema,
						ExplorerIcon.Schema)
					{
						Children = items
					};
				}
			}
		}

		ExplorerItem GetColumnItem(ColumnSchema column)
		{
			var memberType = UseProviderSpecificTypes ? (column.ProviderSpecificType ?? column.MemberType) : column.MemberType;
			var sqlName    = (string)_sqlBuilder.Convert(column.ColumnName, ConvertType.NameToQueryField);

			return new ExplorerItem(
				column.MemberName,
				ExplorerItemKind.Property,
				column.IsPrimaryKey ? ExplorerIcon.Key : ExplorerIcon.Column)
			{
				Text               = $"{column.MemberName} : {memberType}",
				ToolTipText        = $"{sqlName} {column.ColumnType} {(column.IsNullable ? "NULL" : "NOT NULL")}{(column.IsIdentity ? " IDENTITY" : "")}",
				DragText           = column.MemberName,
				SqlName            = sqlName,
				SqlTypeDeclaration = $"{column.ColumnType} {(column.IsNullable ? "NULL" : "NOT NULL")}{(column.IsIdentity ? " IDENTITY" : "")}",
			};
		}

		ExplorerItem GetProcedures(string header, ExplorerIcon icon, List<ProcedureSchema> procedures)
		{
			var results = new HashSet<TableSchema>();

			var items = new ExplorerItem(header, ExplorerItemKind.Category, icon)
			{
				Children = procedures
					.Select(p =>
					{
						var sprocSqlName = _sqlBuilder.BuildTableName(
							new StringBuilder(),
							null,
							p.SchemaName == null ? null : (string)_sqlBuilder.Convert(p.SchemaName, ConvertType.NameToOwner),
							(string)_sqlBuilder.Convert(p.ProcedureName,  ConvertType.NameToQueryTable)).ToString();

						var memberName = p.MemberName;

						if (p.IsFunction && !p.IsTableFunction)
						{
							var res = p.Parameters.FirstOrDefault(pr => pr.IsResult);

							if (res != null)
								memberName += $" -> {res.ParameterType}";
						}

						var ret = new ExplorerItem(memberName, ExplorerItemKind.QueryableObject, icon)
						{
							DragText     = $"{p.MemberName}(" +
								p.Parameters
									.Where (pr => !pr.IsResult)
									.Select(pr => $"{(pr.IsOut ? pr.IsIn ? "ref " : "out " : "")}{pr.ParameterName}")
									.Join(", ") +
								")",
							SqlName      = sprocSqlName,
							IsEnumerable = p.ResultTable != null,
							Children     = p.Parameters
								.Where (pr => !pr.IsResult)
								.Select(pr =>
									new ExplorerItem(
										$"{pr.ParameterName} ({pr.ParameterType})",
										ExplorerItemKind.Parameter,
										ExplorerIcon.Parameter))
								.Union(p.ResultTable?.Columns.Select(GetColumnItem) ?? new ExplorerItem[0])
								.ToList(),
						};

						if (p.ResultTable != null && !results.Contains(p.ResultTable))
						{
							results.Add(p.ResultTable);
						}

						return ret;
					})
					.OrderBy(p => p.Text)
					.ToList(),
			};

			return items;
		}

		*/
	}
}
