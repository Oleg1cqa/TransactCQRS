// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace TransactCQRS.EventStore
{
	internal class TransactionFactory
	{
		private static readonly ConcurrentDictionary<Type, ITransactionFactory> TypeFactories = new ConcurrentDictionary<Type, ITransactionFactory>();
		private static readonly object Lockobject = new object();

		internal static Type GetTypeByRootEvent(string eventName)
		{
			foreach (var item in TypeFactories.ToImmutableArray())
			{
				if (item.Value.RootEventName == eventName)
					return item.Key;
			}
			throw new ArgumentOutOfRangeException(nameof(eventName));
		}

		internal static IEnumerable<string> GetRootEventNames()
		{
			return TypeFactories.Values
				.Select(item => item.RootEventName)
				.ToArray();
		}

		internal static TTransaction Load<TTransaction>(AbstractRepository repository, IEnumerable<AbstractRepository.EventData> events)
			where TTransaction : AbstractTransaction
		{
			return (TTransaction)GetFactory(typeof(TTransaction)).Load(repository, events);
		}

		/// <summary>
		/// Create new transaction.
		/// </summary>
		internal static TTransaction Create<TTransaction>(AbstractRepository repository, string description)
			where TTransaction : AbstractTransaction
		{
			return (TTransaction)GetFactory(typeof(TTransaction)).Create(repository, description);
		}

		internal static LazyLoadTransaction CreateLazyLoad(AbstractRepository repository, AbstractRepository.TransactionData transaction,
			Func<IEnumerable<AbstractRepository.EventData>> getEvents)
		{
			var factory = GetFactory(GetTypeByRootEvent(transaction.EventName));
			return new LazyLoadTransaction(transaction.Identity, () => factory.Load(repository, getEvents()));
		}

		private static ITransactionFactory GetFactory(Type transactionType)
		{
			lock (Lockobject)
				return TypeFactories.GetOrAdd(transactionType, item => CompileAndLoad(new TransactionBuilder(item)));
		}

		public static ITransactionFactory CompileAndLoad(TransactionBuilder builder)
		{
			var compilation = CSharpCompilation.Create("CQRSDynamicCode.dll",
				options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
				syntaxTrees: new[] { CSharpSyntaxTree.ParseText(builder.BuildClass()) },
				references: BuildReferences(builder.BaseType));
			using (var stream = new MemoryStream())
			{
				CheckCompilation(compilation.Emit(stream,
					options: new EmitOptions(outputNameOverride: Utils.ProtectName("assembly"))));
				stream.Position = 0;
				return (ITransactionFactory)Activator.CreateInstance(
					AssemblyLoadContext.Default.LoadFromStream(stream)
						.GetType(builder.QualifiedClassFactoryName, true));
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
