// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace TransactCQRS.EventStore
{
	public interface ITransactionReceiver
	{
		void Send<TTransaction>(TTransaction transaction) where TTransaction : AbstractTransaction;
	}
}
