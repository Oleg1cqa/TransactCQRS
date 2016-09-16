// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TransactCQRS.EventStore.BlockChain
{
	public class BlockTree
	{
		private readonly List<Mainer.IBlock> _source;

		public BlockTree(List<Mainer.IBlock> source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			_source = source;
		}

		public int GetRootCount()
		{
			var identities = _source.Select(block => block.Identity).ToArray();
			return _source.Count(item => !identities.Contains(item.ParentBlock));
		}

		public IEnumerable<Mainer.IBlock> GetBranchRoots()
		{
			var rootsKeys = _source.GroupBy(item => item.ParentBlock)
				.Where(item => item.Count() > 1)
				.Select(item => item.Key)
				.ToArray();
			return _source.Where(item => rootsKeys.Contains(item.Identity));
		}

		public IEnumerable<Mainer.IBlock> GetBranchEnds()
		{
			var parents = _source.Select(block => block.ParentBlock).ToArray();
			return _source.Where(item => !parents.Contains(item.Identity));
		}

		public IEnumerable<IEnumerable<Mainer.IBlock>> GetAllBranches()
		{
			var tree = _source.ToDictionary(item => item.Identity, item => item);
			foreach (var root in GetBranchRoots())
			{
				foreach (var end in GetBranchEnds())
				{
					var pos = end;
					var result = new List<Mainer.IBlock> { pos };
					while (tree.Keys.Contains(pos.ParentBlock) && pos != root)
					{
						pos = tree[pos.ParentBlock];
						result.Add(pos);
					}
					if (pos == root)
						yield return result;
				}
			}
		}
	}
}
