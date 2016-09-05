// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace TransactCQRS.EventStore
{
	/// <summary>
	/// Interface that allow finish transaction.
	/// </summary>
	public interface ITransactionTrailer
	{
		/// <summary>
		/// Commit transaction.
		/// </summary>
		void Commit<TTransaction>(TTransaction source) where TTransaction : AbstractTransaction;
		/// <summary>
		/// Fail transaction.
		/// </summary>
		void Fail<TTransaction>(TTransaction source) where TTransaction : AbstractTransaction;
	}
}
