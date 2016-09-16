﻿// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TransactCQRS.EventStore
{
	internal class TransactionBuilder
	{
		private readonly StringBuilder _entityClasses = new StringBuilder();
		private readonly StringBuilder _entityLoaders = new StringBuilder();
		private readonly string _ownerName;

		public readonly Dictionary<Type, string> Entities = new Dictionary<Type, string>();
		public string ClassName { get; }
		public string QualifiedClassFactoryName => $"TransactCQRS.DynamicCode.{ClassName}+Factory";
		public Type BaseType { get; }

		internal TransactionBuilder(Type transactionType)
		{
			BaseType = transactionType;

			ClassName = Utils.ProtectName(transactionType.Name);
			_ownerName = Utils.ProtectName("owner");
		}

		public string BuildClass()
		{
			var baseTypeName = BaseType.ToCsDeclaration();
			var rootEventName = BaseType.FullName;
			var eventsLoader = new StringBuilder();
			var result = $@"
				using System;
				using System.Linq;
				using System.Collections.Generic;
				using TransactCQRS.EventStore;
				using TransactCQRS;
				
				namespace TransactCQRS.DynamicCode
				{{
					public class {$"{ClassName}"} : {baseTypeName}, IReference<{baseTypeName}>
					{{
						private {ClassName} _{_ownerName} => this;
						private bool _loading;

						public bool IsLoaded => true;
						public string Identity => GetIdentity(this);

						public override string Description {{ get;}}
						public override AbstractRepository Repository {{ get;}}
						public override Type BaseType {{ get; }} = typeof({baseTypeName});

						public {$"{ClassName}"}(AbstractRepository repository, string description)
						{{
							Repository = repository;
							Description = description;
							StartQueue();
							var @params = new Dictionary<string, object> {{ {{ ""description"", description}} }};
							AddEvent(this, ""{rootEventName}"", @params);
						}}

						public {$"{ClassName}"}(AbstractRepository repository, IEnumerable<AbstractRepository.EventData> events)
						{{
							Repository = repository;
							var eventsArr = events.ToArray();
							var @event = eventsArr.First();
							if (@event.EventName != ""{rootEventName}"" || @event.Identity != @event.Root || @event.Identity != @event.Transaction)
								throw new InvalidOperationException(""Unsupported transaction type."");
							Description = (string)@event.Params[""description""];
							LoadEvents(eventsArr.Skip(1));
							SetIdentity(this, @event.Identity);
						}}

						public {baseTypeName} Load()
						{{
							return this;
						}}

						{new EventsBuilder(this, BaseType, _ownerName, eventsLoader).Build()}

						protected override TEntity LoadEntity<TEntity>(IEnumerable<AbstractRepository.EventData> events)
						{{
							var @event = events.First();
							events = events.Skip(1);
							{_entityLoaders}
							throw new System.InvalidOperationException(""Unsupported type of entity detected."");
						}}

						private AbstractTransaction LoadEvents(IEnumerable<AbstractRepository.EventData> events)
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

						public new void AddEvent(object root, string eventName, IDictionary<string, object> @params)
						{{
							base.AddEvent(root, eventName, @params);
						}}

						{_entityClasses}

						protected override bool IsSupportedType(Type type)
						{{
							{BuildIsSupportedType()}
							return false;
						}}

						public class Factory : ITransactionFactory
						{{
							AbstractTransaction ITransactionFactory.Create(AbstractRepository repository, string description)
							{{
								return new {ClassName}(repository, description);
							}}

							AbstractTransaction ITransactionFactory.Load(AbstractRepository repository, IEnumerable<AbstractRepository.EventData> events)
							{{
								return new {ClassName}(repository, events);
							}}

							string ITransactionFactory.RootEventName {{ get; }} = ""{rootEventName}"";
						}}
					}}
				}}";
			return result;
		}

		private string BuildIsSupportedType()
		{
			return Entities.Select(item => $"if(type == typeof({item.Key.ToCsDeclaration()})) return true;")
				.Aggregate((result, item) => $"{result}\r\n{item}");
		}

		public void BuildEntity(MethodInfo method, ParamsBuilder paramsBuilder)
		{
			_entityClasses.AppendLine(BuildEntityClass(method.ReturnType));
			_entityLoaders.AppendLine(BuildEntityLoader(method.ReturnType, method.Name, paramsBuilder));
		}

		private string BuildEntityClass(Type sourceType)
		{
			if (Entities.ContainsKey(sourceType))
				return string.Empty;
			var className = Utils.ProtectName(sourceType.Name);
			Entities.Add(sourceType, className);
			return new EntityBuilder(this, sourceType, className).Build();
		}

		private string BuildEntityLoader(Type resultType, string eventName, ParamsBuilder paramsBuilder)
		{
			var @params = paramsBuilder.CreateParamsWith("@event.Params");
			@params = string.IsNullOrEmpty(@params) ? "this" : $"this, {@params}";
			var entityClassName = Entities[resultType];
			return $@"
							if (typeof(TEntity) == typeof({resultType.ToCsDeclaration()}) && @event.EventName == ""{eventName}""
								&& AbstractTransaction.HaveEqualParamNames(@event.Params{paramsBuilder.GetQuotedList()})) 
							{{
								{paramsBuilder.BuildParameterConversion(_ownerName)}
								return (TEntity)(object)(new {entityClassName}({@params}).LoadEvents(events));
							}}";
		}
	}
}