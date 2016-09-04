// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using TransactCQRS.EventStore.Builders;

namespace TransactCQRS.EventStore
{
	/// <summary>
	/// Base class for override real transaction implementation.
	/// </summary>
	public abstract class AbstractTransaction : IDisposable
	{
		private ConcurrentQueue<EventData> _eventQueue;
		private readonly ConcurrentDictionary<string, object> _entities = new ConcurrentDictionary<string, object>();
		private readonly ConcurrentDictionary<object, string> _identities = new ConcurrentDictionary<object, string>();

		/// <summary>
		/// Description of transaction.
		/// </summary>
		public string Description { get; protected set; }
		public AbstractRepository Repository { get; protected set; }

		/// <summary>
		/// Load transaction from store.
		/// </summary>
		internal static TTransaction Load<TTransaction>(AbstractRepository repository, string identity) where TTransaction : AbstractTransaction
		{
			var events = repository.LoadEntity(identity).ToArray();
			var @event = events.First();
			if (@event.EventName != $"{typeof(TTransaction).Name} started." || @event.Identity != @event.Root || @event.Identity != @event.Transaction)
				throw new InvalidOperationException(Resources.TextResource.UnsupportedTransactionType);
			return (TTransaction)TransactionBuilder.CreateInstance<TTransaction>(repository, (string)@event.Params["description"])
				.LoadEvents(events.Skip(1));
		}

		/// <summary>
		/// Create new transaction.
		/// </summary>
		internal static TTransaction Create<TTransaction>(AbstractRepository repository, string description) where TTransaction : AbstractTransaction
		{
			var result = TransactionBuilder.CreateInstance<TTransaction>(repository, description);
			var @params = new Dictionary<string, object> { { "description", description } };
			result._eventQueue = new ConcurrentQueue<EventData>();
			result.AddEvent(result, $"{typeof(TTransaction).Name} started.", @params);
			return result;
		}

		public TEntity GetEntity<TEntity>(string identity) where TEntity : class
		{
			TEntity result;
			object loaded;
			if (_entities.TryGetValue(identity, out loaded))
			{
				result = loaded as TEntity;
				if (result == null) throw new ArgumentOutOfRangeException(nameof(identity));
			}
			else
			{
				var loadedEvents = Repository.LoadEntity(identity).ToArray();
				if (!loadedEvents.Any()) throw new ArgumentOutOfRangeException(nameof(identity));
				result = LoadEntity<TEntity>(loadedEvents);
				_entities.TryAdd(identity, result);
				_identities.TryAdd(result, identity);
			}
			return result;
		}

		public string GetIdentity(object entity)
		{
			string result;
			if (!_identities.TryGetValue(entity, out result))
				throw new InvalidOperationException(Resources.TextResource.EntityHaventIdentity);
			return result;
		}

		public void Commit()
		{
			Repository.Commit(_eventQueue.Count, MakeCommit);
		}

		private IEnumerable<AbstractRepository.EventData> MakeCommit(Func<string> getNextIdentity)
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

		public void Dispose()
		{
			Interlocked.Exchange(ref _eventQueue, null);
		}

		public static bool HaveEqualParamNames(IDictionary<string, object> @params, params string[] names)
		{
			{
				return @params.Count == names.Length && names.All(@params.ContainsKey);
			}
		}

		internal void SetIdentity(object @event, string identity)
		{
			_identities.TryAdd(@event, identity);
			_entities.TryAdd(identity, @event);
		}

		protected abstract TEntity LoadEntity<TEntity>(IEnumerable<AbstractRepository.EventData> events) where TEntity : class;

		protected abstract AbstractTransaction LoadEvents(IEnumerable<AbstractRepository.EventData> events);

		protected void AddEvent(object root, string eventName, IDictionary<string, object> @params)
		{
			_eventQueue.Enqueue(new EventData { Root = root, EventName = eventName, Params = @params });
		}

		private class EventData
		{
			public object Root { get; set; }
			public string EventName { get; set; }
			public IDictionary<string, object> Params { get; set; }
		}
	}
}