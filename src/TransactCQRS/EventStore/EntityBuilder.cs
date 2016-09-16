// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TransactCQRS.EventStore
{
	internal class EntityBuilder
	{
		private readonly Type _baseType;
		private readonly TransactionBuilder _rootBuilder;
		private readonly string _className;
		private readonly string _ownerName;

		public EntityBuilder(TransactionBuilder rootBuilder, Type baseType, string className)
		{
			_baseType = baseType;
			_rootBuilder = rootBuilder;
			_className = className;

			_ownerName = Utils.ProtectName("owner");
		}

		public string Build()
		{
			var baseClassName = _baseType.ToCsDeclaration();
			var eventsLoader = new StringBuilder();
			return $@"
						public class {_className} : {baseClassName}, IReference<{baseClassName}>
							{{
								private readonly {_rootBuilder.ClassName} _{_ownerName};
								private bool _loading;

								public bool IsLoaded => true;
								public string Identity => _{_ownerName}.GetIdentity(this);

								{BuildCostructors()}

								public {baseClassName} Load()
								{{
									return this;
								}}

								{new EventsBuilder(_rootBuilder, _baseType, _ownerName, eventsLoader).Build()}

								public {_className} LoadEvents(IEnumerable<AbstractRepository.EventData> events)
								{{
									_loading = true;
									foreach(var @event in events)
									{{
										{eventsLoader}
										throw new System.InvalidOperationException(""Unsupported type of event detected."");
									}}
									_loading = false;
									return this;
								}}

								private void AddEvent(object root, string eventName, IDictionary<string, object> @params)
								{{
									_{_ownerName}.AddEvent(root, eventName, @params);
								}}
							}}";
		}

		private string BuildCostructors()
		{
			return _baseType.GetTypeInfo().GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.Select(BuildCostructor)
				.Aggregate((result, item) => $"{result}{item}\r\n");
		}

		private string BuildCostructor(ConstructorInfo constructor)
		{
			var @params = new ParamsBuilder(constructor);
			return $@"
								public {_className}({_rootBuilder.ClassName} {_ownerName}, {@params.CreateDeclarations()})
									: base({@params.GetNameList()})
								{{
									_{_ownerName} = {_ownerName};
								}}";
		}
	}
}
