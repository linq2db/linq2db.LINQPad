using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

			_useProviderSpecificTypes = ((string)_cxInfo.DriverData.Element("useProviderSpecificTypes") ?? "").ToLower() == "true";
			_providerName             = (string)_cxInfo.DriverData.Element("providerName");
		}

		readonly IConnectionInfo _cxInfo;
		readonly StringBuilder   _classCode = new StringBuilder();
		readonly bool            _useProviderSpecificTypes;
		readonly string          _providerName;

		DatabaseSchema _schema;
		IDataProvider  _dataProvider;
		ISqlBuilder    _sqlBuilder;

		public readonly StringBuilder Code = new StringBuilder();

		public IEnumerable<ExplorerItem> GetItemsAndCode(string nameSpace, string typeName)
		{
			var connectionString = _cxInfo.DatabaseInfo.CustomCxString;

			using (var db = new DataConnection(_providerName, connectionString))
			{
				_dataProvider = db.DataProvider;
				_sqlBuilder   = _dataProvider.CreateSqlBuilder();

				var options = new GetSchemaOptions();

				var includeSchemas = (string)_cxInfo.DriverData.Element("includeSchemas");
				if (includeSchemas != null) options.IncludedSchemas = includeSchemas.Split(',', ';');

				var excludeSchemas = (string)_cxInfo.DriverData.Element("excludeSchemas");
				if (excludeSchemas != null) options.ExcludedSchemas = excludeSchemas.Split(',', ';');

				_schema = _dataProvider.GetSchemaProvider().GetSchema(db, options);

				ConvertSchema();
			}

			Code
				.AppendLine("using System;")
				.AppendLine("using LinqToDB;")
				.AppendLine("using LinqToDB.Data;")
				.AppendLine("using LinqToDB.Mapping;")
				;

			if (_useProviderSpecificTypes)
			{
				switch (_providerName)
				{
					case ProviderName.SqlServer : Code.AppendLine("using System.Data.SqlTypes;"); break;
				}
			}

			Code
				.AppendLine($"namespace {nameSpace}")
				.AppendLine( "{")
				.AppendLine($"  public class {typeName} : LinqToDB.LINQPad.LINQPadDataConnection")
				.AppendLine( "  {")
				.AppendLine($"    public {typeName}(string provider, string connectionString)")
				.AppendLine( "      : base(provider, connectionString)")
				.AppendLine( "    {}")
				.AppendLine($"    public {typeName}()")
				.AppendLine($"      : base({ToCodeString(_providerName)}, {ToCodeString(connectionString)})")
				.AppendLine( "    {}")
				;

			var schemas =
			(
				from t in _schema.Tables
				select new { t.IsDefaultSchema, t.SchemaName }
			)
			.Union
			(
				from p in _schema.Procedures
				select new { p.IsDefaultSchema, p.SchemaName }
			)
			.Distinct()
			.ToList();

			foreach (var s in schemas)
			{
				var tables = _schema.Tables.Where(t => s.IsDefaultSchema == t.IsDefaultSchema && s.SchemaName == t.SchemaName).ToList();
				var items  = new List<ExplorerItem>();

				if (tables.Any(t => !t.IsView && !t.IsProcedureResult))
					items.Add(GetTables("Tables", ExplorerIcon.Table, tables.Where(t => !t.IsView && !t.IsProcedureResult)));

				if (tables.Any(t => t.IsView))
					items.Add(GetTables("Views", ExplorerIcon.View, tables.Where(t => t.IsView)));

				if (schemas.Count == 1)
					foreach (var item in items)
						yield return item;
				else
					yield return new ExplorerItem(
						string.IsNullOrEmpty(s.SchemaName) ? s.IsDefaultSchema ? "(default)" : "empty" : s.SchemaName,
						ExplorerItemKind.Schema,
						ExplorerIcon.Schema)
					{
						Children = items
					};
			}

			Code
				.AppendLine("  }")
				.AppendLine(_classCode.ToString())
				.AppendLine("}")
				;
		}

		ExplorerItem GetTables(string header, ExplorerIcon icon, IEnumerable<TableSchema> tables)
		{
			return new ExplorerItem(header, ExplorerItemKind.Category, icon)
			{
				Children = tables
					.Select(t =>
					{
						var memberName = t.TypeName;

						if (!_cxInfo.DynamicSchemaOptions.NoPluralization)
							memberName = Pluralization.ToPlural(memberName);

						Code.AppendLine($"    public ITable<{t.TypeName}> {memberName} {{ get {{ return this.GetTable<{t.TypeName}>(); }} }}");

						_classCode.Append($"  [Table(Name=\"{t.TableName}\"");

						if (!string.IsNullOrEmpty(t.SchemaName))
							_classCode.Append($", Schema=\"{t.SchemaName}\"");

						_classCode
							.AppendLine( "  )]")
							.AppendLine($"  public class {t.TypeName}")
							.AppendLine( "  {")
							;

						var ret = new ExplorerItem(memberName, ExplorerItemKind.QueryableObject, icon)
						{
							DragText     = memberName,
							ToolTipText  = $"ITable<{t.TypeName}>",
							SqlName      = _sqlBuilder.BuildTableName(
								new StringBuilder(),
								null,
								t.SchemaName == null ? null : (string)_sqlBuilder.Convert(t.SchemaName, ConvertType.NameToOwner),
								(string)_sqlBuilder.Convert(t.TableName,  ConvertType.NameToQueryTable)).ToString(),
							IsEnumerable = true,
							Children     = t.Columns
								.Select(c =>
								{
									_classCode
										.AppendLine($"    [Column(\"{c.ColumnName}\")]")
										.AppendLine(c.IsNullable ? "    [Nullable]" : "    [NotNull]");

									if (c.IsPrimaryKey) _classCode.AppendLine($"   [PrimaryKey({c.PrimaryKeyOrder})]");
									if (c.IsIdentity)   _classCode.AppendLine("    [Identity]");

									var memberType = MapMemberType(c.MemberType);

									_classCode.AppendLine($"    public {memberType} {c.MemberName} {{ get; set; }}");

									var sqlName = (string)_sqlBuilder.Convert(c.MemberName, ConvertType.NameToQueryField);

									return new ExplorerItem(
										c.MemberName,
										ExplorerItemKind.Property,
										c.IsPrimaryKey ? ExplorerIcon.Key : ExplorerIcon.Column)
									{
										Text               = $"{c.MemberName} : {memberType}",
										ToolTipText        = $"{sqlName} {c.ColumnType} {(c.IsNullable ? "NULL" : "NOT NULL")}{(c.IsIdentity ? " IDENTITY" : "")}",
										DragText           = c.MemberName,
										SqlName            = sqlName,
										SqlTypeDeclaration = $"{c.ColumnType} {(c.IsNullable ? "NULL" : "NOT NULL")}{(c.IsIdentity ? " IDENTITY" : "")}",
									};
								})
								.ToList()
						};

						_classCode
							.AppendLine("}")
							;

						return ret;
					})
					.ToList()
			};
		}

		string MapMemberType(string memberType)
		{
			if (_useProviderSpecificTypes)
			{
				switch (memberType)
				{
					case "decimal" :
					case "decimal?":

						if (_providerName == ProviderName.SqlServer)
							return "SqlDecimal";
						break;
				}
			}

			return memberType;
		}

		static readonly HashSet<string> _keyWords = new HashSet<string>
		{
			"abstract", "as",       "base",     "bool",    "break",     "byte",     "case",       "catch",     "char",    "checked",
			"class",    "const",    "continue", "decimal", "default",   "delegate", "do",         "double",    "else",    "enum",
			"event",    "explicit", "extern",   "false",   "finally",   "fixed",    "float",      "for",       "foreach", "goto",
			"if",       "implicit", "in",       "int",     "interface", "internal", "is",         "lock",      "long",    "new",
			"null",     "object",   "operator", "out",     "override",  "params",   "private",    "protected", "public",  "readonly",
			"ref",      "return",   "sbyte",    "sealed",  "short",     "sizeof",   "stackalloc", "static",    "struct",  "switch",
			"this",     "throw",    "true",     "try",     "typeof",    "uint",     "ulong",      "unchecked", "unsafe",  "ushort",
			"using",    "virtual",  "volatile", "void",    "while"
		};

		void ConvertSchema()
		{
			foreach (var table in _schema.Tables)
			{
				table.TypeName = ConvertToCompilable(table.TypeName);

				foreach (var column in table.Columns)
				{
					column.MemberName = ConvertToCompilable(column.MemberName);

					if (_keyWords.Contains(column.MemberName))
						column.MemberName = "@" + column.MemberName;

					if (column.MemberName == table.TypeName)
						column.MemberName += "_Column";
				}
			}
		}

		string ToCodeString(string text)
		{
			return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
		}

		static string ConvertToCompilable(string name)
		{
			var query =
				from c in name
				select char.IsLetterOrDigit(c) || c == '@' ? c : '_';

			return new string(query.ToArray());
		}
	}
}
