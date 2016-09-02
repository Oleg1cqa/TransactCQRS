using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis.Emit;
using System.Reflection;

namespace TransactCQRS.EventStore.Builders
{
	internal static class Utils
	{
		public static string ProtectName(string source)
		{
			return $"{source}_{DateTime.Now.Ticks}";;
		}

		public static Type CompileAndLoad(TransactionBuilder builder)
		{
			var compilation = CSharpCompilation.Create("CQRSDynamicCode.dll",
				options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
				syntaxTrees: new[] { CSharpSyntaxTree.ParseText(builder.BuildClass()) },
				references: BuildReferences(builder.BaseType));
			using (var stream = new MemoryStream())
			{
				CheckCompilation(compilation.Emit(stream,
					options: new EmitOptions(outputNameOverride: ProtectName("assembly"))));
				stream.Position = 0;
				return AssemblyLoadContext.Default.LoadFromStream(stream)
					.GetType(builder.QualifiedClassName);
			}
		}

		private static void CheckCompilation(EmitResult source)
		{
			if (!source.Success)
				throw new InvalidOperationException(string.Format(Resources.TextResource.TransactionCompilationFailed,
					source.Diagnostics.Select(item => item.ToString()).Aggregate((result, item) => $"{result} {item}.")));
		}

		private static IEnumerable<MetadataReference> BuildReferences(Type sourceType)
		{
			yield return MetadataReference.CreateFromFile(sourceType.GetTypeInfo().Assembly.Location);
			yield return MetadataReference.CreateFromFile(typeof(AbstractTransaction).GetTypeInfo().Assembly.Location);
			yield return MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location);
			yield return MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location);

			var assemblyPath = Path.GetDirectoryName(typeof(object).GetTypeInfo().Assembly.Location);
			yield return MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "mscorlib.dll"));
			yield return MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll"));
		}
	}
}
