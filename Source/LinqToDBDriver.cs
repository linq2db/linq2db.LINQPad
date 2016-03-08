using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

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
			return ".......";
		}

		public override bool ShowConnectionDialog(IConnectionInfo cxInfo, bool isNewConnection)
		{
			return false;
		}

		public override List<ExplorerItem> GetSchemaAndBuildAssembly(
			IConnectionInfo cxInfo, AssemblyName assemblyToBuild, ref string nameSpace, ref string typeName)
		{
			var syntaxTree = CSharpSyntaxTree.ParseText(@"
				using System;

				namespace RoslynCompileSample
				{
					public class Writer
					{
						public void Write(string message)
						{
							Console.WriteLine(message);
						}
					}
				}");


			var references   = new MetadataReference[]
			{
				MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
				MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
			};

			var compilation = CSharpCompilation.Create(
				assemblyToBuild.Name,
				syntaxTrees: new[] { syntaxTree },
				references : references,
				options    : new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

			return new List<ExplorerItem>();
		}
	}
}
