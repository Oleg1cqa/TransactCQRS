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

		protected override IEnumerable<EventData> LoadEntity(string identity)
		{
			return EventQueue.Where(item => item.Root == identity)
				.Select(item => new EventData
				{
					EventName = item.EventName,
					Identity = item.Identity,
					Root = item.Root,
					Transaction = item.Transaction,
					Params = new Dictionary<string, object>(item.Params)
				})
				.OrderBy(item => int.Parse(item.Identity))
				.ToArray();
		}

		protected override void Commit(int count, Func<Func<string>, IEnumerable<EventData>> getEvents)
		{
			var startIdentity = _maxIdentity;
			_maxIdentity += count;
			var result = getEvents(() => startIdentity++.ToString()).ToArray();
			CheckSupportedParameters(result);
			EventQueue.AddRange(result);
		}

		private void CheckSupportedParameters(EventData[] events)
		{
			if (events.SelectMany(item => item.Params.Values)
				.Any(item => item.IsSupportedClass()))
				throw new ArgumentOutOfRangeException(nameof(events), Resources.TextResource.OnlyValueTypeSupported);

		}
	}
}