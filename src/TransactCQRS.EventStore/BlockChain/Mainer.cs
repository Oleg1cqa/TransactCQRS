// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TransactCQRS.EventStore.BlockChain
{
	public class Mainer
	{
		private readonly IRepository _repository;
		private readonly IBlockFactory _factory;
		private readonly int _mainersCount;
		private readonly string _mainerIdentity;

		public Mainer(IRepository repository, IBlockFactory factory, int mainersCount, string mainerIdentity)
		{
			_repository = repository;
			_factory = factory;
			_mainersCount = mainersCount;
			_mainerIdentity = mainerIdentity;
		}

		public void Start()
		{
			// Имеем x майнеров, интервал опроса d миллисекунд.
			var branch = FindBetterBranch().ToArray();

			// Найти необработанные транзакции
			var transactions = _repository.GetUncommittedTransactions().ToList();
			transactions.RemoveAll((item => branch.Any(block => block.ContainsTransaction(item))));
			// Проверить их
			// Создать из них блок и записать
			if (transactions.Count == 0 && branch.Any()) // Nothing to commit. But we possible can finish chain.
			{
				// Если необработанных транзакций нет то убедится что мы подписали текущую цепочку если нет
				// Создать пустой блок для подписи и записать.
				// Проверим и подтвердим в обработанных если уже есть более половины от майнеров блоков.
				var finished = branch.TakeWhile(item => item.TransactionCount == 0)
					.Any(item => item.MainerIdentity == _mainerIdentity);
				if (!finished)
				{

					//finished = branch.FirstOrDefault(item => item.TransactionCount == 0);
				}
				if (!finished)
					_repository.Save(_factory.Create(branches: branch));
			}
			else if (transactions.Count > 0 && branch.Any()) // Need commit transactions.
			{
				var newBlock = _factory.Create(transactions, branch);
				transactions.ToList().ForEach(item => item.Check(newBlock));
				_repository.Save(newBlock);
			}
			else if (transactions.Count > 0 && !branch.Any()) // We haven't blocks. We need start new chain.
			{
				var newBlock = _factory.Create(transactions);
				transactions.ToList().ForEach(item => item.Check(newBlock));
				_repository.Save(newBlock);
			}
		}

		/// <summary>
		/// Find better branch.
		/// </summary>
		private IEnumerable<IBlock> FindBetterBranch()
		{
			// Загрузим цепочку из последних n блоков.
			// Проверить цепочку и построить дерево.
			// Если дерево не построилось грузим еще блоки пока не будет дерева.
			var result = new List<IBlock>();
			var tree = new BlockTree(result);
			var pageSize = _mainersCount * 10;
			string searchTag = null;
			do result.AddRange(_repository.GetLastBlocks(pageSize, ref searchTag));
				while (tree.GetRootCount() != 1);
			if (result.Count == 0)
				return result; // We haven't blocks. We need start new.

			// Если есть ветви, решить какую транзакцию мы поддерживаем.
			var ends = tree.GetBranchEnds().ToArray();
			if (ends.Length == 0)
				throw new InvalidOperationException(Resources.TextResource.BlockChainEndNotFound);
			if (ends.Length == 1)
				return result; // We have only one branch
			// решить какую транзакцию мы поддерживаем.
			// (Цепочка с большим количеством майнеров, Цепочка с большим количеством транзакций,
			// если количество одинаково то та которая созданна раньше)
			return tree.GetAllBranches()
				.Select(item =>
				{
					item = item.ToArray();
					return new
					{
						Branch = item,
						MainersCount = item.Select(block => block.MainerIdentity).Distinct().Count(),
						TransactionCount = item.Sum(block => block.TransactionCount),
						StartDate = item.Min(block => block.CreateDate)
					};
				})
				.OrderByDescending(item => item.MainersCount)
				.ThenByDescending(item => item.TransactionCount)
				.ThenByDescending(item => item.StartDate)
				.Select(item => item.Branch)
				.First();
		}

		public interface IBlock
		{
			DateTimeOffset CreateDate { get; }
			string Identity { get; }
			string MainerIdentity { get; }
			string ParentBlock { get; }
			int TransactionCount { get; }

			bool ContainsTransaction(ITransaction value);
		}

		/// <summary>
		/// Block factory interface.
		/// </summary>
		public interface IBlockFactory
		{
			/// <summary>
			/// Create new chain block.
			/// </summary>
			/// <param name="transactions">Transactions that was included in this block.</param>
			/// <param name="branches">Brach for block appending.</param>
			/// <returns></returns>
			IBlock Create(IEnumerable<ITransaction> transactions = null, IEnumerable<IBlock> branches = null);
		}

		public interface ITransaction
		{
			void Check(IBlock newBlock);
		}

		public interface IRepository
		{
			IEnumerable<IBlock> GetLastBlocks(int initialCount, ref string searchTag);
			IEnumerable<ITransaction> GetUncommittedTransactions();
			void Save(IBlock value);
		}
	}
}
