using System;
using System.Collections.Generic;
using System.Diagnostics;
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

			UseProviderSpecificTypes = ((string)_cxInfo.DriverData.Element("useProviderSpecificTypes"))?.ToLower() == "true";
			ProviderName             =  (string)_cxInfo.DriverData.Element("providerName");
		}

		public readonly bool         UseProviderSpecificTypes;
		public readonly string       ProviderName;
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
				.AppendLine("using LinqToDB;")
				.AppendLine("using LinqToDB.Data;")
				.AppendLine("using LinqToDB.Mapping;")
				;

			if (!string.IsNullOrWhiteSpace(_schema.ProviderSpecificTypeNamespace))
				Code.AppendLine($"using {_schema.ProviderSpecificTypeNamespace};");

			switch (ProviderName)
			{
				case LinqToDB.ProviderName.MySql :
					References.Add(typeof(MySql.Data.MySqlClient.MySqlConnection).Assembly.Location);
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
			}

			Code
				.AppendLine($"namespace {nameSpace}")
				.AppendLine( "{")
				.AppendLine($"  public class @{typeName} : LinqToDB.LINQPad.LINQPadDataConnection")
				.AppendLine( "  {")
				.AppendLine($"    public @{typeName}(string provider, string connectionString)")
				.AppendLine( "      : base(provider, connectionString)")
				.AppendLine( "    {}")
				.AppendLine($"    public @{typeName}()")
				.AppendLine($"      : base({ToCodeString(ProviderName)}, {ToCodeString(connectionString)})")
				.AppendLine( "    {}")
				;

			var schemas =
			(
				from t in _schema.Tables
				group t by new { t.IsDefaultSchema, t.SchemaName } into gr
				orderby !gr.Key.IsDefaultSchema, gr.Key.SchemaName
				select new { gr.Key, Items = gr.ToList() }
			)
			.ToList();

			foreach (var s in schemas)
			{
				var items = new List<ExplorerItem>();

				if (s.Items.Any(t => !t.IsView && !t.IsProcedureResult))
					items.Add(GetTables("Tables", ExplorerIcon.Table, s.Items.Where(t => !t.IsView && !t.IsProcedureResult)));

				if (s.Items.Any(t => t.IsView))
					items.Add(GetTables("Views", ExplorerIcon.View, s.Items.Where(t => t.IsView)));

				if (schemas.Count == 1)
					foreach (var item in items)
						yield return item;
				else
					yield return new ExplorerItem(
						string.IsNullOrEmpty(s.Key.SchemaName) ? s.Key.IsDefaultSchema ? "(default)" : "empty" : s.Key.SchemaName,
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

#if DEBUG
			Debug.WriteLine(Code.ToString());
#endif
		}

		ExplorerItem GetTables(string header, ExplorerIcon icon, IEnumerable<TableSchema> tables)
		{
			return new ExplorerItem(header, ExplorerItemKind.Category, icon)
			{
				Children = tables
					.Select(t =>
					{
						var memberName = _contextMembers[t];

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

									var sqlName = (string)_sqlBuilder.Convert(c.ColumnName, ConvertType.NameToQueryField);

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
			if (UseProviderSpecificTypes)
			{
				switch (memberType)
				{
					case "decimal" :
					case "decimal?":

						if (ProviderName == LinqToDB.ProviderName.SqlServer)
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

		string GetName(HashSet<string> names, string proposedName)
		{
			var name = proposedName;
			var n    = 0;

			while (names.Contains(name))
				name = proposedName + ++n;

			names.Add(name);

			return name;
		}

		readonly Dictionary<TableSchema,string> _contextMembers = new Dictionary<TableSchema,string>();

		void ConvertSchema(string typeName)
		{
			var typeNames          = new HashSet<string> { typeName };
			var contextMemberNames = new HashSet<string> { typeName };

			foreach (var table in _schema.Tables)
			{
				table.TypeName = GetName(typeNames, ConvertToCompilable(table.TypeName));

				{
					var contextMemberName = table.TypeName;

					if (!_cxInfo.DynamicSchemaOptions.NoPluralization)
						contextMemberName = Pluralization.ToPlural(contextMemberName);

					_contextMembers[table] = GetName(contextMemberNames, contextMemberName);
				}

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
