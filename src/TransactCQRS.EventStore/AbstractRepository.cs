// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace TransactCQRS.EventStore
{
	/// <summary>
	/// Base repository class.
	/// Used  for Transaction managment.
	/// </summary>
	public abstract class AbstractRepository : ITransactionTrailer
	{
		public ITransactionReceiver Queue { get; set; }

		/// <summary>
		/// Start new transaction.
		/// </summary>
		public TTransaction StartTransaction<TTransaction>(string description) where TTransaction : AbstractTransaction
		{
			return AbstractTransaction.Create<TTransaction>(this, description);
		}

		/// <summary>
		/// Load existing transaction.
		/// </summary>
		public TTransaction GetTransaction<TTransaction>(string identity) where TTransaction : AbstractTransaction
		{
			return AbstractTransaction.Load<TTransaction>(this, identity);
		}

		protected internal abstract IEnumerable<EventData> LoadEntity(string identity);
		protected internal abstract void Commit(int count, Func<Func<string>, IEnumerable<EventData>> getEvents);
		protected abstract void CommitTransaction(string identity);
		protected abstract void FailTransaction(string identity);

		void ITransactionTrailer.Commit<TTransaction>(TTransaction source)
		{
			CommitTransaction(source.GetIdentity());
		}

		void ITransactionTrailer.Fail<TTransaction>(TTransaction source)
		{
			FailTransaction(source.GetIdentity());
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