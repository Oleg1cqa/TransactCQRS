// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace TransactCQRS.BlockChain
{
	/// <summary>
	/// Block factory interface.
	/// </summary>
	public interface IBlockFactory
	{
		/// <summary>
		/// Create new chain block.
		/// </summary>
		IBlock Create(IEnumerable<ITransaction> transactions, string mainerIdentity);
	}
}