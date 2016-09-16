// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using TransactCQRS.BlockChain;
using Xunit;

namespace TransactCQRS.Tests.BlockChain
{
	public class MainerBehavior
	{
		[Fact]
		public void ShouldFailedWhenConstructingWithInvalidArguments()
		{
			var factory = new Mock<IBlockFactory>();
			var repository = new Mock<IRepository>();

			Assert.Throws<ArgumentNullException>(() => new Mainer(null, factory.Object, 5, "sdfsdf"));
			Assert.Throws<ArgumentNullException>(() => new Mainer(repository.Object, null, 5, "sdfsdf"));
			Assert.Throws<ArgumentOutOfRangeException>(() => new Mainer(repository.Object, factory.Object, 0, "sdfsdf"));
			Assert.Throws<ArgumentNullException>(() => new Mainer(repository.Object, factory.Object, 5, string.Empty));
			Assert.Throws<ArgumentNullException>(() => new Mainer(repository.Object, factory.Object, 5, null));
		}

		[Fact]
		public async Task ShouldWorkAllTogether()
		{
			var repository = new TestRepository();
			var cancelationSource = new CancellationTokenSource();
			var cancelationToken = cancelationSource.Token;
			const int transactions = 10000;

			var tasks = Enumerable.Range(0, 5)
				.SelectMany(item => new Mainer(repository, repository, 5, $"miner{item}").StartAll(cancelationToken))
				.ToList();
			tasks.Add(Task.Run(async () =>
			{
				var random = new Random();
				await Task.Delay(TimeSpan.FromMilliseconds(random.Next(1000)), cancelationToken);
				Enumerable.Range(0, transactions/2)
					.ToList()
					.ForEach(item => repository.AddTransaction());
				await Task.Delay(TimeSpan.FromMilliseconds(random.Next(1000)), cancelationToken);
				Enumerable.Range(0, transactions/2)
					.ToList()
					.ForEach(item => repository.AddTransaction());
			}, cancelationToken));

			cancelationSource.CancelAfter(TimeSpan.FromSeconds(6));
			try
			{
				Task.WaitAll(tasks.ToArray());
			}
			catch (AggregateException ex)
			{
				if (!ex.InnerExceptions.All(item => item is TaskCanceledException))
					throw;
			}

			Assert.Equal(0, (await repository.GetUncommittedTransactions()).Count());
			var result = repository.GetAllTransactions().ToArray();
			Assert.True(result.Length > transactions);
			var blocks = result.OfType<TestBlock>().ToArray();
			Assert.True(blocks.Length > 1);
			Assert.Equal(transactions, result.Length - blocks.Length);
			Assert.True(blocks.GroupBy(item => item.Position).All(item => item.Count() == 1));
			Assert.True(blocks.All(item => item.Transactions.Any()));
			Assert.Equal(transactions, blocks.Sum(item => item.Transactions.Count()));
			var maxPosition = blocks.Max(item => item.Position);
			var current = blocks.Single(item => item.Position == maxPosition);
			var position = current.Position;
			var chainLength = 1;
			while ((current = blocks.SingleOrDefault(item => item == current.PreviousBlock)) != null)
			{
				Assert.Equal(position, current.Position + 1);
				position = current.Position;
				chainLength++;
			}
			Assert.Equal(blocks.Length, chainLength);
		}

		[Fact]
		public async Task ShouldFailedWithNullArguments()
		{
			var repository = new TestRepository();
			var mainer = new Mainer(repository, repository, 5, "MainerIdentity");

			await Assert.ThrowsAsync<ArgumentNullException>(() => mainer.CommitWinBlock(null));
			await Assert.ThrowsAsync<ArgumentNullException>(() => mainer.VoteForBlock(null));
			await Assert.ThrowsAsync<ArgumentNullException>(() => mainer.CreateBlock(null));
			await Assert.ThrowsAsync<ArgumentNullException>(() => mainer.ClearGenerations(null));
			await Assert.ThrowsAsync<ArgumentNullException>(() => mainer.StartNewElection(null, null));
			await Assert.ThrowsAsync<ArgumentNullException>(() => mainer.CommitForgottenBlock(null, null));
		}

		[Fact]
		public async Task ShouldCommitWinBlock()
		{
			var repository = new Mock<IRepository>(MockBehavior.Strict);
			repository.Setup(foo => foo.Commit(It.IsAny<IBlock>())).Returns(Task.CompletedTask).Verifiable();
			var factory = new Mock<IBlockFactory>(MockBehavior.Strict);
			var mainer = new Mainer(repository.Object, factory.Object, 5, "MainerIdentity");
			var block = new TestBlock { MainerIdentity = "MainerIdentity", Voters = { "MainerIdentity1", "MainerIdentity2" } };

			await mainer.CommitWinBlock(new[] { block });

			repository.Verify();
			factory.Verify();
		}

		[Fact]
		public async Task ShouldVoteForBlock()
		{
			var repository = new Mock<IRepository>(MockBehavior.Strict);
			repository.Setup(foo => foo.Vote(It.IsAny<IBlock>(), It.IsAny<string>())).Returns(Task.CompletedTask).Verifiable();
			var factory = new Mock<IBlockFactory>(MockBehavior.Strict);
			var mainer = new Mainer(repository.Object, factory.Object, 5, "MainerIdentity");
			var block = new TestBlock { MainerIdentity = "MainerIdentity1" };

			await mainer.VoteForBlock(new[] { block });

			repository.Verify();
			factory.Verify();
		}

		[Fact]
		public async Task ShouldCreateBlock()
		{
			var repository = new Mock<IRepository>(MockBehavior.Strict);
			repository.Setup(foo => foo.Save(It.IsAny<IBlock>()))
				.Returns(Task.CompletedTask).Verifiable();
			var factory = new Mock<IBlockFactory>(MockBehavior.Strict);
			factory.Setup(foo => foo.Create(It.IsAny<IEnumerable<ITransaction>>(), It.IsAny<string>()))
				.Returns(new TestBlock()).Verifiable();
			var mainer = new Mainer(repository.Object, factory.Object, 5, "MainerIdentity");
			var transaction = new TestTransaction();

			await mainer.CreateBlock(new[] { transaction });

			repository.Verify();
			factory.Verify();
		}

		[Fact]
		public async Task ShouldClearGenerations()
		{
			var repository = new Mock<IRepository>(MockBehavior.Strict);
			repository.Setup(foo => foo.Delete(It.IsAny<IEnumerable<IBlock>>())).Returns(Task.CompletedTask).Verifiable();
			var factory = new Mock<IBlockFactory>(MockBehavior.Strict);
			var mainer = new Mainer(repository.Object, factory.Object, 5, "MainerIdentity");
			var block1 = new TestBlock { MainerIdentity = "MainerIdentity", Position = 0};
			var block2 = new TestBlock { MainerIdentity = "MainerIdentity", Position = 1};

			await mainer.ClearGenerations(new[] { block1, block2 });

			repository.Verify();
			factory.Verify();
		}

		[Fact]
		public async Task ShouldStartNewElection()
		{
			var repository = new Mock<IRepository>(MockBehavior.Strict);
			repository.Setup(foo => foo.StartNewElection()).Returns(Task.CompletedTask).Verifiable();
			var factory = new Mock<IBlockFactory>(MockBehavior.Strict);
			var mainer = new Mainer(repository.Object, factory.Object, 5, "MainerIdentity");
			var block1 = new TestBlock { MainerIdentity = "MainerIdentity", Voters = { "MainerIdentity3" } };
			var block2 = new TestBlock { MainerIdentity = "MainerIdentity1", Voters = { "MainerIdentity4" } };
			var block3 = new TestBlock { MainerIdentity = "MainerIdentity2"};

			var result = await mainer.StartNewElection(new[] { block1, block2, block3 }, null);
			await mainer.StartNewElection(new[] { block1, block2, block3 }, result);

			repository.Verify();
			factory.Verify();
		}

		[Fact]
		public async Task ShouldCommitForgottenBlock()
		{
			var repository = new Mock<IRepository>(MockBehavior.Strict);
			repository.Setup(foo => foo.Commit(It.IsAny<IBlock>())).Returns(Task.CompletedTask).Verifiable();
			var factory = new Mock<IBlockFactory>(MockBehavior.Strict);
			var mainer = new Mainer(repository.Object, factory.Object, 5, "MainerIdentity");
			var block = new TestBlock { MainerIdentity = "MainerIdentity1", Voters = { "MainerIdentity", "MainerIdentity2" } };

			var result = await mainer.CommitForgottenBlock(new[] { block }, null);
			await mainer.CommitForgottenBlock(new[] { block }, result);

			repository.Verify();
			factory.Verify();
		}
	}
}
