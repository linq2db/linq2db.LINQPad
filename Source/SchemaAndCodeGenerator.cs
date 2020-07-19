using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using CodeJam.Strings;
using CodeJam.Xml;

using LinqToDB.Data;
using LinqToDB.DataProvider;
using LinqToDB.SchemaProvider;
using LinqToDB.SqlProvider;

using LINQPad.Extensibility.DataContext;

namespace LinqToDB.LINQPad
{
	partial class SchemaAndCodeGenerator
	{
		public SchemaAndCodeGenerator(IConnectionInfo cxInfo)
		{
			_cxInfo = cxInfo;

			UseProviderSpecificTypes = ((string?)_cxInfo.DriverData.Element("useProviderSpecificTypes"))?.ToLower() == "true";
			NormalizeJoins           = ((string?)_cxInfo.DriverData.Element("normalizeNames"))          ?.ToLower() == "true";
			AllowMultipleQuery       = ((string?)_cxInfo.DriverData.Element("allowMultipleQuery"))      ?.ToLower() == "true";
			ProviderName             =  (string?)_cxInfo.DriverData.Element("providerName");
			CommandTimeout           = _cxInfo.DriverData.ElementValueOrDefault("commandTimeout", str => str.ToInt32() ?? 0, 0);
		}

		public readonly int          CommandTimeout;
		public readonly bool         UseProviderSpecificTypes;
		public readonly string?      ProviderName;
		public readonly bool         NormalizeJoins;
		public readonly bool         AllowMultipleQuery;
		public readonly List<string> References = new List<string>();

		readonly IConnectionInfo _cxInfo;
		readonly StringBuilder   _classCode = new StringBuilder();

		DatabaseSchema? _schema;
		IDataProvider?  _dataProvider;
		ISqlBuilder?    _sqlBuilder;

		public readonly StringBuilder Code = new StringBuilder();

		private readonly HashSet<string> _existingMemberNames = new HashSet<string>(StringComparer.InvariantCulture);

		public IEnumerable<ExplorerItem> GetItemsAndCode(string nameSpace, string typeName)
		{
			typeName = ConvertToCompilable(typeName, false);

			var connectionString = _cxInfo.DatabaseInfo.CustomCxString;
			var providerInfo     = ProviderHelper.GetProvider(ProviderName);
			var provider         = providerInfo.GetDataProvider(connectionString);

			using (var db = new DataConnection(provider, connectionString))
			{
				db.CommandTimeout = CommandTimeout;

				_dataProvider = db.DataProvider;
				_sqlBuilder   = _dataProvider.CreateSqlBuilder(_dataProvider.MappingSchema);

				var options = new GetSchemaOptions();

				var includeSchemas = (string?)_cxInfo.DriverData.Element("includeSchemas");
				if (includeSchemas != null) options.IncludedSchemas = includeSchemas.Split(',', ';');

				var excludeSchemas = (string?)_cxInfo.DriverData.Element("excludeSchemas");
				if (excludeSchemas != null) options.ExcludedSchemas = excludeSchemas.Split(',', ';');

				var includeCatalogs = (string?)_cxInfo.DriverData.Element("includeCatalogs");
				if (includeCatalogs != null) options.IncludedCatalogs = includeCatalogs.Split(',', ';');

				var excludeCatalogs = (string?)_cxInfo.DriverData.Element("excludeCatalogs");
				if (excludeCatalogs != null) options.ExcludedCatalogs = excludeCatalogs.Split(',', ';');

				options.GetProcedures  = (string?)_cxInfo.DriverData.Element("excludeRoutines") != "true";
				options.GetForeignKeys = (string?)_cxInfo.DriverData.Element("excludeFKs")      != "true";

				_schema = _dataProvider.GetSchemaProvider().GetSchema(db, options);

				ConvertSchema(typeName);
			}

			Code
				.AppendLine("using System;")
				.AppendLine("using System.Collections;")
				.AppendLine("using System.Collections.Generic;")
				.AppendLine("using System.Data;")
				.AppendLine("using System.Reflection;")
				.AppendLine("using System.Linq;")
				.AppendLine("using LinqToDB;")
				.AppendLine("using LinqToDB.Common;")
				.AppendLine("using LinqToDB.Data;")
				.AppendLine("using LinqToDB.Mapping;")
				.AppendLine("using System.Net;")
				.AppendLine("using System.Net.NetworkInformation;")
				;

			if (_schema.Procedures.Any(_ => _.IsAggregateFunction))
				Code
					.AppendLine("using System.Linq.Expressions;")
					;

			if (_schema.ProviderSpecificTypeNamespace.NotNullNorWhiteSpace())
				Code.AppendLine($"using {_schema.ProviderSpecificTypeNamespace};");

			References.AddRange(providerInfo.GetAssemblyLocation(connectionString));
			if (providerInfo.Provider.AdditionalNamespaces != null)
				foreach (var ns in providerInfo.Provider.AdditionalNamespaces)
				{
					Code.AppendLine($"using {ns};");
				}

			Code
				.AppendLine($"namespace {nameSpace}")
				.AppendLine( "{")
				.AppendLine($"  public class {typeName} : LinqToDB.LINQPad.LINQPadDataConnection")
				.AppendLine( "  {")
				.AppendLine($"    public {typeName}(string provider, string connectionString)")
				.AppendLine( "      : base(provider, connectionString)")
				.AppendLine( "    {")
				.AppendLine($"      CommandTimeout = {CommandTimeout};")
				.AppendLine( "    }")
				.AppendLine($"    public {typeName}()")
				.AppendLine($"      : base({CSharpTools.ToStringLiteral(ProviderName)}, {CSharpTools.ToStringLiteral(connectionString)})")
				.AppendLine( "    {")
				.AppendLine($"      CommandTimeout = {CommandTimeout};")
				.AppendLine( "    }")
				;

			if (ProviderName == LinqToDB.ProviderName.PostgreSQL)
				PreprocessPostgreSQLSchema();

			var schemas =
			(
				from t in
					(
						from t in _schema.Tables.Where(t => !t.IsProcedureResult)
						select new { t.IsDefaultSchema, SchemaName = t.SchemaName.IsNullOrEmpty() ? null : t.SchemaName, Table = t, Procedure = (ProcedureSchema?)null }
					)
					.Union
					(
						from p in _schema.Procedures
						select new { p.IsDefaultSchema, SchemaName = p.SchemaName.IsNullOrEmpty() ? null : p.SchemaName, Table = (TableSchema?)null, Procedure = p }
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

			var nonDefaultSchemas = new Dictionary<string, ExplorerItem>();

			var     hasDefaultSchema  = false;
			string? defaultSchemaName = null;

			foreach (var s in schemas)
			{
				if (s.Key.IsDefaultSchema)
				{
					hasDefaultSchema    = true;
					defaultSchemaName ??= s.Key.SchemaName;
				}
				else
				{
					var name = s.Key.SchemaName.IsNullOrEmpty() ? "empty" : s.Key.SchemaName;
					nonDefaultSchemas.Add(name, new ExplorerItem(name, ExplorerItemKind.Schema, ExplorerIcon.Schema) { Children = new List<ExplorerItem>() });
				}
			}

			var useSchemaNode = nonDefaultSchemas.Count > 1 || nonDefaultSchemas.Count == 1 && hasDefaultSchema;
			var defaultSchema = useSchemaNode ? new ExplorerItem(defaultSchemaName ?? "(default)", ExplorerItemKind.Schema, ExplorerIcon.Schema) { Children = new List<ExplorerItem>() } : null;

			foreach (var s in schemas)
			{
				var items = new List<ExplorerItem>();

				if (s.Tables.Any(t => !t.IsView))
					items.Add(GetTables("Tables", ExplorerIcon.Table, s.Tables.Where(t => !t.IsView)));

				if (s.Tables.Any(t => t.IsView))
					items.Add(GetTables("Views", ExplorerIcon.View, s.Tables.Where(t => t.IsView)));

				if (!_cxInfo.DynamicSchemaOptions.ExcludeRoutines && s.Procedures.Any(p => p.IsLoaded && !p.IsFunction))
					items.Add(GetProcedures(
						"Stored Procedures",
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

				if (!useSchemaNode)
				{
					foreach (var item in items)
						yield return item;
				}
				else
				{
					if (s.Key.IsDefaultSchema)
						defaultSchema!.Children.AddRange(items);
					else
						nonDefaultSchemas[s.Key.SchemaName ?? "empty"].Children.AddRange(items);
				}
			}

			if (defaultSchema != null)
				yield return defaultSchema;

			foreach (var schemaNode in nonDefaultSchemas.Values)
				yield return schemaNode;

			Code
				.AppendLine("  }")
				.AppendLine(_classCode.ToString())
				.AppendLine("}")
				;

#if DEBUG
			Debug.WriteLine(Code.ToString());
#endif
		}

		void CodeProcedure(StringBuilder code, ProcedureSchema p, string sprocSqlName)
		{
			code.AppendLine();

			var spName = CSharpTools.ToStringLiteral(sprocSqlName);

			if (p.IsTableFunction)
			{
				code.Append($"    [Sql.TableFunction(Name={CSharpTools.ToStringLiteral(p.ProcedureName)}");

				if (p.SchemaName != null)
					code.Append($", Schema={CSharpTools.ToStringLiteral(p.SchemaName)})");

				code
					.AppendLine("]")
					.Append($"    public ITable<{p.ResultTable?.TypeName}>")
					;
			}
			else if (p.IsAggregateFunction)
			{
				var inputs = p.Parameters.Where(pr => !pr.IsResult).ToArray();
				p.Parameters.RemoveAll(parameter => !parameter.IsResult);

				p.Parameters.Add(new ParameterSchema()
				{
					ParameterType = "IEnumerable<TSource>",
					ParameterName = "src"
				});

				foreach (var input in inputs.Where(pr => !pr.IsResult))
					p.Parameters.Add(new ParameterSchema()
					{
						ParameterType = $"Expression<Func<TSource, {input.ParameterType}>>",
						ParameterName = $"{input.ParameterName}"
					});

				p.MemberName += "<TSource>";

				code
					.Append($"    [Sql.Function(Name={spName}, ServerSideOnly=true, IsAggregate = true")
					.Append(inputs.Length > 0 ? $", ArgIndices = new[] {{ {string.Join(", ", Enumerable.Range(0, inputs.Length))} }}" : string.Empty)
					.AppendLine(")]")
					.Append($"    public static {p.Parameters.Single(pr => pr.IsResult).ParameterType}");
			}
			else if (p.IsFunction)
			{
				code
					.AppendLine($"    [Sql.Function(Name={spName}, ServerSideOnly=true)]")
					.Append    ($"    public static {p.Parameters.Single(pr => pr.IsResult).ParameterType}");
			}
			else
			{
				var type = p.ResultTable == null ? "int" : $"IEnumerable<{p.ResultTable.TypeName}>";

				code.Append($"    public {type}");
			}

			code
				.Append($" {p.MemberName}(")
				.Append(p.Parameters
					.Where (pr => !pr.IsResult || !p.IsFunction)
					.Select(pr => $"{(pr.IsOut || pr.IsResult ? pr.IsIn ? "ref " : "out " : "")}{pr.ParameterType} {pr.ParameterName}")
					.Join(", "))
				.AppendLine(")")
				.AppendLine("    {")
				;

			if (p.IsTableFunction)
			{
				code
					.Append    ($"      return this.GetTable<{p.ResultTable?.TypeName}>(this, (MethodInfo)MethodBase.GetCurrentMethod()")
					.AppendLine(p.Parameters.Count == 0 ? ");" : ",")
					;

				for (var i = 0; i < p.Parameters.Count; i++)
					code.AppendLine($"        {p.Parameters[i].ParameterName}{(i + 1 == p.Parameters.Count ? ");" : ",")}");
			}
			else if (p.IsFunction)
			{
				// aggregate and scalar functions branch
				code.AppendLine("      throw new InvalidOperationException();");
			}
			else
			{
				var inputParameters      = p.Parameters.Where(pp => pp.IsIn)                            .ToList();
				var outputParameters     = p.Parameters.Where(pp => pp.IsOut || pp.IsResult)            .ToList();
				var inOrOutputParameters = p.Parameters.Where(pp => pp.IsIn  || pp.IsOut || pp.IsResult).ToList();

				spName += inOrOutputParameters.Count == 0 ? ");" : ",";

				var retName = "__ret__";
				var retNo   = 0;

				while (p.Parameters.Any(pp => pp.ParameterName == retName))
					retName = "__ret__" + ++retNo;

				var hasOut = outputParameters.Any(pr => pr.IsOut || pr.IsResult);
				var prefix = hasOut ? $"var {retName} =" : "return";

				if (p.ResultTable == null)
				{
					code.Append($"      {prefix} this.ExecuteProc({spName}");
				}
				else
				{
					var hashSet      = new HashSet<string>();
					var hasDuplicate = false;

					foreach (var column in p.ResultTable.Columns)
					{
						if (hashSet.Contains(column.ColumnName))
						{
							hasDuplicate = true;
							break;
						}

						hashSet.Add(column.ColumnName);
					}

					if (hasDuplicate)
					{
						code.AppendLine( "      var ms = this.MappingSchema;");
						code.AppendLine($"      {prefix} this.QueryProc(dataReader =>");
						code.AppendLine($"        new {p.ResultTable.TypeName}");
						code.AppendLine( "        {");

						var n = 0;

						foreach (var c in p.ResultTable.Columns)
						{
							code.AppendLine($"          {c.MemberName} = Converter.ChangeTypeTo<{c.MemberType}>(dataReader.GetValue({n++}), ms),");
						}

						code.AppendLine( "        },");
						code.AppendLine($"        {spName}");
					}
					else
					{
						code.AppendLine($"      {prefix} this.QueryProc<{p.ResultTable.TypeName}>({spName}");
					}
				}

				for (var i = 0; i < inOrOutputParameters.Count; i++)
				{
					var pr = inOrOutputParameters[i];

					var str = string.Format(
						!pr.IsIn && (pr.IsOut || pr.IsResult)
							? "        new DataParameter({0}, null, DataType.{2})"
							: "        new DataParameter({0}, {1}, DataType.{2})",
						CSharpTools.ToStringLiteral(pr.SchemaName),
						pr.ParameterName,
						pr.DataType);

					if (pr.IsOut || pr.IsResult)
					{
						str += " { Direction = " + (pr.IsIn ? "ParameterDirection.InputOutput" : (pr.IsResult ? "ParameterDirection.ReturnValue" : "ParameterDirection.Output"));

						if (pr.Size != null && pr.Size.Value != 0)
							str += ", Size = " + pr.Size.Value;

						str += " }";
					}

					// we need to call ToList(), because otherwise output parameters will not be updated
					// with values. See https://msdn.microsoft.com/en-us/library/ms971497#gazoutas_topic6
					str += i + 1 == inOrOutputParameters.Count ? (outputParameters.Count > 0 && p.ResultTable != null ? ").ToList();" : ");") : ",";

					code.AppendLine(str);
				}

				if (hasOut)
				{
					code.AppendLine();

					foreach (var pr in p.Parameters.Where(_ => _.IsOut || _.IsResult))
					{
						var str = $"      {pr.ParameterName} = Converter.ChangeTypeTo<{pr.ParameterType}>(((IDbDataParameter)this.Command.Parameters[\"{pr.SchemaName}\"]).Value);";
						code.AppendLine(str);
					}

					code
						.AppendLine()
						.AppendLine($"      return {retName};")
						;
				}
			}

			code.AppendLine("    }");
		}

		ExplorerItem GetColumnItem(ColumnSchema column)
		{
			var memberType = UseProviderSpecificTypes ? (column.ProviderSpecificType ?? column.MemberType) : column.MemberType;
			var sqlName    = _sqlBuilder!.ConvertInline(column.ColumnName ?? "unspecified", ConvertType.NameToQueryField);

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
						var sprocSqlName = _sqlBuilder!.BuildTableName(
							new StringBuilder(),
							null,
							null,
							p.SchemaName == null ? null : _sqlBuilder.ConvertInline(p.SchemaName, ConvertType.NameToSchema),
							_sqlBuilder.ConvertInline(p.ProcedureName,  ConvertType.NameToQueryTable)).ToString();

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
									.Where (pr => !pr.IsResult || !p.IsFunction)
									.Select(pr => $"{(pr.IsOut || pr.IsResult ? (pr.IsIn ? "ref " : "out ") : "")}{pr.ParameterName}")
									.Join(", ") +
								")",
							SqlName      = sprocSqlName,
							IsEnumerable = p.ResultTable != null,
							Children     = p.Parameters
								.Where (pr => !pr.IsResult || !p.IsFunction)
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

							CodeTable(Code, p.ResultTable, false);
						}

						CodeProcedure(Code, p, sprocSqlName);

						return ret;
					})
					.OrderBy(p => p.Text)
					.ToList(),
			};

			return items;
		}

		void CodeTable(StringBuilder classCode, TableSchema table, bool addTableAttribute)
		{
			classCode.AppendLine();

			table.TypeName = GetName(_existingMemberNames, table.TypeName);

			if (addTableAttribute)
			{
				classCode.Append($"  [Table(Name={CSharpTools.ToStringLiteral(table.TableName)}");

				if (table.SchemaName.NotNullNorEmpty())
					classCode.Append($", Schema={CSharpTools.ToStringLiteral(table.SchemaName)}");

				classCode.AppendLine(")]");
			}

			classCode
				.AppendLine($"  public class {table.TypeName}")
				.AppendLine( "  {")
				;

			foreach (var c in table.Columns)
			{
				classCode
					.Append($"    [Column({CSharpTools.ToStringLiteral(c.ColumnName)}), ")
					.Append(c.IsNullable ? "Nullable" : "NotNull");

				if (c.IsPrimaryKey) classCode.Append($", PrimaryKey({c.PrimaryKeyOrder})");
				if (c.IsIdentity) classCode.Append(", Identity");

				classCode.AppendLine("]");

				var memberType = UseProviderSpecificTypes ? (c.ProviderSpecificType ?? c.MemberType) : c.MemberType;

				classCode.AppendLine($"    public {memberType} {c.MemberName} {{ get; set; }}");
			}

			foreach (var key in table.ForeignKeys)
			{
				classCode
					.Append( "    [Association(")
					.Append($"ThisKey={CSharpTools.ToStringLiteral((key.ThisColumns.Select(c => c.MemberName)).Join(", "))}")
					.Append($", OtherKey={CSharpTools.ToStringLiteral((key.OtherColumns.Select(c => c.MemberName)).Join(", "))}")
					.Append($", CanBeNull={(key.CanBeNull ? "true" : "false")}")
					;

				if (key.BackReference != null)
				{
					if (key.KeyName.NotNullNorEmpty())
						classCode.Append($", KeyName={CSharpTools.ToStringLiteral(key.KeyName)}");
					if (key.BackReference.KeyName.NotNullNorEmpty())
						classCode.Append($", BackReferenceName={CSharpTools.ToStringLiteral(key.BackReference.MemberName)}");
				}
				else
				{
					classCode.Append(", IsBackReference=true");
				}

				classCode.AppendLine(")]");

				var typeName = key.AssociationType == AssociationType.OneToMany
					? $"List<{key.OtherTable.TypeName}>"
					: key.OtherTable.TypeName;

				classCode.AppendLine($"    public {typeName} {key.MemberName} {{ get; set; }}");
			}

			classCode.AppendLine("  }");
		}

		ExplorerItem GetTables(string header, ExplorerIcon icon, IEnumerable<TableSchema> tableSource)
		{
			var tables = tableSource.ToList();
			var dic    = new Dictionary<TableSchema,ExplorerItem>();

			var items = new ExplorerItem(header, ExplorerItemKind.Category, icon)
			{
				Children = tables
					.Select(t =>
					{
						var memberName = _contextMembers[t];

						Code.AppendLine($"    public ITable<{t.TypeName}> {memberName} {{ get {{ return this.GetTable<{t.TypeName}>(); }} }}");

						CodeTable(_classCode, t, true);

						var tableSqlName = _sqlBuilder!.BuildTableName(
							new StringBuilder(),
							null,
							null,
							t.SchemaName == null ? null : _sqlBuilder.ConvertInline(t.SchemaName, ConvertType.NameToSchema),
							_sqlBuilder.ConvertInline(t.TableName!,  ConvertType.NameToQueryTable)).ToString();

						//Debug.WriteLine($"Table: [{t.SchemaName}].[{t.TableName}] - ${tableSqlName}");

						var ret = new ExplorerItem(memberName, ExplorerItemKind.QueryableObject, icon)
						{
							DragText     = memberName,
							ToolTipText  = $"ITable<{t.TypeName}>",
							SqlName      = tableSqlName,
							IsEnumerable = true,
							Children     = t.Columns.Select(GetColumnItem).ToList()
						};

						dic[t] = ret;

						return ret;
					})
					.OrderBy(t => t.Text)
					.ToList()
			};

			foreach (var table in tables.Where(t => dic.ContainsKey(t)))
			{
				var entry = dic[table];

				foreach (var key in table.ForeignKeys)
				{
					var typeName = key.AssociationType == AssociationType.OneToMany
						? $"List<{key.OtherTable.TypeName}>"
						: key.OtherTable.TypeName;

					entry.Children.Add(
						new ExplorerItem(
							key.MemberName,
							key.AssociationType == AssociationType.OneToMany
								? ExplorerItemKind.CollectionLink
								: ExplorerItemKind.ReferenceLink,
							key.AssociationType == AssociationType.OneToMany
								? ExplorerIcon.OneToMany
								: key.AssociationType == AssociationType.ManyToOne
									? ExplorerIcon.ManyToOne
									: ExplorerIcon.OneToOne)
						{
							DragText        = key.MemberName,
							ToolTipText     = typeName + (key.BackReference == null ? " // Back Reference" : ""),
							SqlName         = key.KeyName,
							IsEnumerable    = key.AssociationType == AssociationType.OneToMany,
							HyperlinkTarget = dic.ContainsKey(key.OtherTable) ? dic[key.OtherTable] : null,
						});
				}
			}

			return items;
		}

		static string GetUniqueName(HashSet<string> names, string proposedName)
		{
			var name = proposedName;
			var n    = 0;

			while (names.Contains(name))
				name = proposedName + ++n;

			names.Add(name);

			return name;
		}

		static string GetName(HashSet<string> names, string proposedName)
		{
			return GetName(names, proposedName, false);
		}

		static string GetName(HashSet<string> names, string proposedName, bool capitalize)
		{
			return GetUniqueName(names, ConvertToCompilable(proposedName, capitalize));
		}

		readonly Dictionary<TableSchema,string> _contextMembers = new Dictionary<TableSchema,string>();

		void ConvertSchema(string typeName)
		{
			var typeNames          = new HashSet<string> { typeName };
			var contextMemberNames = new HashSet<string> { typeName };

			foreach (var table in _schema!.Tables)
			{
				table.TypeName = GetName(typeNames, table.TypeName);

				{
					var contextMemberName = table.TypeName;

					if (!_cxInfo.DynamicSchemaOptions.NoPluralization)
						contextMemberName = Pluralization.ToPlural(contextMemberName);

					_contextMembers[table] = GetName(contextMemberNames, contextMemberName);
				}

				var classMemberNames = new HashSet<string> { table.TypeName };

				foreach (var column in table.Columns)
				{
					//Debug.WriteLine($"{table.TypeName}.{column.MemberName}");

					column.MemberName = GetName(classMemberNames, column.MemberName, !_cxInfo.DynamicSchemaOptions.NoCapitalization);
				}

				foreach (var key in table.ForeignKeys)
				{
					if (!_cxInfo.DynamicSchemaOptions.NoPluralization)
						key.MemberName = key.AssociationType == AssociationType.OneToMany
							? Pluralization.ToPlural  (key.MemberName)
							: Pluralization.ToSingular(key.MemberName);

					key.MemberName = GetName(classMemberNames, key.MemberName);
				}
			}

			foreach (var procedure in _schema.Procedures)
			{
				// migrate https://github.com/linq2db/linq2db/pull/1905
				if (!procedure.IsFunction && ProviderName == LinqToDB.ProviderName.SqlServer)
				{
					// sql server procedures always have integer return parameter
					var name = "@returnValue";
					var cnt  = 0;
					while (procedure.Parameters.Any(_ => _.ParameterName == name))
						name = $"@returnValue{cnt++}";

					procedure.Parameters.Add(new ParameterSchema()
					{
						SchemaName           = name,
						ParameterName        = name,
						IsResult             = true,
						DataType             = DataType.Int32,
						SystemType           = typeof(int),
						SchemaType           = "int",
						ParameterType        = "int",
						ProviderSpecificType = "int"
					});
				}

				procedure.MemberName = GetName(typeNames, procedure.MemberName);

				if (procedure.ResultTable != null && !_contextMembers.ContainsKey(procedure.ResultTable))
				{
					procedure.ResultTable.TypeName = GetName(typeNames, procedure.ResultTable.TypeName);

					var classMemberNames = new HashSet<string> { procedure.ResultTable.TypeName };

					foreach (var column in procedure.ResultTable.Columns)
					{
						var memberName = column.MemberName.IsNullOrWhiteSpace() ? "Column" : column.MemberName;
						column.MemberName = GetName(classMemberNames, memberName);
					}
				}

				foreach (var parameter in procedure.Parameters)
				{
					parameter.ParameterName = ConvertToCompilable(parameter.ParameterName, false);
				}
			}
		}

		static string ConvertToCompilable(string name, bool capitalize)
		{
			if (capitalize)
			{
				var sb = new StringBuilder(name);
				for (int i = 0; i < sb.Length; i++)
				{
					if (char.IsLetter(sb[i]))
					{
						sb[i] = sb[i].ToUpper();
						break;
					}
				}

				name = sb.ToString();
			}

			return CSharpTools.ToValidIdentifier(name);
		}
	}
}
