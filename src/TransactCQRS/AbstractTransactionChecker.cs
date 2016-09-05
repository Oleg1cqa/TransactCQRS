// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using TransactCQRS.EventStore;

namespace TransactCQRS
{
	public abstract class AbstractTransactionChecker : ITransactionReceiver
	{
		private readonly ITransactionTrailer _trailer;
		private readonly ITransactionReceiver _receiver;

		protected AbstractTransactionChecker(ITransactionTrailer trailer, ITransactionReceiver receiver = null)
		{
			_trailer = trailer;
			_receiver = receiver;
		}

		public abstract bool CheckTransaction<TTransaction>(TTransaction source);

		void ITransactionReceiver.Send<TTransaction>(TTransaction transaction)
		{
			if (CheckTransaction(transaction))
			{
				_trailer.Commit(transaction);
				_receiver?.Send(transaction);
			}
			else
				_trailer.Fail(transaction);
		}
	}
}
