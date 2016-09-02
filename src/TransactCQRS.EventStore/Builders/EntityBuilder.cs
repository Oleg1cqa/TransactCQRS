// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;

namespace TransactCQRS.EventStore.Builders
{
	internal class EntityBuilder
	{
		private readonly Type _entityType;
		private readonly TransactionBuilder _rootBuilder;

		public EntityBuilder(Type entityType, TransactionBuilder rootBuilder)
		{
			_entityType = entityType;
			_rootBuilder = rootBuilder;
		}

		public string Build()
		{
			var sourceName = _entityType.Name;
			var ownerName = $"_owner_{DateTime.Now.Ticks}";
			return $@"
						public class {sourceName}Impl : {_entityType.ToCsDeclaration()}, IReference<{_entityType.ToCsDeclaration()}>
							{{
								private readonly {_rootBuilder.TransactionType.Name} _{ownerName};
								private bool _loading;

								{BuildCostructors(ownerName)}

								public {_entityType.ToCsDeclaration()} Load()
								{{
									return this;
								}}

								{BuildEvents(ownerName)}

								public {sourceName}Impl LoadEvents(IEnumerable<AbstractRepository.EventData> events)
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

								private void AddEvent(object root, string eventName, IDictionary<string, object> @params)
								{{
									_{ownerName}.AddEvent(root, eventName, @params);
								}}
							}}";
		}

		private string BuildLoader()
		{
			var result = string.Empty;
			if (_rootBuilder.EntityLoaders.ContainsKey(_entityType))
				result = _rootBuilder.EntityLoaders[_entityType].ToString();
			return result;
		}

		private string BuildEvents(string ownerName)
		{
			return _rootBuilder.BuildEvents(_entityType, ownerName);
		}

		private string BuildCostructors(string ownerName)
		{
			return _entityType.GetTypeInfo().GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.Select(item => BuildCostructor(item, ownerName))
				.Aggregate((result, item) => $"{result}{item}\r\n");
		}

		private string BuildCostructor(ConstructorInfo constructor, string ownerName)
		{
			var sourceName = _entityType.Name;
			var @params = new ParamsBuilder(constructor.GetParameters());
			return $@"
								public {sourceName}Impl({_rootBuilder.TransactionType.Name} {ownerName}, {@params.CreateDeclarations()}) : base({@params.GetNameList()})
								{{
									_{ownerName} = {ownerName};
								}}";
		}
	}
}
