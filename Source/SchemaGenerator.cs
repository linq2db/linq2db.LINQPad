using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Media.Animation;

using LinqToDB.SchemaProvider;
using LinqToDB.SqlProvider;

using LINQPad.Extensibility.DataContext;

namespace LinqToDB.LINQPad
{
	class SchemaGenerator
	{
		public SchemaGenerator(IConnectionInfo cxInfo, Type customType)
		{
			_cxInfo = cxInfo;
			_customType = customType;
		}

		readonly IConnectionInfo _cxInfo;
		readonly Type            _customType;

		public IEnumerable<ExplorerItem> GetSchema()
		{
			var tables = _customType.GetProperties()
				.Where(p =>
					!p.GetCustomAttributes().Any(a => IsSame(a.GetType(), typeof(ObsoleteAttribute))) &&
					IsIQueriable(p.PropertyType))
				.ToList();

			var items = new List<ExplorerItem>();

			if (tables.Any())
				items.Add(GetTables("Tables", ExplorerIcon.Table, tables));

			return items;
		}

		bool IsSame(Type type1, Type type2)
		{
			return type1.FullName == type2.FullName;
		}

		bool IsIQueriable(Type type)
		{
			do
			{
				if (type.IsGenericType)
				{
					var gtype = type.GetGenericTypeDefinition();

					if (IsSame(gtype, typeof(IQueryable<>)))
						return true;
				}

				foreach (var inf in type.GetInterfaces())
					if (IsIQueriable(inf))
						return true;

				type = type.BaseType;
				
			} while(type != null);

			return false;
		}

		ExplorerItem GetTables(string header, ExplorerIcon icon, IEnumerable<PropertyInfo> tableSource)
		{
			var tables = tableSource.ToList();
			var dic    = new Dictionary<TableSchema,ExplorerItem>();

			var items = new ExplorerItem(header, ExplorerItemKind.Category, icon)
			{
				Children = tables
					.Select(t =>
					{
						var memberName = t.Name;

//						var tableSqlName = _sqlBuilder.BuildTableName(
//							new StringBuilder(),
//							null,
//							t.SchemaName == null ? null : (string)_sqlBuilder.Convert(t.SchemaName, ConvertType.NameToOwner),
//							(string)_sqlBuilder.Convert(t.TableName,  ConvertType.NameToQueryTable)).ToString();

						//Debug.WriteLine($"Table: [{t.SchemaName}].[{t.TableName}] - ${tableSqlName}");

						var ret = new ExplorerItem(memberName, ExplorerItemKind.QueryableObject, icon)
						{
							DragText     = memberName,
//							ToolTipText  = $"ITable<{t.TypeName}>",
//							SqlName      = tableSqlName,
							IsEnumerable = true,
//							Children     = t.Columns.Select(GetColumnItem).ToList()
						};

						return ret;
					})
					.OrderBy(t => t.Text)
					.ToList()
			};

//			foreach (var table in tables.Where(t => dic.ContainsKey(t)))
//			{
//				var entry = dic[table];
//
//				foreach (var key in table.ForeignKeys)
//				{
//					var typeName = key.AssociationType == AssociationType.OneToMany
//						? $"List<{key.OtherTable.TypeName}>"
//						: key.OtherTable.TypeName;
//
//					entry.Children.Add(
//						new ExplorerItem(
//							key.MemberName,
//							key.AssociationType == AssociationType.OneToMany
//								? ExplorerItemKind.CollectionLink
//								: ExplorerItemKind.ReferenceLink,
//							key.AssociationType == AssociationType.OneToMany
//								? ExplorerIcon.OneToMany
//								: key.AssociationType == AssociationType.ManyToOne
//									? ExplorerIcon.ManyToOne
//									: ExplorerIcon.OneToOne)
//						{
//							DragText        = key.MemberName,
//							ToolTipText     = typeName + (key.BackReference == null ? " // Back Reference" : ""),
//							SqlName         = key.KeyName,
//							IsEnumerable    = key.AssociationType == AssociationType.OneToMany,
//							HyperlinkTarget = dic.ContainsKey(key.OtherTable) ? dic[key.OtherTable] : null,
//						});
//				}
//			}

			return items;
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
