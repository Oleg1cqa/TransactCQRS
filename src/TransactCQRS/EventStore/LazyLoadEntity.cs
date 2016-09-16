﻿// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace TransactCQRS.EventStore
{
	public class LazyLoadEntity<TEntity> : IReference<TEntity> where TEntity : class
	{
		public AbstractTransaction Transaction { get; }
		/// <inheritdoc/>
		public string Identity { get; }
		public TEntity Entity { get; private set; }

		public static implicit operator TEntity(LazyLoadEntity<TEntity> source)
		{
			return source.Entity;
		}

		public LazyLoadEntity(AbstractTransaction transaction, string identity)
		{
			Transaction = transaction;
			Identity = identity;
		}

		/// <inheritdoc/>
		public bool IsLoaded => Entity != null;

		/// <inheritdoc/>
		public TEntity Load()
		{
			return Entity ?? (Entity = Transaction.GetEntity<TEntity>(Identity));
		}
	}
}