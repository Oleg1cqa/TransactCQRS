// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace TransactCQRS.BlockChain
{
	/// <summary>
	/// Repository interface.
	/// </summary>
	public interface IRepository
	{
		/// <summary>
		/// Get all uncommitted transactions.
		/// </summary>
		Task<IEnumerable<ITransaction>> GetUncommittedTransactions();

		/// <summary>
		/// Save new block.
		/// </summary>
		Task Save(IBlock value);

		/// <summary>
		/// Commit block and all transaction from block.
		/// </summary>
		Task Commit(IBlock value);

		/// <summary>
		/// Vote for chain block.
		/// </summary>
		Task Vote(IBlock block, string voter);

		/// <summary>
		/// Delete uncommitted block.
		/// </summary>
		Task Delete(IEnumerable<IBlock> value);

		/// <summary>
		/// Clear all votes.
		/// </summary>
		Task StartNewElection();
	}
}