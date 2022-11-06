using System.Text;
using LINQPad.Extensibility.DataContext;
using LinqToDB.CodeModel;
using LinqToDB.Common;
using LinqToDB.DataModel;
using LinqToDB.Scaffold;
using LinqToDB.Schema;
using LinqToDB.SqlProvider;
using LinqToDB.SqlQuery;

namespace LinqToDB.LINQPad;

/// <summary>
/// Scaffold interceptor used to populate generated data model for dynamic context (with proper type/member identifiers).
/// </summary>
internal sealed class ModelProviderInterceptor : ScaffoldInterceptors
{
	private readonly ISqlBuilder _sqlBuilder;

	// stores populated model information:
	// - FK associations
	// - schema-scoped objects (views, tables, routines)
	private readonly List<AssociationData>          _associations = new ();
	private readonly Dictionary<string, SchemaData> _schemaItems  = new ();

	public ModelProviderInterceptor(ISqlBuilder sqlBuilder)
	{
		_sqlBuilder = sqlBuilder;
	}

	#region model DTOs

	private sealed   record SchemaData                   (List<TableData> Tables, List<TableData> Views, List<ProcedureData> Procedures, List<TableFunctionData> TableFunctions, List<ScalarOrAggregateFunctionData> ScalarFunctions, List<ScalarOrAggregateFunctionData> AggregateFunctions);
	private sealed   record TableData                    (string ContextName, IType ContextType, string DbName, List<ColumnData> Columns);
	private sealed   record ColumnData                   (string MemberName, IType Type, string DbName, bool IsPrimaryKey, bool IsIdentity, DataType DataType, DatabaseType DbType);
	private sealed   record AssociationData              (string MemberName, IType Type, bool FromSide, bool OneToMany, string KeyName, TableData Source, TableData Target);
	private sealed   record ResultColumnData             (string MemberName, IType Type, string DbName, DataType DataType, DatabaseType DbType);
	private sealed   record ParameterData                (string Name, IType Type, ParameterDirection Direction);
	private abstract record FunctionBaseData             (string MethodName, string DbName, IReadOnlyList<ParameterData> Parameters);
	private sealed   record ProcedureData                (string MethodName, string DbName, IReadOnlyList<ParameterData> Parameters, IReadOnlyList<ResultColumnData>? Result) : FunctionBaseData(MethodName, DbName, Parameters);
	private sealed   record TableFunctionData            (string MethodName, string DbName, IReadOnlyList<ParameterData> Parameters, IReadOnlyList<ResultColumnData> Result) : FunctionBaseData(MethodName, DbName, Parameters);
	private sealed   record ScalarOrAggregateFunctionData(string MethodName, string DbName, IReadOnlyList<ParameterData> Parameters, IType ResultType) : FunctionBaseData(MethodName, DbName, Parameters);

	public enum ParameterDirection
	{
		None,
		Ref,
		Out
	}

	#endregion

	#region Model Population

	public override void AfterSourceCodeGenerated(FinalDataModel model)
	{
		// tables lookup for association model population
		var tablesLookup = new Dictionary<EntityModel, TableData>();

		foreach (var entity      in model.Entities          ) ProcessEntity           (entity, tablesLookup);
		foreach (var association in model.Associations      ) ProcessAssociation      (association, tablesLookup);
		foreach (var proc        in model.StoredProcedures  ) ProcessStoredProcedure  (proc);
		foreach (var func        in model.TableFunctions    ) ProcessTableFunction    (func);
		foreach (var func        in model.ScalarFunctions   ) ProcessScalarFunction   (func);
		foreach (var func        in model.AggregateFunctions) ProcessAggregateFunction(func);
	}

	private SchemaData GetSchema(string? schemaName)
	{
		if (!_schemaItems.TryGetValue(schemaName ?? string.Empty, out var data))
			_schemaItems.Add(schemaName ?? string.Empty, data = new SchemaData(new(), new(), new(), new(), new(), new()));

		return data;
	}

	private void ProcessEntity(EntityModel entityModel, Dictionary<EntityModel, TableData> tablesLookup)
	{
		var schemaName = entityModel.Metadata.Name!.Value.Schema;
		var schema     = GetSchema(schemaName);
		var columns    = new List<ColumnData>();

		foreach (var column in entityModel.Columns)
		{
			columns.Add(new ColumnData(
				column.Property.Name,
				column.Property.Type!,
				column.Metadata.Name!,
				column.Metadata.IsPrimaryKey,
				column.Metadata.IsIdentity,
				column.Metadata.DataType!.Value,
				column.Metadata.DbType!));
		}

		var table = new TableData(
				entityModel.ContextProperty!.Name,
				entityModel.ContextProperty.Type!,
				GetDbName(entityModel.Metadata.Name!.Value.Name, schemaName),
				columns);

		tablesLookup.Add(entityModel, table);

		if (entityModel.Metadata.IsView)
			schema.Views.Add(table);
		else
			schema.Tables.Add(table);
	}

	private void ProcessAssociation(AssociationModel associationModel, Dictionary<EntityModel, TableData> tablesLookup)
	{
		if (   tablesLookup.TryGetValue(associationModel.Source, out var fromTable)
			&& tablesLookup.TryGetValue(associationModel.Target, out var toTable))
		{
			_associations.Add(new AssociationData(
				associationModel.Property!.Name,
				associationModel.Property.Type!,
				true,
				associationModel.ManyToOne,
				associationModel.ForeignKeyName!,
				fromTable,
				toTable));

			_associations.Add(new AssociationData(
				associationModel.BackreferenceProperty!.Name,
				associationModel.BackreferenceProperty.Type!,
				false,
				associationModel.ManyToOne,
				associationModel.ForeignKeyName!,
				toTable,
				fromTable));
		}
	}

	private void ProcessStoredProcedure(StoredProcedureModel procedureModel)
	{
		var schema     = GetSchema(procedureModel.Name.Schema);
		var parameters = CollectParameters(procedureModel.Parameters);

		List<ResultColumnData>? result = null;

		if (procedureModel.Results.Count > 0)
			result = CollectResultData(procedureModel.Results[0]);

		schema.Procedures.Add(new ProcedureData(
			procedureModel.Method.Name,
			GetDbName(procedureModel.Name.Name, procedureModel.Name.Schema),
			parameters,
			result));
	}

	private void ProcessTableFunction(TableFunctionModel functionModel)
	{
		var schema     = GetSchema(functionModel.Name.Schema);
		var parameters = CollectParameters(functionModel.Parameters);
		var result     = CollectResultData(functionModel.Result!);

		schema.TableFunctions.Add(new TableFunctionData(
			functionModel.Method.Name,
			GetDbName(functionModel.Name.Name, functionModel.Name.Schema),
			parameters,
			result));
	}

	private void ProcessAggregateFunction(AggregateFunctionModel functionModel)
	{
		var schema     = GetSchema(functionModel.Name.Schema);
		var parameters = CollectParameters(functionModel.Parameters);

		schema.AggregateFunctions.Add(new ScalarOrAggregateFunctionData(
			functionModel.Method.Name,
			GetDbName(functionModel.Name.Name, functionModel.Name.Schema),
			parameters,
			functionModel.ReturnType));
	}

	private void ProcessScalarFunction(ScalarFunctionModel functionModel)
	{
		var schema     = GetSchema(functionModel.Name.Schema);
		var parameters = CollectParameters(functionModel.Parameters);

		schema.ScalarFunctions.Add(new ScalarOrAggregateFunctionData(
			functionModel.Method.Name,
			GetDbName(functionModel.Name.Name, functionModel.Name.Schema),
			parameters,
			functionModel.Return!));
	}

	private static List<ResultColumnData> CollectResultData(FunctionResult procedureModel)
	{
		var table  = procedureModel.CustomTable!;
		var result = new List<ResultColumnData>(table.Columns.Count);

		foreach (var column in table.Columns)
		{
			result.Add(new ResultColumnData(
				column.Property.Name,
				column.Property.Type!,
				column.Metadata.Name!,
				column.Metadata.DataType!.Value,
				column.Metadata.DbType!));
		}

		return result;
	}

	private static List<ParameterData> CollectParameters(List<FunctionParameterModel> parameters)
	{
		var parametersData = new List<ParameterData>(parameters.Count);

		foreach (var param in parameters)
		{
			var direction = param.Direction switch
			{
				System.Data.ParameterDirection.InputOutput        => ParameterDirection.Ref,
				System.Data.ParameterDirection.Output
					or System.Data.ParameterDirection.ReturnValue => ParameterDirection.Out,
				_                                                 => ParameterDirection.None
			};

			parametersData.Add(new ParameterData(
				param.Parameter.Name,
				param.Parameter.Type,
				direction));
		}

		return parametersData;
	}

	#endregion

	#region Convert data model to LINQPad tree model

	public List<ExplorerItem> GetTree()
	{
		var tablesLookup = new Dictionary<TableData, ExplorerItem>();

		// don't create schema node for single schema without name (default schema)
		if (_schemaItems.Count == 1 && _schemaItems.ContainsKey(string.Empty))
		{
			var result = PopulateSchemaMembers(string.Empty, tablesLookup);
			PopulateAssociations(_associations, tablesLookup);
			return result;
		}

		var model = new List<ExplorerItem>();

		foreach (var schema in _schemaItems.Keys.OrderBy(_ => _))
		{
			model.Add(new ExplorerItem(schema, ExplorerItemKind.Schema, ExplorerIcon.Schema)
			{
				ToolTipText = $"schema: {schema}",
				SqlName     = GetDbName(schema),
				Children    = PopulateSchemaMembers(schema, tablesLookup)
			});
		}

		// associations need references to table nodes and could define cross-schema references, so we must create them after all table nodes created
		// for all schemas
		PopulateAssociations(_associations, tablesLookup);

		return model;
	}

	private List<ExplorerItem> PopulateSchemaMembers(string schemaName, Dictionary<TableData, ExplorerItem> tablesLookup)
	{
		var items = new List<ExplorerItem>();
		var data  = _schemaItems[schemaName];

		if (data.Tables.Count > 0)
			items.Add(PopulateTables(data.Tables, "Tables", ExplorerIcon.Table, tablesLookup));

		if (data.Views.Count > 0)
			items.Add(PopulateTables(data.Views, "Views", ExplorerIcon.View, tablesLookup));

		if (data.Procedures.Count > 0)
			items.Add(PopulateStoredProcedures(data.Procedures));

		if (data.TableFunctions.Count > 0)
			items.Add(PopulateTableFunctions(data.TableFunctions));

		if (data.ScalarFunctions.Count > 0)
			items.Add(PopulateScalarFunctions(data.ScalarFunctions, "Scalar Functions"));

		if (data.AggregateFunctions.Count > 0)
			items.Add(PopulateScalarFunctions(data.AggregateFunctions, "Aggregate Functions"));

		return items;
	}

	private ExplorerItem PopulateStoredProcedures(List<ProcedureData> procedures)
	{
		var items = new List<ExplorerItem>(procedures.Count);

		foreach (var func in procedures.OrderBy(f => f.MethodName))
		{
			List<ExplorerItem>? children = null;
			var size = func.Parameters.Count + (func.Result != null ? 1 : 0);

			if (size > 0)
			{
				children = new List<ExplorerItem>(size);
				AddParameters(func.Parameters, children);

				if (func.Result != null)
					AddResultTable(func.Result, children);
			}

			items.Add(new ExplorerItem(func.MethodName, ExplorerItemKind.QueryableObject, ExplorerIcon.StoredProc)
			{
				DragText     = $"{func.MethodName}({string.Join(", ", func.Parameters.Select(GetParameterName))})",
				Children     = children,
				IsEnumerable = func.Result != null,
				SqlName      = func.DbName
			});
		}

		return new ExplorerItem("Stored Procedures", ExplorerItemKind.Category, ExplorerIcon.StoredProc)
		{
			Children = items
		};
	}

	private void AddResultTable(IReadOnlyList<ResultColumnData> resultColumns, List<ExplorerItem> children)
	{
		var columns = new List<ExplorerItem>(resultColumns.Count);

		foreach (var column in resultColumns)
		{
			var dbName = GetDbName(column.DbName);
			var dbType = $"{GetTypeName(column.DataType, column.DbType)} {(column.Type.IsNullable ? "NULL" : "NOT NULL")}";

			columns.Add(new ExplorerItem($"{column.MemberName} : {SimpleBuildTypeName(column.Type)}", ExplorerItemKind.Property, ExplorerIcon.Column)
			{
				ToolTipText        = $"{dbName} {dbType}",
				DragText           = column.MemberName,
				SqlName            = dbName,
				SqlTypeDeclaration = dbType,
			});
		}

		children.Add(new ExplorerItem("Result Table", ExplorerItemKind.Category, ExplorerIcon.Table)
		{
			Children = columns
		});
	}

	private void AddParameters(IReadOnlyList<ParameterData> parameters, List<ExplorerItem> children)
	{
		foreach (var param in parameters)
			children.Add(new ExplorerItem($"{GetParameterName(param)} : {SimpleBuildTypeName(param.Type)}", ExplorerItemKind.Parameter, ExplorerIcon.Parameter));
	}

	private static string GetParameterName(ParameterData param)
	{
		return $"{(param.Direction == ParameterDirection.Out ? "out " : param.Direction == ParameterDirection.Ref ? "ref " : null)}{param.Name}";
	}

	private ExplorerItem PopulateTableFunctions(List<TableFunctionData> functions)
	{
		var items = new List<ExplorerItem>(functions.Count);

		foreach (var func in functions.OrderBy(f => f.MethodName))
		{
			var children = new List<ExplorerItem>(func.Parameters.Count + 1);

			AddParameters(func.Parameters, children);
			AddResultTable(func.Result   , children);

			items.Add(new ExplorerItem(func.MethodName, ExplorerItemKind.QueryableObject, ExplorerIcon.TableFunction)
			{
				DragText     = $"{func.MethodName}({string.Join(", ", func.Parameters.Select(GetParameterName))})",
				Children     = children,
				IsEnumerable = true,
				SqlName      = func.DbName
			});
		}

		return new ExplorerItem("Table Functions", ExplorerItemKind.Category, ExplorerIcon.TableFunction)
		{
			Children = items
		};
	}

	private ExplorerItem PopulateScalarFunctions(List<ScalarOrAggregateFunctionData> functions, string categoryName)
	{
		var items = new List<ExplorerItem>(functions.Count);

		foreach (var func in functions.OrderBy(f => f.MethodName))
		{
			List<ExplorerItem>? children = null;
			if (func.Parameters.Count > 0)
			{
				children = new List<ExplorerItem>(func.Parameters.Count);
				AddParameters(func.Parameters, children);
			}

			items.Add(new ExplorerItem(func.MethodName, ExplorerItemKind.QueryableObject, ExplorerIcon.TableFunction)
			{
				DragText     = $"{func.MethodName}({string.Join(", ", func.Parameters.Select(GetParameterName))})",
				Children     = children,
				IsEnumerable = false,
				SqlName      = func.DbName
			});
		}

		return new ExplorerItem(categoryName, ExplorerItemKind.Category, ExplorerIcon.TableFunction)
		{
			Children = items
		};
	}

	private ExplorerItem PopulateTables(List<TableData> tables, string category, ExplorerIcon icon, Dictionary<TableData, ExplorerItem> tablesLookup)
	{
		var children = new List<ExplorerItem>(tables.Count);

		foreach (var table in tables.OrderBy(t => t.ContextName))
		{
			var tableChildren = new List<ExplorerItem>(table.Columns.Count);

			foreach (var column in table.Columns)
			{
				var dbName = GetDbName(column.DbName);
				var dbType = $"{GetTypeName(column.DataType, column.DbType)} {(column.Type.IsNullable ? "NULL" : "NOT NULL")}{(column.IsIdentity ? " IDENTITY" : string.Empty)}";

				tableChildren.Add(
					new ExplorerItem(
						$"{column.MemberName} : {SimpleBuildTypeName(column.Type)}",
						ExplorerItemKind.Property,
						column.IsPrimaryKey ? ExplorerIcon.Key : ExplorerIcon.Column)
					{
						ToolTipText        = $"{dbName} {dbType}",
						DragText           = column.MemberName,
						SqlName            = dbName,
						SqlTypeDeclaration = dbType,
					});
			}

			var tableNode = new ExplorerItem(table.ContextName, ExplorerItemKind.QueryableObject, icon)
			{
				DragText     = table.ContextName,
				ToolTipText  = SimpleBuildTypeName(table.ContextType),
				SqlName      = table.DbName,
				IsEnumerable = true,
				// we don't sort columns/associations and render associations after columns intentionally
				Children     = tableChildren
			};

			tablesLookup.Add(table, tableNode);

			children.Add(tableNode);
		}

		return new ExplorerItem(category, ExplorerItemKind.Category, icon)
		{
			Children = children
		};
	}

	private void PopulateAssociations(List<AssociationData> associations, Dictionary<TableData, ExplorerItem> tablesLookup)
	{
		foreach (var association in associations)
		{
			if (tablesLookup.TryGetValue(association.Source, out var sourceNode)
				&& tablesLookup.TryGetValue(association.Target, out var targetNode))
			{
				sourceNode.Children.Add(
					new ExplorerItem(
							association.MemberName,
							association.OneToMany && association.FromSide
								? ExplorerItemKind.CollectionLink
								: ExplorerItemKind.ReferenceLink,
							association.OneToMany && association.FromSide
								? ExplorerIcon.OneToMany
								: association.OneToMany && !association.FromSide
									? ExplorerIcon.ManyToOne
									: ExplorerIcon.OneToOne)
					{
						DragText        = association.MemberName,
						ToolTipText     = $"{SimpleBuildTypeName(association.Type)}{(!association.FromSide ? " // Back Reference" : null)}",
						SqlName         = association.KeyName,
						IsEnumerable    = association.OneToMany && association.FromSide,
						HyperlinkTarget = targetNode,
					});
			}
		}
	}

	#endregion

	private string GetDbName(string name, string? schema = null)
	{
		return _sqlBuilder!.BuildObjectName(
				new StringBuilder(),
				new SqlObjectName(Name: name, Schema: schema),
				tableOptions: TableOptions.NotSet)
			.ToString();
	}

	private string GetTypeName(DataType dataType, DatabaseType type)
	{
		return _sqlBuilder!.BuildDataType(
				new StringBuilder(),
				new SqlDataType(new DbDataType(typeof(object),
					dataType : dataType,
					dbType   : type.Name,
					length   : type.Length,
					precision: type.Precision,
					scale    : type.Scale)))
			.ToString();
	}

	private readonly Dictionary<IType, string> _typeNameCache = new();

	// we use this method as we don't have type-only generation logic in scaffold framework
	// and actually we don't need such logic - simple C# type name generator below is enough for us
	private string SimpleBuildTypeName(IType type)
	{
		if (!_typeNameCache.TryGetValue(type, out var typeName))
		{
			typeName = type.Kind switch
			{
				TypeKind.Regular
					or TypeKind.TypeArgument => type.Name!.Name,
				TypeKind.Array               => $"{SimpleBuildTypeName(type.ArrayElementType!)}[]",
				TypeKind.Dynamic             => "dynamic",
				TypeKind.Generic             => $"{type.Name!.Name}<{string.Join(", ", type.TypeArguments!.Select(SimpleBuildTypeName))}>",
				TypeKind.OpenGeneric         => $"{type.Name!.Name}<{string.Join(", ", type.TypeArguments!.Select(_ => string.Empty))}>",
				_                            => throw new InvalidOperationException($"Unsupported type kind: {type.Kind}")
			};

			_typeNameCache.Add(type, typeName);
		}

		return typeName;
	}
}
