// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TransactCQRS.BlockChain;

namespace TransactCQRS.Tests.BlockChain
{
	public class TestRepository : IRepository, IBlockFactory
	{
		private readonly List<TestTransaction> _transactions = new List<TestTransaction>();
		private int _identity;
		private readonly object _lockObject = new object();

		internal void AddTransaction()
		{
			lock (_lockObject)
			{
				_transactions.Add(new TestTransaction
				{
					CreateDate = DateTimeOffset.UtcNow,
					Identity = Interlocked.Increment(ref _identity).ToString()
				});
			}
		}

		internal IEnumerable<ITransaction> GetAllTransactions()
		{
			lock (_lockObject)
			{
				return _transactions.ToArray();
			}
		}

#pragma warning disable 1998
		public async Task<IEnumerable<ITransaction>> GetUncommittedTransactions()
		{
			lock (_lockObject)
			{
				return _transactions.Where(item => !item.IsCommitted).ToArray();
			}
		}

		public async Task StartNewElection()
		{
			lock (_lockObject)
			{
				_transactions.OfType<TestBlock>()
					.ToList()
					.ForEach(item => item.Voters.Clear());
			}
		}

		public async Task Commit(IBlock value)
		{
			lock (_lockObject)
			{
				var block = ((TestBlock)value);
				block.IsCommitted = true;
				block.Transactions.ToList().ForEach(item => item.IsCommitted = true);
				_transactions.RemoveAll(item => item is TestBlock && !item.IsCommitted);
			}
		}

		public IBlock Create(IEnumerable<ITransaction> transactions, string mainerIdentity)
		{
			lock (_lockObject)
			{
				var blocks = _transactions.OfType<TestBlock>().Where(item => item.IsCommitted).ToArray();
				var maxPosition = blocks.Any() ? blocks.Max(item => item.Position) : 0;
				return new TestBlock
				{
					CreateDate = DateTimeOffset.UtcNow,
					Position = maxPosition + 1,
					PreviousBlock = blocks.SingleOrDefault(item => item.Position == maxPosition),
					MainerIdentity = mainerIdentity,
					Identity = Interlocked.Increment(ref _identity).ToString(),
					Transactions = transactions.Cast<TestTransaction>().ToArray()
				};
			}
		}

		public async Task Delete(IEnumerable<IBlock> value)
		{
			lock (_lockObject)
			{
				value.ToList()
					.ForEach(item => _transactions.Remove((TestBlock) item));
			}
		}

		public async Task Save(IBlock value)
		{
			lock (_lockObject)
			{
				_transactions.Add((TestTransaction)value);
			}
		}

		public async Task Vote(IBlock block, string voter)
#pragma warning restore 1998
		{
			lock (_lockObject)
			{
				((TestBlock) block).Voters.Add(voter);
			}
		}
	}
}
