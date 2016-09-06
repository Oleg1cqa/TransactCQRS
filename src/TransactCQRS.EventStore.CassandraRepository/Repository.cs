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
		private Table<EventData> _eventsTable;
		private Table<CommittedTransaction> _commitedTransactionTable;
		private Table<Transaction> _transactionTable;

		public Repository(ISession session)
		{
			_session = session;
			CheckMetadata();
		}

		private void CheckMetadata()
		{
			_session.Execute("CREATE TYPE IF NOT EXISTS param_desc (name text, type_name text, value text)");
			_session.Execute("CREATE TYPE IF NOT EXISTS event_desc (identity timeuuid, root timeuuid)");
			_session.UserDefinedTypes.Define(UdtMap.For<ParamDesc>("param_desc")
				.Map(v => v.TypeName, "type_name")
				.Map(v => v.Name, "name")
				.Map(v => v.Value, "value"));
			_session.UserDefinedTypes.Define(UdtMap.For<EventDesc>("event_desc")
				.Map(v => v.Identity, "identity")
				.Map(v => v.Root, "root"));
			var mappingConfig = new MappingConfiguration().Define(new Map<EventData>()
					.TableName("events")
					.PartitionKey(item => item.Root)
					.ClusteringKey(item => item.Identity)
					.Column(item => item.Params, map => map.AsFrozen()),
				new Map<CommittedTransaction>()
					.TableName("commited_transactions")
					.PartitionKey(item => item.Root)
					.ClusteringKey(item => item.Transaction),
				new Map<Transaction>()
					.TableName("transactions")
					.PartitionKey(item => item.Identity)
					.Column(item => item.Events, map => map.AsFrozen()));
			_eventsTable = new Table<EventData>(_session, mappingConfig);
			_eventsTable.CreateIfNotExists();
			_commitedTransactionTable = new Table<CommittedTransaction>(_session, mappingConfig);
			_commitedTransactionTable.CreateIfNotExists();
			_transactionTable = new Table<Transaction>(_session, mappingConfig);
			_transactionTable.CreateIfNotExists();
		}

		protected override IEnumerable<AbstractRepository.EventData> LoadEntity(string identity)
		{
			var events = _eventsTable.Where(item => item.Root.Equals(Guid.Parse(identity)))
				.OrderBy(item => item.Identity)
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute()
				.ToArray();
			var transactions = _commitedTransactionTable.Where(item => item.Root.Equals(Guid.Parse(identity)))
				.Select(item => item.Transaction)
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute()
				.ToArray();
			return events.Where(item => transactions.Contains(item.Transaction))
				.Select(EventData.Convert)
				.ToArray();
		}

		protected override void SaveTransaction(int count, Func<Func<string>, IEnumerable<AbstractRepository.EventData>> getEvents)
		{
			var startTime = DateTimeOffset.UtcNow;
			var events = getEvents(() => TimeUuid.NewId(startTime = startTime.AddTicks(1)).ToString())
				.Select(EventData.Convert)
				.ToArray();
			_session.CreateBatch()
				.Append(events.Select(_eventsTable.Insert))
				.Append(new [] {_transactionTable.Insert(new Transaction
				{
					Identity = events[0].Transaction,
					Events = events.Select(item => new EventDesc {Identity = item.Identity, Root = item.Root})
				})})
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute();
		}

		protected override void CommitTransaction(string identity)
		{
			var transaction = LoadAndCheckTransaction(Guid.Parse(identity));
			_session.CreateBatch()
				.Append(new[] { _transactionTable.Where(item => item.Identity.Equals(Guid.Parse(identity)))
					.Select(item => new Transaction {IsCommited = true})
					.Update()})
				.Append(transaction.Events.Select(item => item.Root).Distinct().Select(item => _commitedTransactionTable.Insert(
					new CommittedTransaction {Root = item, Transaction = transaction.Identity})))
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute();
		}

		protected override void RollbackTransaction(string identity)
		{
			var transaction = LoadAndCheckTransaction(Guid.Parse(identity));
			_session.CreateBatch()
				.Append(new[] {_transactionTable.Where(item => item.Identity.Equals(transaction.Identity))
					.Delete()})
				.Append(transaction.Events.Select(@event => _eventsTable.Where(item => item.Identity.Equals(@event.Identity))
					.Where(item => item.Root.Equals(@event.Root))
					.Delete()))
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute();
		}

		private Transaction LoadAndCheckTransaction(Guid identity)
		{
			var result = _transactionTable.Where(item => item.Identity.Equals(identity))
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute()
				.Single();
			if (result.IsCommited)
				throw new InvalidOperationException(Resources.TextResource.TransactionAlreadyCommited);
			return result;
		}

		public class ParamDesc
		{
			public string Name { get; set; }
			public string TypeName { get; set; }
			public string Value { get; set; }
		}

		public class EventDesc
		{
			public Guid Identity { get; set; }
			public Guid Root { get; set; }
		}

		public class Transaction
		{
			public bool IsCommited { get; set; }
			public TimeUuid Identity { get; set; }
			public IEnumerable<EventDesc> Events { get; set; }
		}

		public class CommittedTransaction
		{
			// ReSharper disable once MemberHidesStaticFromOuterClass
			public TimeUuid Transaction { get; set; }
			public TimeUuid Root { get; set; }
		}

		public new class EventData
		{
			// ReSharper disable once MemberHidesStaticFromOuterClass
			public TimeUuid Transaction { get; set; }
			public TimeUuid Identity { get; set; }
			public TimeUuid Root { get; set; }
			public string EventName { get; set; }
			public IEnumerable<ParamDesc> Params { get; set; }

			public static AbstractRepository.EventData Convert(EventData source)
			{
				return new AbstractRepository.EventData
				{
					EventName = source.EventName,
					Identity = source.Identity.ToString(),
					Root = source.Root.ToString(),
					Transaction = source.Transaction.ToString(),
					Params = Convert(source.Params)
				};
			}

			private static IDictionary<string, object> Convert(IEnumerable<ParamDesc> @params)
			{
				return @params.ToDictionary(item => item.Name,
					item => System.Convert.ChangeType(item.Value, Type.GetType(item.TypeName, true)));
			}

			public static EventData Convert(AbstractRepository.EventData source)
			{
				return new EventData
				{
					EventName = source.EventName,
					Identity = Guid.Parse(source.Identity),
					Root = Guid.Parse(source.Root),
					Transaction = Guid.Parse(source.Transaction),
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
