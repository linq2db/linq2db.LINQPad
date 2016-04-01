using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using CodeJam;

using LinqToDB.Data;
using LinqToDB.DataProvider;
using LinqToDB.SchemaProvider;
using LinqToDB.SqlProvider;

using LINQPad.Extensibility.DataContext;

namespace LinqToDB.LINQPad
{
	class SchemaAndCodeGenerator
	{
		public SchemaAndCodeGenerator(IConnectionInfo cxInfo)
		{
			_cxInfo = cxInfo;

			UseProviderSpecificTypes = ((string)_cxInfo.DriverData.Element("useProviderSpecificTypes"))?.ToLower() == "true";
			ProviderName             =  (string)_cxInfo.DriverData.Element("providerName");
			ProviderVersion          =  (string)_cxInfo.DriverData.Element("providerVersion");
		}

		public readonly bool         UseProviderSpecificTypes;
		public readonly string       ProviderName;
		public readonly string       ProviderVersion;
		public readonly List<string> References = new List<string>();

		readonly IConnectionInfo _cxInfo;
		readonly StringBuilder   _classCode = new StringBuilder();

		DatabaseSchema _schema;
		IDataProvider  _dataProvider;
		ISqlBuilder    _sqlBuilder;

		public readonly StringBuilder Code = new StringBuilder();

		public IEnumerable<ExplorerItem> GetItemsAndCode(string nameSpace, string typeName)
		{
			var connectionString = _cxInfo.DatabaseInfo.CustomCxString;

			using (var db = new DataConnection(ProviderName, connectionString))
			{
				_dataProvider = db.DataProvider;
				_sqlBuilder   = _dataProvider.CreateSqlBuilder();

				var options = new GetSchemaOptions();

				var includeSchemas = (string)_cxInfo.DriverData.Element("includeSchemas");
				if (includeSchemas != null) options.IncludedSchemas = includeSchemas.Split(',', ';');

				var excludeSchemas = (string)_cxInfo.DriverData.Element("excludeSchemas");
				if (excludeSchemas != null) options.ExcludedSchemas = excludeSchemas.Split(',', ';');

				_schema = _dataProvider.GetSchemaProvider().GetSchema(db, options);

				ConvertSchema(typeName);
			}

			Code
				.AppendLine("using System;")
				.AppendLine("using System.Collections;")
				.AppendLine("using System.Collections.Generic;")
				.AppendLine("using System.Data;")
				.AppendLine("using System.Reflection;")
				.AppendLine("using LinqToDB;")
				.AppendLine("using LinqToDB.Common;")
				.AppendLine("using LinqToDB.Data;")
				.AppendLine("using LinqToDB.Mapping;")
				;

			if (_schema.ProviderSpecificTypeNamespace.NotNullNorWhiteSpace())
				Code.AppendLine($"using {_schema.ProviderSpecificTypeNamespace};");

			switch (ProviderName)
			{
				case LinqToDB.ProviderName.DB2LUW :
				case LinqToDB.ProviderName.DB2zOS :
					References.Add(typeof(IBM.Data.DB2.DB2Connection).Assembly.Location);
					break;

				case LinqToDB.ProviderName.Informix :
					References.Add(typeof(IBM.Data.Informix.IfxConnection).Assembly.Location);
					break;

				case LinqToDB.ProviderName.Firebird :
					References.Add(typeof(FirebirdSql.Data.FirebirdClient.FbConnection).Assembly.Location);
					break;

				case LinqToDB.ProviderName.MySql :
					References.Add(typeof(MySql.Data.MySqlClient.MySqlConnection).Assembly.Location);
					break;

				case LinqToDB.ProviderName.OracleNative :
					References.Add(typeof(Oracle.DataAccess.Client.OracleConnection).Assembly.Location);
					break;

				case LinqToDB.ProviderName.OracleManaged :
					References.Add(typeof(Oracle.ManagedDataAccess.Client.OracleConnection).Assembly.Location);
					break;

				case LinqToDB.ProviderName.PostgreSQL :
					References.Add(typeof(Npgsql.NpgsqlConnection).Assembly.Location);
					break;

				case LinqToDB.ProviderName.SqlCe :
					References.Add(typeof(System.Data.SqlServerCe.SqlCeConnection).Assembly.Location);
					break;

				case LinqToDB.ProviderName.SQLite :
					References.Add(typeof(System.Data.SQLite.SQLiteConnection).Assembly.Location);
					break;

				case LinqToDB.ProviderName.SqlServer :
					Code.AppendLine("using Microsoft.SqlServer.Types;");
					References.Add(typeof(Microsoft.SqlServer.Types.SqlHierarchyId).Assembly.Location);
					break;

				case LinqToDB.ProviderName.Sybase :
					References.Add(typeof(Sybase.Data.AseClient.AseConnection).Assembly.Location);
					break;

				case LinqToDB.ProviderName.SapHana :
					References.Add(typeof(Sap.Data.Hana.HanaConnection).Assembly.Location);
					break;
			}

			Code
				.AppendLine($"namespace {nameSpace}")
				.AppendLine( "{")
				.AppendLine($"  public class @{typeName} : LinqToDB.LINQPad.LINQPadDataConnection")
				.AppendLine( "  {")
				.AppendLine($"    public @{typeName}(string provider, string connectionString)")
				.AppendLine( "      : base(provider, connectionString)")
				.AppendLine( "    {")
				.AppendLine( "      LinqToDB.DataProvider.Firebird.FirebirdConfiguration.QuoteIdentifiers = true;")
				.AppendLine( "    }")
				.AppendLine($"    public @{typeName}()")
				.AppendLine($"      : base({ToCodeString(ProviderVersion ?? ProviderName)}, {ToCodeString(connectionString)})")
				.AppendLine( "    {")
				.AppendLine( "    }")
				;

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

			if (p.IsTableFunction)
			{
				code.Append($"    [Sql.TableFunction(Name=\"{p.ProcedureName}\"");

				if (p.SchemaName != null)
					code.Append($", Schema=\"{p.SchemaName}\")");

				code
					.AppendLine("]")
					.Append($"    public ITable<{p.ResultTable?.TypeName}>")
					;
			}
			else if (p.IsFunction)
			{
				code
					.AppendLine($"    [Sql.Function(Name=\"{sprocSqlName}\", ServerSideOnly=true)]")
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
					.Where (pr => !pr.IsResult)
					.Select(pr => $"{(pr.IsOut ? pr.IsIn ? "ref " : "out " : "")}{pr.ParameterType} {pr.ParameterName}")
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
				code.AppendLine("      throw new InvalidOperationException();");
			}
			else
			{
				var spName = $"\"{sprocSqlName.Replace("\"", "\\\"")}\"";

				var inputParameters  = p.Parameters.Where(pp => pp.IsIn). ToList();
				var outputParameters = p.Parameters.Where(pp => pp.IsOut).ToList();

				spName += inputParameters.Count == 0 ? ");" : ",";

				var retName = "ret";
				var retNo   = 0;

				while (p.Parameters.Any(pp => pp.ParameterName == retName))
					retName = "ret" + ++retNo;

				var hasOut = outputParameters.Any(pr => pr.IsOut);
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

				for (var i = 0; i < inputParameters.Count; i++)
				{
					var pr = inputParameters[i];

					var str = $"        new DataParameter(\"{pr.SchemaName}\", {pr.ParameterName}, DataType.{pr.DataType})";

					if (pr.IsOut)
					{
						str += " { Direction = " + (pr.IsIn ? "ParameterDirection.InputOutput" : "ParameterDirection.Output");

						if (pr.Size != null && pr.Size.Value != 0)
							str += ", Size = " + pr.Size.Value;

						str += " }";
					}

					str += i + 1 == inputParameters.Count ? ");" : ",";

					code.AppendLine(str);
				}

				if (hasOut)
				{
					code.AppendLine();

					foreach (var pr in p.Parameters.Where(_ => _.IsOut))
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

							CodeTable(Code, p.ResultTable, false);
						}

						CodeProcedure(Code, p, sprocSqlName);

						return ret;
					})
					.ToList(),
			};

			return items;
		}

		void CodeTable(StringBuilder classCode, TableSchema table, bool addTableAttribute)
		{
			classCode.AppendLine();

			if (addTableAttribute)
			{
				classCode.Append($"  [Table(Name=\"{table.TableName}\"");

				if (table.SchemaName.NotNullNorEmpty())
					classCode.Append($", Schema=\"{table.SchemaName}\"");

				classCode.AppendLine(")]");
			}

			classCode
				.AppendLine($"  public class @{table.TypeName}")
				.AppendLine( "  {")
				;

			foreach (var c in table.Columns)
			{
				classCode
					.Append($"    [Column(\"{c.ColumnName}\"), ")
					.Append(c.IsNullable ? "Nullable" : "NotNull");

				if (c.IsPrimaryKey) classCode.Append($", PrimaryKey({c.PrimaryKeyOrder})");
				if (c.IsIdentity)   classCode.Append(", Identity");

				classCode.AppendLine("]");

				var memberType = UseProviderSpecificTypes ? (c.ProviderSpecificType ?? c.MemberType) : c.MemberType;

				classCode.AppendLine($"    public {memberType} @{c.MemberName} {{ get; set; }}");
			}

			foreach (var key in table.ForeignKeys)
			{
				classCode
					.Append( "    [Association(")
					.Append($"ThisKey=\"{(key.ThisColumns.Select(c => c.MemberName)).Join(", ")}\"")
					.Append($", OtherKey=\"{(key.OtherColumns.Select(c => c.MemberName)).Join(", ")}\"")
					.Append($", CanBeNull={(key.CanBeNull ? "true" : "false")}")
					;

				if (key.BackReference != null)
				{
					if (key.KeyName.NotNullNorEmpty())
						classCode.Append($", KeyName=\"{key.KeyName}\"");
					if (key.BackReference.KeyName.NotNullNorEmpty())
						classCode.Append($", BackReferenceName=\"{key.BackReference.MemberName}\"");
				}
				else
				{
					classCode.Append(", IsBackReference=true");
				}

				classCode.AppendLine(")]");

				var typeName = key.AssociationType == AssociationType.OneToMany
					? $"List<{key.OtherTable.TypeName}>"
					: key.OtherTable.TypeName;

				classCode.AppendLine($"    public {typeName} @{key.MemberName} {{ get; set; }}");
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

						Code.AppendLine($"    public ITable<@{t.TypeName}> @{memberName} {{ get {{ return this.GetTable<@{t.TypeName}>(); }} }}");

						CodeTable(_classCode, t, true);

						var tableSqlName = _sqlBuilder.BuildTableName(
							new StringBuilder(),
							null,
							t.SchemaName == null ? null : (string)_sqlBuilder.Convert(t.SchemaName, ConvertType.NameToOwner),
							(string)_sqlBuilder.Convert(t.TableName,  ConvertType.NameToQueryTable)).ToString();

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
			return GetUniqueName(names, ConvertToCompilable(proposedName));
		}

		readonly Dictionary<TableSchema,string> _contextMembers = new Dictionary<TableSchema,string>();

		void ConvertSchema(string typeName)
		{
			var typeNames          = new HashSet<string> { typeName };
			var contextMemberNames = new HashSet<string> { typeName };

			foreach (var table in _schema.Tables)
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

					column.MemberName = GetName(classMemberNames, column.MemberName);
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
			}
		}

		static string ToCodeString(string text)
		{
			return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
		}

		static string ConvertToCompilable(string name)
		{
			var query =
				from c in name.TrimStart('@')
				select c.IsLetterOrDigit() ? c : '_';

			var arr = query.ToArray();

			if (arr.Length == 0)
				arr = new[] { '_' };

			if (arr[0].IsDigit())
				return '_' + new string(arr);

			return new string(arr);
		}
	}
}
