// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TransactCQRS.MemoryRepository
{
	public class Repository : AbstractRepository
	{
		private List<EventData> EventQueue { get; } = new List<EventData>();
		private readonly object _lockObj = new object();
		private int _maxIdentity;

		protected override IEnumerable<TransactionData> LoadWaitingTransactions()
		{
			lock(_lockObj)
				return EventQueue.Where(item => !item.TransactionCommitted)
					.Where(item => item.Root == item.Identity)
					.Where(item => item.Root == item.Transaction)
					.OrderBy(item => int.Parse(item.Identity))
					.Select(item => new TransactionData {Identity = item.Identity, EventName = item.EventName})
					.ToArray();
		}

		protected override IEnumerable<AbstractRepository.EventData> LoadTransactionEvents(string identity)
		{
			lock (_lockObj)
				return EventQueue.Where(item => item.Root == identity)
					.Select(EventData.Clone)
					.OrderBy(item => int.Parse(item.Identity))
					.ToArray();
		}

		protected override IEnumerable<AbstractRepository.EventData> LoadEntity(string identity)
		{
			lock (_lockObj)
				return EventQueue.Where(item => item.Root == identity)
					.Where(item => item.TransactionCommitted)
					.Select(EventData.Clone)
					.OrderBy(item => int.Parse(item.Identity))
					.ToArray();
		}

		protected override void SaveTransaction(int count, Func<Func<string>, IEnumerable<AbstractRepository.EventData>> getEvents)
		{
			var startIdentity = Interlocked.Add(ref _maxIdentity, count) - count;
			var result = getEvents(() => startIdentity++.ToString())
				.Select(EventData.Clone)
				.ToArray();
			lock (_lockObj)
			{
				EventQueue.AddRange(result);
			}
		}

		protected override void CommitTransaction(string identity)
		{
			lock (_lockObj)
				EventQueue.Where(item => item.Transaction == identity)
					.ToList()
					.ForEach(item => item.TransactionCommitted = true);
		}

		protected override void RollbackTransaction(string identity)
		{
			lock (_lockObj)
				EventQueue.RemoveAll(item => item.Transaction == identity);
		}

		private new class EventData : AbstractRepository.EventData
		{
			public bool TransactionCommitted { get; set; }

			public static EventData Clone(AbstractRepository.EventData source)
			{
				return new EventData
				{
					EventName = source.EventName,
					Identity = source.Identity,
					Root = source.Root,
					Transaction = source.Transaction,
					Params = new Dictionary<string, object>(source.Params)
				};
			}
		}
	}
}