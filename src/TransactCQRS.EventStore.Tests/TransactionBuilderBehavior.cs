// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Xunit;

namespace TransactCQRS.EventStore.Tests
{
	public class TransactionBuilderBehavior
	{
		[Fact]
		public void EntityTypeShouldBeDefined()
		{
			CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
			var repository = new MemoryRepository.Repository();
			var transaction = repository.StartTransaction<TestTransaction>("Started EntityTypeShouldBeDefined test.");

			var ex = Assert.Throws<InvalidOperationException>(() => transaction.GetEntity<NonPublicEvent>("sdfsd"));

			Assert.Equal("Unsupported type of entity.", ex.Message);
		}

		[Fact]
		public void EventMethodShouldBePublic()
		{
			CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
			var repository = new MemoryRepository.Repository();

			var ex = Assert.Throws<InvalidOperationException>(() => repository.StartTransaction<NonPublicEvent>("Started EventMethodShouldBePublic test."));

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
			CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
			var repository = new MemoryRepository.Repository();

			var ex = Assert.Throws<InvalidOperationException>(() => repository.StartTransaction<NonVirtualEvent>("Started EventMethodShouldBeVirtual test."));

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
			CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
			var repository = new MemoryRepository.Repository();

			var ex = Assert.Throws<InvalidOperationException>(() => repository.StartTransaction<NonVoidEvent>("Started EventMethodShouldReturnVoid test."));

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
			CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
			var repository = new MemoryRepository.Repository();

			var ex = Assert.Throws<InvalidOperationException>(() => repository.StartTransaction<VoidEvent>("Started EventMethodShouldHaveReturnType test."));

			Assert.Equal("Event method \"SetDescription\" should return class.", ex.Message);
		}

		public abstract class VoidEvent : AbstractTransaction
		{
			[Event]
			public abstract void SetDescription(string value);
		}
	}
}
