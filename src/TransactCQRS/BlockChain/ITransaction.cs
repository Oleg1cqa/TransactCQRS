// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace TransactCQRS.BlockChain
{
	/// <summary>
	/// Transaction interface.
	/// </summary>
	public interface ITransaction
	{
		/// <summary>
		/// Identity of transaction.
		/// </summary>
		string Identity { get; }

		/// <summary>
		/// Transaction create date
		/// </summary>
		DateTimeOffset CreateDate { get; }
	}
}