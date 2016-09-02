// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace TransactCQRS.EventStore.Tests
{
	public class MemoryRepositoryBehavior
	{
		[Fact]
		public void ShouldGetCommitedEntity()
		{
			var repository = new MemoryRepository.Repository();

			string identity;
			using (var transaction = repository.CreateTransaction<TestTransaction>("Started ShouldGetCommitEntity test."))
			{
				var entity = transaction.CreateTestEntity("TestName");
				entity.MakeOperation1(456);
				transaction.Commit();
				identity = transaction.GetIdentity(entity);
			}
			using (var transaction = repository.CreateTransaction<TestTransaction>("Started ShouldGetCommitEntity test part 2."))
			{
				var entity = transaction.GetEntity<TestEntity>(identity);
				Assert.Equal("TestName", entity.Name);
				Assert.Equal("AfterMakeOperation1", entity.State);
				Assert.Equal(456, entity.Testparametr);
			}
		}

		[Fact]
		public void ShouldSerializeDeserializeTranzaction()
		{
			var repository = new MemoryRepository.Repository();

			string identity;
			using (var transaction = repository.CreateTransaction<TestTransaction>("Started test transaction."))
			{
				var entity = transaction.CreateTestEntity("TestName");
				entity.MakeOperation1(456);
				transaction.SetCreator("Oleg");
				transaction.Commit();
				identity = transaction.GetIdentity(transaction);
			}
			using (var transaction = repository.GetTransaction<TestTransaction>(identity))
			{
				Assert.Equal("Started test transaction.", transaction.Description);
				Assert.Equal("Oleg", transaction.Creator);
			}
		}

		[Fact]
		public void EventMethodShouldBePublic()
		{
			var repository = new MemoryRepository.Repository();

			var ex = Assert.Throws<InvalidOperationException>(() => repository.CreateTransaction<NonPublicEvent>("Started EventMethodShouldBePublic test."));

			Assert.Equal("Event method \"SetDescription\" should be public.", ex.Message);
		}

		public abstract class NonPublicEvent : AbstractTransaction
		{
			[Event]
			internal virtual void SetDescription(string value) { }
		}

		[Fact]
		public void EventMethodShouldBeVirtual()
		{
			var repository = new MemoryRepository.Repository();

			var ex = Assert.Throws<InvalidOperationException>(() => repository.CreateTransaction<NonVirtualEvent>("Started EventMethodShouldBeVirtual test."));

			Assert.Equal("Event method \"SetDescription\" should be virtual.", ex.Message);
		}

		public abstract class NonVirtualEvent : AbstractTransaction
		{
			[Event]
			public void SetDescription(string value) { }
		}

		[Fact]
		public void EventMethodShouldReturnVoid()
		{
			var repository = new MemoryRepository.Repository();

			var ex = Assert.Throws<InvalidOperationException>(() => repository.CreateTransaction<NonVoidEvent>("Started EventMethodShouldReturnVoid test."));

			Assert.Equal("Event method \"SetDescription\" should return void.", ex.Message);
		}

		public abstract class NonVoidEvent : AbstractTransaction
		{
			[Event]
			public virtual string SetDescription(string value)
			{
				return null;
			}
		}

		[Fact]
		public void EventMethodShouldHaveReturnType()
		{
			var repository = new MemoryRepository.Repository();

			var ex = Assert.Throws<InvalidOperationException>(() => repository.CreateTransaction<VoidEvent>("Started EventMethodShouldHaveReturnType test."));

			Assert.Equal("Event method \"SetDescription\" should return class.", ex.Message);
		}

		public abstract class VoidEvent : AbstractTransaction
		{
			[Event]
			public abstract void SetDescription(string value);
		}
	}
}
