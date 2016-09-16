// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using TransactCQRS.BlockChain;

namespace TransactCQRS.Tests.BlockChain
{
	public class TestBlock : TestTransaction, IBlock
	{
		public string MainerIdentity { get; set; }
		public long Position { get; set; }
		public int Votes => Voters.Count;
		public IEnumerable<TestTransaction> Transactions { get; set; }
		public TestBlock PreviousBlock { get; set; }
		public List<string> Voters { get; } = new List<string>();

		public bool HaveVoteFrom(string mainerIdentity)
		{
			return Voters.Contains(mainerIdentity);
		}
	}
}