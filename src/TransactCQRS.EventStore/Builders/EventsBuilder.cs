// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TransactCQRS.EventStore.Builders
{
	internal class EventsBuilder
	{
		private readonly Type _sourceType;
		private readonly string _ownerName;
		private readonly StringBuilder _eventsLoader;
		private readonly TransactionBuilder _rootBuilder;

		public EventsBuilder(TransactionBuilder rootBuilder, Type sourceType, string ownerName, StringBuilder eventsLoader)
		{
			_sourceType = sourceType;
			_ownerName = ownerName;
			_eventsLoader = eventsLoader;
			_rootBuilder = rootBuilder;
		}

		public string Build()
		{
			var result = _sourceType.GetTypeInfo()
				.DeclaredMethods
				.Where(item => item.GetCustomAttribute<EventAttribute>() != null)
				.Select(BuildEvent)
				.ToArray();
			return result.Any() ? result.Aggregate((aggr, item) => $"{aggr}{item}\r\n") : string.Empty;
		}

		// Example: public virtual void SetDescription(string value)
		private string BuildEvent(MethodInfo method)
		{
			if (!method.IsPublic) throw new InvalidOperationException(string.Format(Resources.TextResource.MethodShouldBePublic, method.Name));
			if (!method.IsVirtual) throw new InvalidOperationException(string.Format(Resources.TextResource.MethodShouldBeVirtual, method.Name));
			if (method.IsAbstract) return BuildAbstractEvent(method);
			if (method.ReturnType != typeof(void)) throw new InvalidOperationException(string.Format(Resources.TextResource.MethodShouldReturnVoid, method.Name));
			var paramsBuilder = new ParamsBuilder(method);
			_eventsLoader.AppendLine(BuildEventMethodCall(method.Name, paramsBuilder));
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

		private string BuildEventMethodCall(string eventName, ParamsBuilder paramsBuilder)
		{
			return $@"
							if (@event.EventName == ""{eventName}"" && AbstractTransaction.HaveEqualParamNames(@event.Params{paramsBuilder.GetQuotedList()})) 
							{{
								{paramsBuilder.BuildParameterConversion(_ownerName)}
								{eventName}({paramsBuilder.CreateParamsWith("@event.Params")});
							}} else";
		}

		// Example: public abstract TestEntity CreateTestEntity(string name);
		private string BuildAbstractEvent(MethodInfo method)
		{
			if (!method.ReturnType.GetTypeInfo().IsClass) throw new InvalidOperationException(string.Format(Resources.TextResource.MethodShouldReturnClass, method.Name));
			var paramsBuilder = new ParamsBuilder(method);
			_rootBuilder.BuildEntity(method, paramsBuilder);
			var entityClassName = _rootBuilder.Entities[method.ReturnType];
			return $@"
						public override {method.ReturnType.ToCsDeclaration()} {method.Name}({paramsBuilder.CreateDeclarations()})
						{{
							if (_loading) throw new System.InvalidOperationException(""Invalid call in loading state."");
							var result = new {entityClassName}(_{_ownerName}, {paramsBuilder.GetNameList()});
							var @params = new Dictionary<string,object> {{{paramsBuilder.CreateDictionaryParams()}}};
							AddEvent(result, ""{method.Name}"", @params);
							return result;
						}}";
		}
	}
}
