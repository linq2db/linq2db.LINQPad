using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using LinqToDB.Common;
using LinqToDB.Data;
using LinqToDB.SchemaProvider;

using LINQPad;
using LINQPad.Extensibility.DataContext;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LinqToDB.LINQPad
{
	public class LinqToDBDriver : DynamicDataContextDriver
	{
		static LinqToDBDriver()
		{
		}

		public override string Name   => "LINQ to DB";
		public override string Author => "Igor Tkachev";

		public override string GetConnectionDescription(IConnectionInfo cxInfo)
		{
			var providerName = (string)cxInfo.DriverData.Element("providerName");
			var dbInfo       = cxInfo.DatabaseInfo;

			return $"[{providerName}] {dbInfo.Server}\\{dbInfo.Database} (v.{dbInfo.DbVersion})";
		}

		public override DateTime? GetLastSchemaUpdate(IConnectionInfo cxInfo)
		{
			var providerName = (string)cxInfo.DriverData.Element("providerName");

			if (providerName == ProviderName.SqlServer)
				using (var db = new LINQPadDataConnection(cxInfo))
					return db.Query<DateTime?>("select max(modify_date) from sys.objects").FirstOrDefault();

			return null;
		}

		#region ShowConnectionDialog

		public override bool ShowConnectionDialog(IConnectionInfo cxInfo, bool isNewConnection)
		{
			var model        = new ConnectionViewModel();
			var providerName = (string)cxInfo.DriverData.Element("providerName");

			if (providerName != null)
				model.SelectedProvider = model.Providers.IndexOf(providerName);

			model.Name             = cxInfo.DisplayName;
			model.ConnectionString = string.IsNullOrWhiteSpace(cxInfo.DatabaseInfo.CustomCxString) ? (string)cxInfo.DriverData.Element("connectionString") : cxInfo.DatabaseInfo.CustomCxString;
			model.IncludeSchemas   = (string)cxInfo.DriverData.Element("includeSchemas");
			model.ExcludeSchemas   = (string)cxInfo.DriverData.Element("excludeSchemas");

			_cxInfo = cxInfo;

			if (ConnectionDialog.Show(this, model))
			{
				providerName = model.Providers[model.SelectedProvider];

				cxInfo.DriverData.SetElementValue("providerName",     providerName);
				cxInfo.DriverData.SetElementValue("connectionString", null);
				cxInfo.DriverData.SetElementValue("includeSchemas",   string.IsNullOrWhiteSpace(model.IncludeSchemas)   ? null : model.IncludeSchemas);
				cxInfo.DriverData.SetElementValue("excludeSchemas",   string.IsNullOrWhiteSpace(model.ExcludeSchemas)   ? null : model.ExcludeSchemas);

				switch (providerName)
				{
					case ProviderName.SqlServer: cxInfo.DatabaseInfo.Provider = typeof(SqlConnection).Namespace; break;
				}

				try
				{
					using (var db = new LINQPadDataConnection(providerName, model.ConnectionString))
					{
						cxInfo.DatabaseInfo.Provider  = db.Connection.GetType().Namespace;
						cxInfo.DatabaseInfo.Server    = ((DbConnection)db.Connection).DataSource;
						cxInfo.DatabaseInfo.Database  = db.Connection.Database;
						cxInfo.DatabaseInfo.DbVersion = ((DbConnection)db.Connection).ServerVersion;
					}
				}
				catch
				{
				}

				cxInfo.DatabaseInfo.CustomCxString        = model.ConnectionString;
				cxInfo.DatabaseInfo.EncryptCustomCxString = true;
				cxInfo.DisplayName                        = string.IsNullOrWhiteSpace(model.Name) ? null : model.Name;

				return true;
			}

			return false;
		}

		IConnectionInfo _cxInfo;

		public Exception TestConnection(ConnectionViewModel model)
		{
			if (model == null)
				return null;

//			var providerName = model.Providers[model.SelectedProvider];
//
//			switch (providerName)
//			{
//				case ProviderName.SQLite:
//					_cxInfo.DatabaseInfo.Provider = SQLiteTools.AssemblyName;
//
//					base.GetProviderFactory(_cxInfo);
//					
//					break;
//			}

			try
			{
				using (var db = new DataConnection(model.Providers[model.SelectedProvider], model.ConnectionString))
				{
					var conn = db.Connection;
					return null;
				}
			}
			catch (Exception ex)
			{
				return ex;
			}
		}

		#endregion

		#region GetSchemaAndBuildAssembly

		ExplorerItem GetTables(string header, ExplorerIcon icon, StringBuilder code, StringBuilder classCode, IEnumerable<TableSchema> tables)
		{
			return new ExplorerItem(header, ExplorerItemKind.Category, icon)
			{
				Children = tables
					.Select(t =>
					{
						code.AppendLine($"    public ITable<{t.TypeName}> {t.TypeName} {{ get {{ return this.GetTable<{t.TypeName}>(); }} }}");

						classCode.Append($"  [Table(Name=\"{t.TableName}\"");

						if (!string.IsNullOrEmpty(t.SchemaName))
							classCode.Append($", Schema=\"{t.SchemaName}\"");

						classCode
							.AppendLine( "  )]")
							.AppendLine($"  public class {t.TypeName}")
							.AppendLine( "  {")
							;

						var ret = new ExplorerItem(t.TableName, ExplorerItemKind.QueryableObject, icon)
						{
							IsEnumerable = true,
							Children     = t.Columns
								.Select(c =>
								{
									classCode
										.AppendLine($"    [Column(\"{c.ColumnName}\")]")
										.AppendLine(c.IsNullable ? "    [Nullable]" : "    [NotNull]");

									if (c.IsPrimaryKey) classCode.AppendLine($"   [PrimaryKey({c.PrimaryKeyOrder})]");
									if (c.IsIdentity)   classCode.AppendLine("    [Identity]");

									classCode.AppendLine($"    public {c.MemberType} {c.MemberName} {{ get; set; }}");

									return new ExplorerItem(
										c.MemberName,
										ExplorerItemKind.Property,
										c.IsPrimaryKey ? ExplorerIcon.Key : ExplorerIcon.Column)
									{
										SqlName = c.ColumnName,
									};
								})
								.ToList()
						};

						classCode
							.AppendLine("}")
							;

						return ret;
					})
					.ToList()
			};
		}

		string ToCodeString(string text)
		{
			return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
		}

		IEnumerable<ExplorerItem> GetItemsAndCode(IConnectionInfo cxInfo, StringBuilder code, string nameSpace, string typeName)
		{
			var providerName     = (string)cxInfo.DriverData.Element("providerName");
			var connectionString = cxInfo.DatabaseInfo.CustomCxString;

			DatabaseSchema schema;

			using (var db = new DataConnection(providerName, connectionString))
			{
				var options = new GetSchemaOptions();

				var includeSchemas = (string)cxInfo.DriverData.Element("includeSchemas");
				if (includeSchemas != null) options.IncludedSchemas = includeSchemas.Split(',', ';');

				var excludeSchemas = (string)cxInfo.DriverData.Element("excludeSchemas");
				if (excludeSchemas != null) options.ExcludedSchemas = excludeSchemas.Split(',', ';');

				schema = db.DataProvider.GetSchemaProvider().GetSchema(db, options);

				Convert(schema);
			}

			code
				.AppendLine( "using System;")
				.AppendLine( "using LinqToDB;")
				.AppendLine( "using LinqToDB.Data;")
				.AppendLine( "using LinqToDB.Mapping;")
				.AppendLine($"namespace {nameSpace}")
				.AppendLine( "{")
				.AppendLine($"  public class {typeName} : LinqToDB.LINQPad.LINQPadDataConnection")
				.AppendLine( "  {")
				.AppendLine($"    public {typeName}(string provider, string connectionString)")
				.AppendLine( "      : base(provider, connectionString)")
				.AppendLine( "    {}")
				.AppendLine($"    public {typeName}()")
				.AppendLine($"      : base({ToCodeString(providerName)}, {ToCodeString(connectionString)})")
				.AppendLine( "    {}")
				;

			var schemas =
			(
				from t in schema.Tables
				select new { t.IsDefaultSchema, t.SchemaName }
			)
			.Union
			(
				from p in schema.Procedures
				select new { p.IsDefaultSchema, p.SchemaName }
			)
			.Distinct()
			.ToList();

			var classCode = new StringBuilder();

			foreach (var s in schemas)
			{
				var tables = schema.Tables.Where(t => s.IsDefaultSchema == t.IsDefaultSchema && s.SchemaName == t.SchemaName).ToList();
				var items  = new List<ExplorerItem>();

				if (tables.Any(t => !t.IsView && !t.IsProcedureResult))
					items.Add(GetTables("Tables", ExplorerIcon.Table, code, classCode, tables.Where (t => !t.IsView && !t.IsProcedureResult)));

				if (tables.Any(t => t.IsView))
					items.Add(GetTables("Views", ExplorerIcon.View, code, classCode, tables.Where(t => t.IsView)));

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

			code
				.AppendLine("  }")
				;

			code.AppendLine(classCode.ToString());

			code
				.AppendLine("}")
				;
		}

		static string ConvertToCompilable(string name)
		{
			var query =
				from c in name
				select char.IsLetterOrDigit(c) || c == '@' ? c : '_';

			return new string(query.ToArray());
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

		void Convert(DatabaseSchema schema)
		{
			foreach (var table in schema.Tables)
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

		public override List<ExplorerItem> GetSchemaAndBuildAssembly(
			IConnectionInfo cxInfo, AssemblyName assemblyToBuild, ref string nameSpace, ref string typeName)
		{
			var code       = new StringBuilder();
			var items      = GetItemsAndCode(cxInfo, code, nameSpace, typeName).ToList();
			var text       = code.ToString();
			var syntaxTree = CSharpSyntaxTree.ParseText(text);

			var references = new MetadataReference[]
			{
				MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
				MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
				MetadataReference.CreateFromFile(typeof(IDbConnection).Assembly.Location),
				MetadataReference.CreateFromFile(typeof(DataConnection).Assembly.Location),
				MetadataReference.CreateFromFile(typeof(LINQPadDataConnection).Assembly.Location),
			};

			var compilation = CSharpCompilation.Create(
				assemblyToBuild.Name,
				syntaxTrees: new[] { syntaxTree },
				references : references,
				options    : new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

			using (var stream = new FileStream(assemblyToBuild.CodeBase, FileMode.Create))
			//using (var stream = new MemoryStream())
			{
				var result = compilation.Emit(stream);

				if (!result.Success)
				{
					var failures = result.Diagnostics.Where(diagnostic =>
						diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

					foreach (var diagnostic in failures)
						throw new Exception(diagnostic.ToString());
				}
				//else
				//{
				//	stream.Seek(0, SeekOrigin.Begin);
				//
				//	var assembly = Assembly.Load(stream.ToArray());
				//	var type     = assembly.GetType($"{nameSpace}.{typeName}");
				//	var obj      = Activator.CreateInstance(type);
				//}
			}

			return items;
		}

#endregion

		public override ParameterDescriptor[] GetContextConstructorParameters(IConnectionInfo cxInfo)
		{
			return new[]
			{
				new ParameterDescriptor("provider",         typeof(string).FullName), 
				new ParameterDescriptor("connectionString", typeof(string).FullName), 
			};
		}

		public override object[] GetContextConstructorArguments(IConnectionInfo cxInfo)
		{
			return new object[]
			{
				(string)cxInfo.DriverData.Element("providerName"),
				cxInfo.DatabaseInfo.CustomCxString,
			};
		}

		public override IEnumerable<string> GetAssembliesToAdd(IConnectionInfo cxInfo)
		{
			return new[]
			{
				typeof(IDbConnection).        Assembly.Location,
				typeof(DataConnection).       Assembly.Location,
				typeof(LINQPadDataConnection).Assembly.Location,
			};
		}

		public override IEnumerable<string> GetNamespacesToAdd(IConnectionInfo cxInfo)
		{
			return new[]
			{
				"LinqToDB",
				"LinqToDB.Data",
				"LinqToDB.Mapping",
			};
		}

		public override void ClearConnectionPools(IConnectionInfo cxInfo)
		{
			using (var db = new LINQPadDataConnection(cxInfo))
			{
				if (db.Connection is SqlConnection)
					SqlConnection.ClearPool((SqlConnection)db.Connection);
			}
		}

		public override void InitializeContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager)
		{
			var conn = (DataConnection)context;

			conn.OnTraceConnection = info =>
			{
				if (info.BeforeExecute)
				{
					executionManager.SqlTranslationWriter.WriteLine(info.SqlText);
				}
				else if (info.TraceLevel == TraceLevel.Error)
				{
					var sb = new StringBuilder();

					for (var ex = info.Exception; ex != null; ex = ex.InnerException)
					{
						sb
							.AppendLine()
							.AppendLine("/*")
							.AppendFormat("Exception: {0}", ex.GetType())
							.AppendLine()
							.AppendFormat("Message  : {0}", ex.Message)
							.AppendLine()
							.AppendLine(ex.StackTrace)
							.AppendLine("*/")
							;
					}

					executionManager.SqlTranslationWriter.WriteLine(sb.ToString());
				}
				else if (info.RecordsAffected != null)
				{
					executionManager.SqlTranslationWriter.WriteLine("-- Execution time: {0}. Records affected: {1}.\r\n".Args(info.ExecutionTime, info.RecordsAffected));
				}
				else
				{
					executionManager.SqlTranslationWriter.WriteLine("-- Execution time: {0}\r\n".Args(info.ExecutionTime));
				}
			};
		}

		public override void TearDownContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager, object[] constructorArguments)
		{
			((DataConnection)context).Dispose();
		}

		public override IDbConnection GetIDbConnection(IConnectionInfo cxInfo)
		{
			using (var conn = new LINQPadDataConnection(cxInfo))
				return conn.DataProvider.CreateConnection(conn.ConnectionString);
		}

		public override void ExecuteESqlQuery(IConnectionInfo cxInfo, string query)
		{
			throw new Exception ("ESQL queries are not supported for this type of connection");
		}
	}
}
