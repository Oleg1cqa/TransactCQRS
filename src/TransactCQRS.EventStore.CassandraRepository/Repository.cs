// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;

namespace TransactCQRS.EventStore.CassandraRepository
{
	public class Repository : AbstractRepository
	{
		private readonly ISession _session;
		private Table<EventData> _table;

		public Repository(ISession session)
		{
			_session = session;
			CheckMetadata();
		}

		private void CheckMetadata()
		{
			_session.Execute("CREATE TYPE IF NOT EXISTS param_desc (name text, type_name text, value text)");
			_session.UserDefinedTypes.Define(UdtMap.For<ParamDesc>("param_desc")
					.Map(v => v.TypeName, "type_name")
					.Map(v => v.Name, "name")
					.Map(v => v.Value, "value"));
			var config = new Map<EventData>()
				.TableName("events_queue")
				.PartitionKey(item => item.Root)
				.ClusteringKey(item => item.Identity)
				.Column(item => item.Params, map => map.AsFrozen());
			var mappingConfig = new MappingConfiguration().Define(config);
			_table = new Table<EventData>(_session, mappingConfig);
			_table.CreateIfNotExists();
		}

		protected override IEnumerable<AbstractRepository.EventData> LoadEntity(string identity)
		{
			return (from item in _table where item.Root.Equals(identity) select item)
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute()
				.OrderBy(item => item.Position)
				.Select(EventData.Convert)
				.ToArray();
		}

		protected override void Commit(int count, Func<Func<string>, IEnumerable<AbstractRepository.EventData>> getEvents)
		{
			var startTime = DateTimeOffset.UtcNow;
			var batch = _session.CreateBatch();
			batch.Append(getEvents(GetNewId).Select(item => EventData.Convert(item, startTime = startTime.AddMilliseconds(1), count))
				.Select(_table.Insert));
			batch.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute();
		}

		private static string GetNewId()
		{
			return Guid.NewGuid().ToString();
		}

		public class ParamDesc
		{
			public string Name { get; set; }
			public string TypeName { get; set; }
			public string Value { get; set; }
		}

		public new class EventData
		{
			public DateTimeOffset Position { get; set; }
			public int Count { get; set; }
			public string Transaction { get; set; }
			public string Identity { get; set; }
			public string Root { get; set; }
			public string EventName { get; set; }
			public IEnumerable<ParamDesc> Params { get; set; }

			public static AbstractRepository.EventData Convert(EventData source)
			{
				return new AbstractRepository.EventData
				{
					EventName = source.EventName,
					Identity = source.Identity,
					Root = source.Root,
					Transaction = source.Transaction,
					Params = Convert(source.Params)
				};
			}

			private static IDictionary<string, object> Convert(IEnumerable<ParamDesc> @params)
			{
				return @params.ToDictionary(item => item.Name,
					item => System.Convert.ChangeType(item.Value, Type.GetType(item.TypeName, true)));
			}

			public static EventData Convert(AbstractRepository.EventData source, DateTimeOffset position, int count)
			{
				return new EventData
				{
					Position = position,
					Count = count,
					EventName = source.EventName,
					Identity = source.Identity,
					Root = source.Root,
					Transaction = source.Transaction,
					Params = Convert(source.Params),
				};
			}

			private static IEnumerable<ParamDesc> Convert(IDictionary<string, object> @params)
			{
				return @params.Select(item => new ParamDesc
				{
					Name = item.Key,
					Value = System.Convert.ToString(item.Value),
					TypeName = item.Value.GetType().FullName
				});
			}
		}
	}
}
