using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using LinqToDB.Data;
using LinqToDB.SchemaProvider;

using LINQPad.Extensibility.DataContext;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LinqToDB.LINQPad.Driver
{
	public class LinqToDBDriver : DynamicDataContextDriver
	{
		public override string Name   => "LINQ to DB";
		public override string Author => "Igor Tkachev";

		public override string GetConnectionDescription(IConnectionInfo cxInfo)
		{
			var providerName = (string)cxInfo.DriverData.Element("providerName");
			var name         = (string)cxInfo.DriverData.Element("name");
			var dbInfo       = cxInfo.DatabaseInfo;

			return name ?? $"[{providerName}] {dbInfo.Server}\\{dbInfo.Database} (v.{dbInfo.DbVersion})";
		}

		public override bool ShowConnectionDialog(IConnectionInfo cxInfo, bool isNewConnection)
		{
			var model        = new ConnectionViewModel();
			var providerName = (string)cxInfo.DriverData.Element("providerName");

			if (providerName != null)
				model.SelectedProvider = model.Providers.IndexOf(providerName);

			model.Name             = (string)cxInfo.DriverData.Element("name");
			model.ConnectionString = (string)cxInfo.DriverData.Element("connectionString");
			model.IncludeSchemas   = (string)cxInfo.DriverData.Element("includeSchemas");
			model.ExcludeSchemas   = (string)cxInfo.DriverData.Element("excludeSchemas");

			_cxInfo = cxInfo;

			if (ConnectionDialog.Show(this, model))
			{
				providerName = model.Providers[model.SelectedProvider];

				cxInfo.DriverData.SetElementValue("providerName",     providerName);
				cxInfo.DriverData.SetElementValue("name",             string.IsNullOrWhiteSpace(model.Name)             ? null : model.Name);
				cxInfo.DriverData.SetElementValue("connectionString", string.IsNullOrWhiteSpace(model.ConnectionString) ? null : model.ConnectionString);
				cxInfo.DriverData.SetElementValue("includeSchemas",   string.IsNullOrWhiteSpace(model.IncludeSchemas)   ? null : model.IncludeSchemas);
				cxInfo.DriverData.SetElementValue("excludeSchemas",   string.IsNullOrWhiteSpace(model.ExcludeSchemas)   ? null : model.ExcludeSchemas);

				switch (providerName)
				{
					case ProviderName.SqlServer: _cxInfo.DatabaseInfo.Provider = typeof(SqlConnection).FullName; break;
				}

				try
				{
					using (var db = new DataConnection(model.Providers[model.SelectedProvider], model.ConnectionString))
					{
						cxInfo.DatabaseInfo.Server    = ((DbConnection)db.Connection).DataSource;
						cxInfo.DatabaseInfo.Database  = db.Connection.Database;
						cxInfo.DatabaseInfo.DbVersion = ((DbConnection)db.Connection).ServerVersion;
					}
				}
				catch
				{
				}

				cxInfo.DisplayName = GetConnectionDescription(cxInfo);

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

		ExplorerItem GetTables(string header, ExplorerIcon icon, IEnumerable<TableSchema> tables)
		{
			return new ExplorerItem(header, ExplorerItemKind.Category, icon)
			{
				Children = tables
					.Select(t => new ExplorerItem(t.TableName, ExplorerItemKind.QueryableObject, icon)
					{
						IsEnumerable = true,
						Children     = t.Columns
							.Select(c => new ExplorerItem(
								c.MemberName,
								ExplorerItemKind.Property,
								c.IsPrimaryKey ? ExplorerIcon.Key : ExplorerIcon.Column)
								{
									SqlName = c.ColumnName,
								})
							.ToList()
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
			var connectionString = (string)cxInfo.DriverData.Element("connectionString");

			DatabaseSchema schema;

			using (var db = new DataConnection(providerName, connectionString))
			{
				var options = new GetSchemaOptions();

				var includeSchemas = (string)cxInfo.DriverData.Element("includeSchemas");
				if (includeSchemas != null) options.IncludedSchemas = includeSchemas.Split(',', ';');

				var excludeSchemas = (string)cxInfo.DriverData.Element("excludeSchemas");
				if (excludeSchemas != null) options.ExcludedSchemas = excludeSchemas.Split(',', ';');

				schema = db.DataProvider.GetSchemaProvider().GetSchema(db, options);
			}

			code
				.AppendLine("using System;")
				.AppendLine("using LinqToDB.Data;")
				.Append    ("namespace ").AppendLine(nameSpace)
				.AppendLine("{")
				.Append    ("  public class ").Append(typeName).AppendLine(" : DataConnection")
				.AppendLine("  {")
				.Append    ("    public ").Append(typeName).AppendLine("(string provider, string connectionString)")
				.AppendLine("        : base(provider, connectionString)")
				.AppendLine("    {}")
				.Append    ("    public ").Append(typeName).AppendLine("()")
				.Append    ("        : base(").Append(ToCodeString(providerName)).Append(", ").Append(ToCodeString(connectionString)).AppendLine(")")
				.AppendLine("    {}")
				;

			var schemas =
			(
				from t in schema.Tables
				select new { t.IsDefaultSchema, t.SchemaName }
			)
			.Union(
				from p in schema.Procedures
				select new { p.IsDefaultSchema, p.SchemaName }
			)
			.Distinct()
			.ToList();

			foreach (var s in schemas)
			{
				var tables = schema.Tables.Where(t => s.IsDefaultSchema == t.IsDefaultSchema && s.SchemaName == t.SchemaName).ToList();
				var items  = new List<ExplorerItem>();

				if (tables.Any(t => !t.IsView && !t.IsProcedureResult))
					items.Add(GetTables("Tables", ExplorerIcon.Table, tables.Where (t => !t.IsView && !t.IsProcedureResult)));

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

			code
				.AppendLine("  }")
				.AppendLine("}")
				;
		}

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
				(string)cxInfo.DriverData.Element("connectionString"),
			};
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
			};

			var compilation = CSharpCompilation.Create(
				assemblyToBuild.Name,
				syntaxTrees: new[] { syntaxTree },
				references : references,
				options    : new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

			using (var ms = new MemoryStream())
			{
				var result = compilation.Emit(ms);

				if (!result.Success)
				{
					var failures = result.Diagnostics.Where(diagnostic =>
						diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

					foreach (var diagnostic in failures)
						throw new Exception($"{diagnostic.Id}: {diagnostic.GetMessage()}");
				}
				else
				{
					ms.Seek(0, SeekOrigin.Begin);

					var assembly = Assembly.Load(ms.ToArray());
					var type     = assembly.GetType($"{nameSpace}.{typeName}");
					var obj      = Activator.CreateInstance(type);
				}
			}

			return items;
		}
	}
}
