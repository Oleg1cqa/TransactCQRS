// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using TransactCQRS.BlockChain;

namespace TransactCQRS
{
	public abstract class ChainBlock : AbstractTransaction, IBlock
	{
		public IReference<ChainBlock> Previous { get; }
		public string CheckSum { get; private set; }

		protected ChainBlock(IReference<ChainBlock> previous)
		{
			Previous = previous;
		}

		[Event]
		public void AddTransaction(AbstractTransaction value)
		{
			
		}

		[Event]
		public void TransactionFailed(AbstractTransaction value)
		{

		}

		[Event]
		public void SetCheckSum(string value)
		{
			CheckSum = value;
		}

		public void BeforeSave()
		{
			SetCheckSum(GenerateCheckSum());
		}

		private string GenerateCheckSum()
		{
			throw new System.NotImplementedException();
		}

		public string MainerIdentity { get; }
		public long Position { get; }
		public int Votes { get; }
		public bool HaveVoteFrom(string mainerIdentity)
		{
			throw new System.NotImplementedException();
		}
	}
}
