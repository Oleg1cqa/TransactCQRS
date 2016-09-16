// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace TransactCQRS.Tests
{
	public class AbstractTransactionBehavior
	{
		[Fact]
		public void ShouldThrowWhenTransactionSavedAgain()
		{
			var repository = new MemoryRepository.Repository();
			var transaction = repository.StartTransaction<TestTransaction>("Started test transaction.");
			var entity = transaction.CreateTestEntity("TestName");
			entity.MakeOperation1(456);
			transaction.SetCreator("Oleg");
			transaction.Commit();
			var identity = transaction.GetIdentity();

			Assert.Throws<InvalidOperationException>(() => transaction.Save());
			transaction = repository.GetTransaction<TestTransaction>(identity);
			Assert.Throws<InvalidOperationException>(() => transaction.Save());
		}
	}
}
