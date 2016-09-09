// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using TransactCQRS.EventStore.Builders;

namespace TransactCQRS.EventStore
{
	/// <summary>
	/// Base repository class.
	/// Used  for Transaction managment.
	/// </summary>
	public abstract class AbstractRepository
	{
		public Action<AbstractTransaction> OnTransactionSaved { get; set; }

		/// <summary>
		/// Start new transaction.
		/// </summary>
		public TTransaction StartTransaction<TTransaction>(string description) where TTransaction : AbstractTransaction
		{
			return TransactionFactory.Create<TTransaction>(this, description);
		}

		/// <summary>
		/// Load existing transaction.
		/// </summary>
		public TTransaction GetTransaction<TTransaction>(string identity) where TTransaction : AbstractTransaction
		{
			var events = LoadTransactionEvents(identity).ToArray();
			if (!events.Any())
				throw new ArgumentOutOfRangeException(nameof(identity));
			return TransactionFactory.Load<TTransaction>(this, events);
		}

		/// <summary>
		/// Get non committed transactions.
		/// </summary>
		public IEnumerable<IReference<AbstractTransaction>> GetWaitingTransactions()
		{
			var rootEvents = TransactionFactory.GetRootEventNames();
			return LoadWaitingTransactions()
				.Where(item => rootEvents.Contains(item.EventName))
				.Select(item => TransactionFactory.CreateLazyLoad(this, item, () => LoadTransactionEvents(item.Identity)));
		}

		protected abstract IEnumerable<TransactionData> LoadWaitingTransactions();
		protected internal abstract IEnumerable<EventData> LoadTransactionEvents(string identity);
		protected internal abstract IEnumerable<EventData> LoadEntity(string identity);
		protected internal abstract void SaveTransaction(int eventCount, Func<Func<string>, IEnumerable<EventData>> getEvents);
		protected internal abstract void CommitTransaction(string identity);
		protected internal abstract void RollbackTransaction(string identity);

		protected internal class TransactionData
		{
			public string Identity { get; set; }
			public string EventName { get; set; }
		}

		public class EventData
		{
			public string Transaction { get; set; }
			public string Identity { get; set; }
			public string Root { get; set; }
			public string EventName { get; set; }
			public IDictionary<string, object> Params { get; set; }
		}
	}
}