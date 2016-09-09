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
		private Table<Transaction> _transactionTable;
		private Table<WaitingTransaction> _waitingTransactionTable;

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
			var mappingConfig = new MappingConfiguration().Define(new Map<EventData>()
					.TableName("events")
					.PartitionKey(item => item.Root)
					.ClusteringKey(item => item.Identity)
					.Column(item => item.Params, map => map.AsFrozen()),
				new Map<Transaction>()
					.TableName("transactions")
					.PartitionKey(item => item.Identity)
					.ClusteringKey(item => item.Event),
				new Map<WaitingTransaction>()
					.TableName("waiting_transactions")
					.PartitionKey(item => item.Identity));
			_eventsTable = new Table<EventData>(_session, mappingConfig);
			_eventsTable.CreateIfNotExists();
			_transactionTable = new Table<Transaction>(_session, mappingConfig);
			_transactionTable.CreateIfNotExists();
			_waitingTransactionTable = new Table<WaitingTransaction>(_session, mappingConfig);
			_waitingTransactionTable.CreateIfNotExists();
		}

		protected override IEnumerable<TransactionData> LoadWaitingTransactions()
		{
			return _waitingTransactionTable
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute()
				.Select(Convert)
				.ToArray();
		}

		protected override IEnumerable<AbstractRepository.EventData> LoadTransactionEvents(string identity)
		{
			return _eventsTable.Where(item => item.Root.Equals(Guid.Parse(identity)))
				.OrderBy(item => item.Identity)
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute()
				.Select(EventData.Convert)
				.ToArray();
		}

		protected override IEnumerable<AbstractRepository.EventData> LoadEntity(string identity)
		{
			return _eventsTable.Where(item => item.Root.Equals(Guid.Parse(identity)))
				.OrderBy(item => item.Identity)
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute()
				.Where(item => item.Committed)
				.Select(EventData.Convert)
				.ToArray();
		}

		protected override void SaveTransaction(int count, Func<Func<string>, IEnumerable<AbstractRepository.EventData>> getEvents)
		{
			var startTime = DateTimeOffset.UtcNow;
			var events = getEvents(() => TimeUuid.NewId(startTime = startTime.AddTicks(1)).ToString())
				.ToArray();
			var firstEevent = events.First();
			_session.CreateBatch()
				.Append(events.Select(EventData.Convert).Select(_eventsTable.Insert))
				.Append(events.Select(Transaction.Convert).Select(_transactionTable.Insert))
				.Append(new CqlCommand[]
				{
					_waitingTransactionTable.Insert(new WaitingTransaction
					{
						Identity = Guid.Parse(firstEevent.Transaction),
						EventName = firstEevent.EventName
					})
				})
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute();
		}

		protected override void CommitTransaction(string identity)
		{
			var transaction = Guid.Parse(identity);
			CheckTransactionWait(transaction);
			var eventsList = _transactionTable.Where(item => item.Identity.Equals(transaction))
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute();
			_session.CreateBatch()
				.Append(new []{_waitingTransactionTable.Where(item => item.Identity.Equals(transaction)).Delete()})
				.Append(eventsList.Select(@event => _eventsTable.Where(item => item.Identity.Equals(@event.Event))
					.Where(item => item.Root.Equals(@event.Root))
					.Select(item => new EventData { Committed = true })
					.Update()))
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute();
		}

		protected override void RollbackTransaction(string identity)
		{
			var transaction = Guid.Parse(identity);
			CheckTransactionWait(transaction);
			var eventList = _transactionTable.Where(item => item.Identity.Equals(transaction))
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute();
			_session.CreateBatch()
				.Append(new CqlCommand[]
				{
					_transactionTable.Where(item => item.Identity.Equals(transaction)).Delete(),
					_waitingTransactionTable.Where(item => item.Identity.Equals(transaction)).Delete()
				})
				.Append(eventList.Select(@event => _eventsTable.Where(item => item.Identity.Equals(@event.Event))
					.Where(item => item.Root.Equals(@event.Root))
					.Delete()))
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute();
		}

		private void CheckTransactionWait(Guid transaction)
		{
			var isWaiting = _waitingTransactionTable.Where(item => item.Identity.Equals(transaction))
				.SetConsistencyLevel(ConsistencyLevel.Quorum)
				.Execute()
				.Any();
			if (!isWaiting)
				throw new InvalidOperationException(Resources.TextResource.TransactionAlreadyCommited);
		}

		private static TransactionData Convert(WaitingTransaction source)
		{
			return new TransactionData { Identity = source.Identity.ToString(), EventName = source.EventName };
		}

		public class ParamDesc
		{
			public string Name { get; set; }
			public string TypeName { get; set; }
			public string Value { get; set; }
		}

		public class Transaction
		{
			public TimeUuid Identity { get; set; }
			public TimeUuid Event { get; set; }
			public TimeUuid Root { get; set; }

			internal static Transaction Convert(AbstractRepository.EventData source)
			{
				return new Transaction
				{
					Event = Guid.Parse(source.Identity),
					Identity = Guid.Parse(source.Transaction),
					Root = Guid.Parse(source.Root)
				};
			}
		}

		public class WaitingTransaction
		{
			public TimeUuid Identity { get; set; }
			public string EventName { get; set; }
		}

		public new class EventData
		{
			// ReSharper disable once MemberHidesStaticFromOuterClass
			public TimeUuid Transaction { get; set; }
			public TimeUuid Identity { get; set; }
			public TimeUuid Root { get; set; }
			public string EventName { get; set; }
			public IEnumerable<ParamDesc> Params { get; set; }
			public bool Committed { get; set; }

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
