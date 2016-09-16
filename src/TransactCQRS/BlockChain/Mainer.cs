// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TransactCQRS.BlockChain
{
	/// <summary>
	/// Mainer for committing block chain of transactions.
	/// </summary>
	public class Mainer
	{
		private readonly IRepository _repository;
		private readonly IBlockFactory _factory;
		private readonly int _mainersCount;
		private readonly string _mainerIdentity;

		public Mainer(IRepository repository, IBlockFactory factory, int mainersCount, string mainerIdentity)
		{
			if (repository == null) throw new ArgumentNullException(nameof(repository));
			if (factory == null) throw new ArgumentNullException(nameof(factory));
			if (string.IsNullOrEmpty(mainerIdentity)) throw new ArgumentNullException(nameof(mainerIdentity));
			if (mainersCount < 3) throw new ArgumentOutOfRangeException(nameof(mainersCount));
			_repository = repository;
			_factory = factory;
			_mainersCount = mainersCount;
			_mainerIdentity = mainerIdentity;
		}

		/// <summary>
		/// Commit block when we have one win election.
		/// </summary>
		public async Task CommitWinBlock(IEnumerable<ITransaction> source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			var blocks = source.OfType<IBlock>().ToArray();
			if (!blocks.Any()) return;
			var generations = blocks.Select(item => item.Position).Distinct();
			if (generations.Count() != 1) return;
			var maxVotes = blocks.Max(item => item.Votes + 1);
			if (maxVotes <= _mainersCount/2) return;
			var winBlock = blocks.Single(item => item.Votes + 1 == maxVotes);
			if (winBlock.MainerIdentity == _mainerIdentity)
				await _repository.Commit(winBlock);
		}

		/// <summary>
		/// Vote for block if we can vote.
		/// </summary>
		public async Task VoteForBlock(IEnumerable<ITransaction> source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			var blocks = source.OfType<IBlock>().ToArray();
			if (!blocks.Any()) return;
			var generations = blocks.Select(item => item.Position).Distinct();
			if (generations.Count() != 1) return;
			var maxVotes = blocks.Max(item => item.Votes + 1);
			if (maxVotes > _mainersCount/2) return;
			if (blocks.Any(item => item.MainerIdentity == _mainerIdentity) ||
			    blocks.Any(item => item.HaveVoteFrom(_mainerIdentity))) return;
			var block = blocks.OrderByDescending(item => item.Votes).ThenBy(item => item.CreateDate).First();
			await _repository.Vote(block, _mainerIdentity);
		}

		/// <summary>
		/// Create Block if we have transactions and haven't block
		/// </summary>
		public async Task CreateBlock(IEnumerable<ITransaction> source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			var transactions = source.ToArray();
			if (!transactions.Any()) return;
			if (transactions.OfType<IBlock>().Any()) return;
			await _repository.Save(_factory.Create(transactions, _mainerIdentity));
		}

		/// <summary>
		/// Delete generations if we have more than one
		/// </summary>
		public async Task ClearGenerations(IEnumerable<ITransaction> source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			var blocks = source.OfType<IBlock>().ToArray();
			if (!blocks.Any()) return;
			var generations = blocks.Select(item => item.Position).Distinct().ToArray();
			if (generations.Length <= 1) return;
			var maxGeneration = generations.Max();
			await _repository.Delete(blocks.Where(item => item.Position != maxGeneration));
		}

		/// <summary>
		/// Clean votes if locked state detected.
		/// </summary>
		public async Task<long?> StartNewElection(IEnumerable<ITransaction> source, long? generation)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			var blocks = source.OfType<IBlock>().ToArray();
			if (blocks.Length < 3) return null;
			var generations = blocks.Select(item => item.Position).Distinct().ToArray();
			if (generations.Length != 1) return null;
			var allVotes = blocks.Sum(item => item.Votes + 1);
			if (allVotes <= _mainersCount/2) return null;
			var maxVotes = blocks.Max(item => item.Votes + 1);
			if (maxVotes > _mainersCount/2) return null;
			if (generation != generations[0])
				return generations[0];
			await _repository.StartNewElection();
			return null;
		}

		/// <summary>
		/// Commit block when we have one win election.
		/// </summary>
		public async Task<IBlock> CommitForgottenBlock(IEnumerable<ITransaction> source, IBlock block)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			var blocks = source.OfType<IBlock>().ToArray();
			if (!blocks.Any()) return null;
			var generations = blocks.Select(item => item.Position).Distinct().ToArray();
			if (generations.Length != 1) return null;
			var maxVotes = blocks.Max(item => item.Votes + 1);
			if (maxVotes <= _mainersCount/2) return null;
			var winBlock = blocks.Single(item => item.Votes + 1 == maxVotes);
			if (winBlock.MainerIdentity == _mainerIdentity) return null;
			if (block == null || winBlock.Identity != block.Identity) 
				return winBlock;
			await _repository.Commit(winBlock);
			return null;
		}

		/// <summary>
		/// Start Mainer.
		/// </summary>
		public IEnumerable<Task> StartAll(CancellationToken cancelToken)
		{
			if (cancelToken == null) throw new ArgumentNullException(nameof(cancelToken));
			yield return StartCycle(CommitWinBlock, cancelToken);
			yield return StartCycle(VoteForBlock, cancelToken);
			yield return StartCycle(CreateBlock, cancelToken);
			yield return StartCycle(ClearGenerations, cancelToken, 250, 400);
			yield return StartDoubleCycle<long?>(StartNewElection, cancelToken);
			yield return StartDoubleCycle<IBlock>(CommitForgottenBlock, cancelToken);
		}

		private async Task StartCycle(Func<IEnumerable<ITransaction>,Task> action, CancellationToken cancelToken,
			int minTime = 150, int randomPartTime = 200)
		{
			var random = new Random();
			while (!cancelToken.IsCancellationRequested)
			{
				await action(await _repository.GetUncommittedTransactions());
				await Task.Delay(TimeSpan.FromMilliseconds(minTime + random.Next(randomPartTime)), cancelToken);
			}
			cancelToken.ThrowIfCancellationRequested();
		}

		private async Task StartDoubleCycle<TReturn>(Func<IEnumerable<ITransaction>,TReturn,Task<TReturn>> action,
			CancellationToken cancelToken, int minTime = 200, int randomPartTime = 200)
		{
			var random = new Random();
			while (!cancelToken.IsCancellationRequested)
			{
				var result = default(TReturn);
				while (!EqualityComparer<TReturn>.Default.Equals(
					result = await action(await _repository.GetUncommittedTransactions(), result), default(TReturn)))
				{
					await Task.Delay(TimeSpan.FromMilliseconds(minTime + random.Next(randomPartTime)), cancelToken);
				}
				await Task.Delay(TimeSpan.FromMilliseconds(minTime + random.Next(randomPartTime)), cancelToken);
			}
			cancelToken.ThrowIfCancellationRequested();
		}
	}
}
