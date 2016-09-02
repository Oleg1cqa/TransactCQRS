// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace TransactCQRS.EventStore.Builders
{
	internal class TransactionBuilder
	{
		private static readonly Dictionary<Type, Type> TypeCache = new Dictionary<Type, Type>();
		private static readonly object LockObject = new object();

		private readonly List<Type> _entities = new List<Type>();
		private readonly StringBuilder _entitiesCode = new StringBuilder();
		private readonly StringBuilder _loaders = new StringBuilder();

		internal readonly Dictionary<Type, StringBuilder> EntityLoaders = new Dictionary<Type, StringBuilder>();
		internal readonly Type TransactionType;

		private TransactionBuilder(Type transactionType)
		{
			TransactionType = transactionType;
		}

		public static TTransaction CreateInstance<TTransaction>(AbstractRepository repository, string description) where TTransaction : AbstractTransaction
		{
			lock (LockObject)
			{
				Type result;
				if (!TypeCache.TryGetValue(typeof(TTransaction), out result))
				{
					result = new TransactionBuilder(typeof(TTransaction)).Create();
					TypeCache.Add(typeof(TTransaction), result);
				}
				return (TTransaction) Activator.CreateInstance(result, repository, description);
			}
		}

		private Type Create()
		{
			var compilation = CSharpCompilation.Create(
				"CQRSDynamicCode.dll",
				options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
				syntaxTrees:
				new[] { CSharpSyntaxTree.ParseText(BuildClass()) },
				references: BuildReferences());
			using (var stream = new MemoryStream())
			{
				CheckCompilation(compilation.Emit(stream,
					options: new EmitOptions(outputNameOverride: $"assembly_{DateTime.Now.Ticks}")));
				stream.Position = 0;
				return AssemblyLoadContext.Default.LoadFromStream(stream)
					.GetType($"CQRSDynamicCode.{TransactionType.Name}");
			}
		}

		private static void CheckCompilation(EmitResult source)
		{
			if (!source.Success)
				throw new InvalidOperationException(string.Format(Resources.TextResource.TransactionCompilationFailed,
					source.Diagnostics.Select(item => item.ToString()).Aggregate((result, item) => $"{result} {item}.")));
		}

		private  IEnumerable<MetadataReference> BuildReferences()
		{
			yield return MetadataReference.CreateFromFile(TransactionType.GetTypeInfo().Assembly.Location);
			yield return MetadataReference.CreateFromFile(GetType().GetTypeInfo().Assembly.Location);
			yield return MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location);
			yield return MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location);

			var assemblyPath = Path.GetDirectoryName(typeof(object).GetTypeInfo().Assembly.Location);
			yield return MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "mscorlib.dll"));
			yield return MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll"));
		}

		private string BuildClass()
		{
			var ownerName = $"owner_{DateTime.Now.Ticks}";
			string baseTypeName = TransactionType.ToCsDeclaration();
			var result = $@"
				using System;
				using System.Linq;
				using System.Collections.Generic;
				using TransactCQRS.EventStore;

				namespace CQRSDynamicCode
				{{
					public class {$"{TransactionType.Name}"} : {baseTypeName}, IReference<{baseTypeName}>
					{{
						private {TransactionType.Name} _{ownerName} => this;
						private bool _loading;

						public bool IsLoaded => true;
						public string Identity => GetIdentity(this);

						public {$"{TransactionType.Name}"}(AbstractRepository repository, string description)
						{{
							Repository = repository;
							Description = description;
						}}

						public {baseTypeName} Load()
						{{
							return this;
						}}

						{BuildEvents(TransactionType, ownerName)}

						protected override TEntity LoadEntity<TEntity>(IEnumerable<AbstractRepository.EventData> events)
						{{
							var @event = events.First();
							events = events.Skip(1);
							{_loaders}
							throw new System.InvalidOperationException(""Unsupported type of entity detected."");
						}}

						protected override AbstractTransaction LoadEvents(IEnumerable<AbstractRepository.EventData> events)
						{{
							_loading = true;
							foreach(var @event in events)
							{{
								{BuildLoader()}
								throw new System.InvalidOperationException(""Unsupported type of event detected."");
							}}
							_loading = false;
							return this;
						}}

						public new void AddEvent(object root, string eventName, IDictionary<string, object> @params)
						{{
							base.AddEvent(root, eventName, @params);
						}}

						{_entitiesCode}
					}}
				}}";
			return result;
		}

		private string BuildLoader()
		{
			if (EntityLoaders.ContainsKey(TransactionType))
				return EntityLoaders[TransactionType].ToString();
			return string.Empty;
		}

		public string BuildEvents(Type sourceType, string ownerName)
		{
			var result = sourceType.GetTypeInfo()
				.DeclaredMethods
				.Where(item => item.GetCustomAttribute<EventAttribute>() != null)
				.Select(item => BuildEvent(item, ownerName))
				.ToArray();
			return result.Any() ? result.Aggregate((aggr, item) => $"{aggr}{item}\r\n") : string.Empty;
		}

		// Example: public virtual void SetDescription(string value)
		private string BuildEvent(MethodInfo method, string ownerName)
		{
			if (!method.IsPublic) throw new InvalidOperationException(string.Format(Resources.TextResource.MethodShouldBePublic, method.Name));
			if (!method.IsVirtual) throw new InvalidOperationException(string.Format(Resources.TextResource.MethodShouldBeVirtual, method.Name));
			if (method.IsAbstract) return BuildAbstractEvent(method, ownerName);
			if (method.ReturnType != typeof(void)) throw new InvalidOperationException(string.Format(Resources.TextResource.MethodShouldReturnVoid, method.Name));
			var paramsBuilder = new ParamsBuilder(method.GetParameters());
			GetEntityLoaders(method.DeclaringType).AppendLine(BuildEntityLoader(method.Name, paramsBuilder));
			return $@"
						public override void {method.Name}({paramsBuilder.CreateDeclarations()})
						{{
							if (!_loading)
							{{
								var @params = new Dictionary<string,object> {{{paramsBuilder.CreateDictionaryParams()}}};
								AddEvent(this, ""{method.Name}"", @params);
							}}
							base.{method.Name}({paramsBuilder.GetNameList()});
						}}";
		}

		private StringBuilder GetEntityLoaders(Type entityType)
		{
			if (EntityLoaders.ContainsKey(entityType))
				return EntityLoaders[entityType];
			var result = new StringBuilder();
			EntityLoaders[entityType] = result;
			return result;
		}

		private string BuildEntityLoader(string eventName, ParamsBuilder paramsBuilder)
		{
			return $@"
							if (@event.EventName == ""{eventName}"" && AbstractTransaction.HaveEqualParamNames(@event.Params{paramsBuilder.GetQuotedList()})) 
								{eventName}({paramsBuilder.CreateParamsWith("@event.Params")}); else";
		}

		// Example: public abstract TestEntity CreateTestEntity(string name);
		private string BuildAbstractEvent(MethodInfo method, string ownerName)
		{
			if (!method.ReturnType.GetTypeInfo().IsClass) throw new InvalidOperationException(string.Format(Resources.TextResource.MethodShouldReturnClass, method.Name));
			var paramsBuilder = new ParamsBuilder(method.GetParameters());
			var sourceName = method.ReturnType.Name;
			_entitiesCode.AppendLine(BuildEntityClass(method.ReturnType));
			_loaders.AppendLine(BuildLoader(method.ReturnType, method.Name, paramsBuilder));
			return $@"
						public override {method.ReturnType.ToCsDeclaration()} {method.Name}({paramsBuilder.CreateDeclarations()})
						{{
							if (_loading) throw new System.InvalidOperationException(""Invalid call in loading state."");
							var result = new {sourceName}Impl(_{ownerName}, {paramsBuilder.GetNameList()});
							var @params = new Dictionary<string,object> {{{paramsBuilder.CreateDictionaryParams()}}};
							AddEvent(result, ""{method.Name}"", @params);
							return result;
						}}";
		}

		private static string BuildLoader(Type resultType, string eventName, ParamsBuilder paramsBuilder)
		{
			var @params = paramsBuilder.CreateParamsWith("@event.Params");
			if (!string.IsNullOrEmpty(@params))
				@params = $", {@params}";
			return $@"
							if (typeof(TEntity) == typeof({resultType.ToCsDeclaration()}) && @event.EventName == ""{eventName}"") 
								return (TEntity)(object)(new {resultType.Name}Impl(this{@params}).LoadEvents(events));";
		}

		private string BuildEntityClass(Type sourceType)
		{
			if (_entities.Contains(sourceType))
				return string.Empty;
			_entities.Add(sourceType);
			return new EntityBuilder(sourceType, this).Build();
		}
	}
}