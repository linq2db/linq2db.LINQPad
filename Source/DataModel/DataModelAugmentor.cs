﻿using LinqToDB.CodeModel;

namespace LinqToDB.LINQPad;

sealed class DataModelAugmentor : ConvertCodeModelVisitor
{
	private static readonly CodeReference _commandTimeoutProperty = WellKnownTypes.PropertyOrField((LINQPadDataConnection ctx) => ctx.CommandTimeout, false);

	private readonly IEqualityComparer<IType> _typeComparer;
	private readonly IType _baseContextType;
	private readonly string? _providerName;
	private readonly string? _assemblyPath;
	private readonly string _connectionName;
	private readonly CodeBuilder _ast;
	private readonly int _commandTimeout;

	public DataModelAugmentor(ILanguageProvider languageProvider, IType baseContextType,
		string? providerName,
		string? assemblyPath,
		string connectionName,
		int commandTimeout)
	{
		_ast = languageProvider.ASTBuilder;
		_typeComparer = languageProvider.TypeEqualityComparerWithoutNRT;
		_baseContextType = baseContextType;

		_providerName = providerName;
		_assemblyPath = assemblyPath;
		_connectionName = connectionName;
		_commandTimeout = commandTimeout;
	}

	protected override ICodeElement Visit(CodeClass @class)
	{
		if (@class.Inherits != null && _typeComparer.Equals(@class.Inherits.Type, _baseContextType))
		{
			var members = @class.Members.ToList();
			var constructors = new ConstructorGroup(@class);
			members.Add(constructors);

			// context found
			@class = new CodeClass(
				@class.CustomAttributes,
				@class.Attributes,
				@class.XmlDoc,
				@class.Type,
				@class.Name,
				@class.Parent,
				@class.Inherits,
				@class.Implements,
				members,
				@class.TypeInitializer);

			constructors.Class = @class;

			var parametrisedCtor = constructors.New()
				.SetModifiers(Modifiers.Public);
			var providerParam = _ast.Parameter(WellKnownTypes.System.String, _ast.Name("provider"), CodeParameterDirection.In);
			var assemblyPathParam = _ast.Parameter(WellKnownTypes.System.String.WithNullability(true), _ast.Name("assemblyPath"), CodeParameterDirection.In);
			var connectionStringParam = _ast.Parameter(WellKnownTypes.System.String, _ast.Name("connectionString"), CodeParameterDirection.In);

			parametrisedCtor
				.Parameter(providerParam)
				.Parameter(assemblyPathParam)
				.Parameter(connectionStringParam)
				.Base(providerParam.Reference, assemblyPathParam.Reference, connectionStringParam.Reference);

			parametrisedCtor
				.Body()
				.Append(_ast.Assign(_ast.Member(@class.This, _commandTimeoutProperty), _ast.Constant(_commandTimeout, true)));

			return @class;
		}

		return base.Visit(@class);
	}
}
