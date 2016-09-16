// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace TransactCQRS.EventStore
{
	/// <summary>
	/// Base class for override real transaction implementation.
	/// </summary>
	public abstract class AbstractTransaction
	{
		private ConcurrentQueue<EventData> _eventQueue;
		private readonly ConcurrentDictionary<string, object> _entities = new ConcurrentDictionary<string, object>();
		private readonly ConcurrentDictionary<object, string> _identities = new ConcurrentDictionary<object, string>();

		/// <summary>
		/// Description of transaction.
		/// </summary>
		public abstract string Description { get;}
		public abstract AbstractRepository Repository { get;}
		public abstract Type BaseType { get; }

		public TEntity GetEntity<TEntity>(string identity) where TEntity : class
		{
			TEntity result;
			if (TryGetEntity(identity, out result))
				return result;
			throw new ArgumentOutOfRangeException(nameof(identity));
		}

		public bool TryGetEntity<TEntity>(string identity, out TEntity result) where TEntity : class
		{
			if (!IsSupportedType(typeof(TEntity))) throw new InvalidOperationException(Resources.TextResource.UnsupportedTypeOfEntity);
			result = null;
			object loaded;
			if (_entities.TryGetValue(identity, out loaded))
			{
				result = loaded as TEntity;
				if (result == null) throw new ArgumentOutOfRangeException(nameof(identity));
			}
			else
			{
				var loadedEvents = Repository.LoadEntity(identity).ToArray();
				if (!loadedEvents.Any())
					return false;
				result = LoadEntity<TEntity>(loadedEvents);
				_entities.TryAdd(identity, result);
				_identities.TryAdd(result, identity);
			}
			return true;
		}

		public string GetIdentity(object entity)
		{
			string result;
			if (!_identities.TryGetValue(entity, out result))
				throw new InvalidOperationException(Resources.TextResource.EntityHaventIdentity);
			return result;
		}

		public void Save()
		{
			if (_eventQueue == null)
				throw new InvalidOperationException(Resources.TextResource.TransactionReadOnly);
			Repository.SaveTransaction(_eventQueue.Count, GetEventsForSave);
			Interlocked.Exchange(ref _eventQueue, null);
			Repository.OnTransactionSaved?.Invoke(this);
		}

		public void Commit()
		{
			string identity;
			if (!_identities.TryGetValue(this, out identity))
				Save();
			Repository.CommitTransaction(GetIdentity(this));
		}

		public void Rollback()
		{
			Repository.RollbackTransaction(GetIdentity(this));
		}

		private IEnumerable<AbstractRepository.EventData> GetEventsForSave(Func<string> getNextIdentity)
		{
			string transactionIdentity = null;
			EventData @event;
			while (_eventQueue.TryDequeue(out @event))
			{
				var result = new AbstractRepository.EventData
				{
					Transaction = transactionIdentity,
					EventName = @event.EventName,
					Params = ConvertParams(@event.Params)
				};
				var eventTransaction = @event.Root as AbstractTransaction;
				string outRoot;
				if (eventTransaction != null)
				{
					if (transactionIdentity == null)
					{
						transactionIdentity = getNextIdentity();
						result.Transaction = transactionIdentity;
						result.Root = transactionIdentity;
						result.Identity = transactionIdentity;
						SetIdentity(@event.Root, transactionIdentity);
					}
					else
					{
						result.Root = transactionIdentity;
						result.Identity = getNextIdentity();
					}
				}
				else if (_identities.TryGetValue(@event.Root, out outRoot))
				{
					result.Identity = getNextIdentity();
					result.Root = outRoot;
				}
				else
				{
					result.Root = result.Identity = getNextIdentity();
					SetIdentity(@event.Root, result.Identity);
				}
				yield return result;
			}
		}

		private IDictionary<string, object> ConvertParams(IDictionary<string, object> @params)
		{
			foreach (var key in @params.Keys.ToImmutableArray())
			{
				if (!@params[key].IsSupportedClass()) continue;
				string result;
				if (_identities.TryGetValue(@params[key], out result))
					@params[key] = result;
				else if (@params[key].IsIReference())
					@params[key] = @params[key].GetIdentity();
				else
					throw new ArgumentOutOfRangeException(nameof(@params), Resources.TextResource.OnlyValueTypeSupported);
			}
			return @params;
		}

		public static bool HaveEqualParamNames(IDictionary<string, object> @params, params string[] names)
		{
			{
				return @params.Count == names.Length && names.All(@params.ContainsKey);
			}
		}

		protected abstract TEntity LoadEntity<TEntity>(IEnumerable<AbstractRepository.EventData> events) where TEntity : class;

		protected abstract bool IsSupportedType(Type type);

		protected void AddEvent(object root, string eventName, IDictionary<string, object> @params)
		{
			if (_eventQueue == null)
				throw new InvalidOperationException(Resources.TextResource.TransactionReadOnly);
			_eventQueue.Enqueue(new EventData { Root = root, EventName = eventName, Params = @params });
		}

		protected void SetIdentity(object @event, string identity)
		{
			_identities.TryAdd(@event, identity);
			_entities.TryAdd(identity, @event);
		}

		protected void StartQueue()
		{
			_eventQueue = new ConcurrentQueue<EventData>();
		}

		private class EventData
		{
			public object Root { get; set; }
			public string EventName { get; set; }
			public IDictionary<string, object> Params { get; set; }
		}
	}
}