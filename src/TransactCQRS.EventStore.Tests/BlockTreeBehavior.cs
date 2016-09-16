// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using TransactCQRS.EventStore.BlockChain;
using Xunit;

namespace TransactCQRS.EventStore.Tests
{
	public class BlockTreeBehavior
	{
		[Fact]
		public void ShouldFailExceptionWhenNullParameter()
		{
			Assert.Throws<ArgumentNullException>(() => new BlockTree(null));
		}

		[Fact]
		public void ShouldSupportEmptyList()
		{
			var tree = new BlockTree(new List<Mainer.IBlock>());

			Assert.Equal(0, tree.GetRootCount());
			Assert.Equal(0, tree.GetBranchEnds().Count());
			Assert.Equal(0, tree.GetBranchRoots().Count());
			Assert.Equal(0, tree.GetAllBranches().Count());
		}

		[Fact]
		public void ShouldCorrectGetEnds()
		{
			var source = new List<Mainer.IBlock>
			{
				new TestBlock {Identity = "1",ParentBlock = "0"},
				new TestBlock {Identity = "2",ParentBlock = "1"},
				new TestBlock {Identity = "3",ParentBlock = "1"},
				new TestBlock {Identity = "4",ParentBlock = "2"},
				new TestBlock {Identity = "5",ParentBlock = "2"},
				new TestBlock {Identity = "6",ParentBlock = "3"},
				new TestBlock {Identity = "7",ParentBlock = "5"},
			};
			var tree = new BlockTree(source);

			Assert.Equal(1, tree.GetRootCount());
			Assert.Equal(new[] { "1", "2" }, tree.GetBranchRoots().Select(item => item.Identity));
			Assert.Equal(new[] { "4", "6", "7" }, tree.GetBranchEnds().Select(item => item.Identity));
			Assert.Equal(new[] { "4; 2; 1", "6; 3; 1", "7; 5; 2; 1", "4; 2", "7; 5; 2" }, tree.GetAllBranches()
				.Select(item => item.Select(item1 => item1.Identity).Aggregate((acc, block) => $"{acc}; {block}")));
		}

		[Fact]
		public void ShouldFoundTwoRoots()
		{
			var source = new List<Mainer.IBlock>
			{
				new TestBlock {Identity = "2",ParentBlock = "1"},
				new TestBlock {Identity = "3",ParentBlock = "1"},
				new TestBlock {Identity = "4",ParentBlock = "2"},
				new TestBlock {Identity = "5",ParentBlock = "2"},
				new TestBlock {Identity = "6",ParentBlock = "3"},
				new TestBlock {Identity = "7",ParentBlock = "5"},
			};
			var tree = new BlockTree(source);

			Assert.Equal(2, tree.GetRootCount());
		}

		public class TestBlock : Mainer.IBlock
		{
			public DateTimeOffset CreateDate { get; set; }
			public string Identity { get; set; }
			public string MainerIdentity { get; set; }

			public string ParentBlock { get; set; }
			public int TransactionCount { get; set; }
			public bool ContainsTransaction(Mainer.ITransaction value)
			{
				throw new NotImplementedException();
			}
		}
	}
}
