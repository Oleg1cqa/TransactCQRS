// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TransactCQRS.EventStore.MemoryRepository
{
	public class Repository : AbstractRepository
	{
		private List<EventData> EventQueue { get; } = new List<EventData>();
		private int _maxIdentity;

		protected override IEnumerable<AbstractRepository.EventData> LoadTransaction(string identity)
		{
			return EventQueue.Where(item => item.Root == identity)
				.Select(EventData.Clone)
				.OrderBy(item => int.Parse(item.Identity));
		}

		protected override IEnumerable<AbstractRepository.EventData> LoadEntity(string identity)
		{
			return EventQueue.Where(item => item.Root == identity)
				.Where(item => item.TransactionCommitted)
				.Select(EventData.Clone)
				.OrderBy(item => int.Parse(item.Identity));
		}

		protected override void SaveTransaction(int count, Func<Func<string>, IEnumerable<AbstractRepository.EventData>> getEvents)
		{
			var startIdentity = _maxIdentity;
			_maxIdentity += count;
			var result = getEvents(() => startIdentity++.ToString())
				.Select(EventData.Clone)
				.ToArray();
			CheckSupportedParameters(result);
			EventQueue.AddRange(result);
		}

		protected override void CommitTransaction(string identity)
		{
			EventQueue.Where(item => item.Transaction == identity)
				.ToList()
				.ForEach(item => item.TransactionCommitted = true);
		}

		protected override void RollbackTransaction(string identity)
		{
			EventQueue.RemoveAll(item => item.Transaction == identity);
		}

		private void CheckSupportedParameters(EventData[] events)
		{
			if (events.SelectMany(item => item.Params.Values)
				.Any(item => item.IsSupportedClass()))
				throw new ArgumentOutOfRangeException(nameof(events), Resources.TextResource.OnlyValueTypeSupported);
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