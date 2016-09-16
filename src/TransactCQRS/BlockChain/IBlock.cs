// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace TransactCQRS.BlockChain
{
	/// <summary>
	/// Block from chain
	/// </summary>
	public interface IBlock : ITransaction
	{
		/// <summary>
		/// Mainer identity.
		/// </summary>
		string MainerIdentity { get; }

		/// <summary>
		/// Position in block chain.
		/// </summary>
		long Position { get; }

		/// <summary>
		/// Votes for this block.
		/// </summary>
		int Votes { get; }

		/// <summary>
		/// Already vote for this block.
		/// </summary>
		bool HaveVoteFrom(string mainerIdentity);
	}
}