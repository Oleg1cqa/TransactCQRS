// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace TransactCQRS.EventStore
{
	internal class LazyLoadTransaction : IReference<AbstractTransaction>
	{
		private readonly Func<AbstractTransaction> _loadFunc;
		private AbstractTransaction _transaction;

		public string Identity { get; }

		public AbstractTransaction Transaction => _transaction ?? Load();
		public bool IsLoaded => Transaction != null;

		public LazyLoadTransaction(string identity, Func<AbstractTransaction> loadFunc)
		{
			_loadFunc = loadFunc;
			Identity = identity;
		}

		public AbstractTransaction Load()
		{
			return _transaction = _loadFunc();
		}
	}
}