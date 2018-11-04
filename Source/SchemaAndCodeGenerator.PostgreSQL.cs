using LinqToDB.SchemaProvider;
using System.Collections.Generic;
using System.Linq;

namespace LinqToDB.LINQPad
{
	partial class SchemaAndCodeGenerator
	{
		private void PreprocessPostgreSQLSchema()
		{
			PgSqlFixTableFunctions();
			PgSqlFixRecordResultFunctions();
			PgSqlFixVoidFunctions();
			PgSqlFixFunctionNames();
		}

		void PgSqlFixRecordResultFunctions()
		{
			var mappings = new List<(string, IList<string>)>();

			foreach (var proc in _schema.Procedures
				.Where(p => p.IsFunction && !p.IsAggregateFunction && !p.IsTableFunction && p.Parameters.Any(pr => pr.IsOut)))
			{
				if (proc.Parameters.Count(pr => pr.IsOut) > 1)
				{
					var result = new TableSchema()
					{
						TypeName          = SchemaProviderBase.ToValidName(proc.ProcedureName + "Result"),
						Columns           = new List<ColumnSchema>(),
						IsProcedureResult = true,
						ForeignKeys       = new List<ForeignKeySchema>()
					};

					_schema.Tables.Add(result);
					proc.ResultTable = result;

					proc.Parameters.Add(new ParameterSchema()
					{
						IsResult      = true,
						ParameterType = result.TypeName
					});

					var resultMappings = new List<string>();
					mappings.Add((result.TypeName, resultMappings));

					foreach (var outParam in proc.Parameters.Where(_ => _.IsOut))
					{
						//outParam.ParameterType, outParam.ParameterName, null, null
						result.Columns.Add(new ColumnSchema()
						{
							MemberType = outParam.ParameterType,
							MemberName = outParam.ParameterName
						});

						resultMappings.Add($"{outParam.ParameterName} = ({outParam.ParameterType})tuple[{resultMappings.Count}]");

						if (outParam.IsIn)
							outParam.IsOut = false;
					}

					proc.Parameters = proc.Parameters.Where(_ => !_.IsOut).ToList();

				}
				else // one parameter
				{
					var param = proc.Parameters.Single(_ => _.IsOut);
					proc.Parameters.Remove(param);
					proc.Parameters.Add(new ParameterSchema()
					{
						IsResult      = true,
						ParameterType = param.ParameterType
					});
				}
			}

			if (mappings.Count > 0)
			{
				Code
					.AppendLine("    protected override void InitMappingSchema()")
					.AppendLine("    {");

				foreach (var (typeName, valueMappings) in mappings)
				{
					Code.AppendLine($"        MappingSchema.SetConvertExpression<object[], {typeName}>(tuple => new {typeName}() {{ {string.Join(", ", valueMappings)} }});");
				}

				Code
					.AppendLine("    }");
			}
		}

		void PgSqlFixFunctionNames()
		{
			foreach (var proc in _schema.Procedures)
			{
				if (proc.ProcedureName.Any(char.IsUpper))
					proc.ProcedureName = "\"" + proc.ProcedureName + "\"";
			}
		}

		void PgSqlFixTableFunctions()
		{
			foreach (var proc in _schema.Procedures
				.Where(p => p.IsTableFunction && p.Parameters.Any(pr => pr.IsOut)))
			{
				proc.Parameters = proc.Parameters.Where(pr => !pr.IsOut).ToList();
			}
		}

		void PgSqlFixVoidFunctions()
		{
			// generated functions should return object for void-typed functions
			foreach (var proc in _schema.Procedures
				.Where(p => p.IsFunction && !p.IsTableFunction && !p.Parameters.Any(pr => pr.IsResult)))
			{
				proc.Parameters.Add(new ParameterSchema()
				{
					IsResult      = true,
					ParameterType = "object",
					SystemType    = typeof(object)
				});
			}
		}
	}
}
