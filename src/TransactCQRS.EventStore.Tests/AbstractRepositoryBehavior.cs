// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Cassandra;
using Xunit;

namespace TransactCQRS.EventStore.Tests
{
	public class AbstractRepositoryBehavior
	{
		public static IEnumerable<object[]> GetTestRepositories()
		{
			yield return new object[] { new MemoryRepository.Repository() };
			var session = Cluster.Builder()
				.AddContactPoints("127.0.0.1", "127.0.0.2", "127.0.0.3")
				.Build()
				.Connect();
			const string keyspace = "test_6";
			session.CreateKeyspaceIfNotExists(keyspace);
			session.ChangeKeyspace(keyspace);
			yield return new object[] { new CassandraRepository.Repository(session) };
		}

		[Theory]
		[MemberData(nameof(GetTestRepositories))]
		public void ShouldGetCommitedEntity(AbstractRepository repository)
		{
			string identity;
			using (var transaction = repository.StartTransaction<TestTransaction>("Started ShouldGetCommitEntity test."))
			{
				var entity = transaction.CreateTestEntity("TestName");
				entity.MakeOperation1(456);
				transaction.Commit();
				identity = entity.GetIdentity();
			}
			using (var transaction = repository.StartTransaction<TestTransaction>("Started ShouldGetCommitEntity test part 2."))
			{
				var entity = transaction.GetEntity<TestEntity>(identity);
				Assert.Equal("TestName", entity.Name);
				Assert.Equal("AfterMakeOperation1", entity.State);
				Assert.Equal(456, entity.Testparametr);
			}
		}

		[Theory]
		[MemberData(nameof(GetTestRepositories))]
		public void ShouldSerializeDeserializeTranzaction(AbstractRepository repository)
		{
			string identity;
			using (var transaction = repository.StartTransaction<TestTransaction>("Started test transaction."))
			{
				var entity = transaction.CreateTestEntity("TestName");
				entity.MakeOperation1(456);
				transaction.SetCreator("Oleg");
				transaction.Commit();
				identity = transaction.GetIdentity();
			}
			using (var transaction = repository.GetTransaction<TestTransaction>(identity))
			{
				Assert.Equal("Started test transaction.", transaction.Description);
				Assert.Equal("Oleg", transaction.Creator);
			}
		}

		[Theory]
		[MemberData(nameof(GetTestRepositories))]
		public void ShouldReferencedCorrectly(AbstractRepository repository)
		{
			string product1Id;
			string product2Id;
			string orderId;
			using (var transaction = repository.StartTransaction<OrderTransaction>("Create products."))
			{
				var product1 = transaction.CreateProduct("product1");
				var product2 = transaction.CreateProduct("product2");
				transaction.Commit();
				product1Id = product1.GetIdentity();
				product2Id = product2.GetIdentity();
			}
			using (var transaction = repository.StartTransaction<OrderTransaction>("Create order."))
			{
				var customer = transaction.CreateCustomer("Customer name");
				var order = transaction.CreateOrder(customer.GetReference());
				order.AddLine(order.CreateLine(transaction.GetEntity<Product>(product1Id).GetReference()));
				order.AddLine(order.CreateLine(transaction.GetEntity<Product>(product2Id).GetReference()));
				transaction.Commit();
				orderId = order.GetIdentity();
			}
			using (var transaction = repository.StartTransaction<OrderTransaction>("Create order."))
			{
				var order = transaction.GetEntity<Order>(orderId);
				var customer = order.Customer.Load();
				var product = order.Lines.First().Product.Load();

				Assert.Equal("product1", product.Name);
				Assert.Equal("Customer name", customer.Name);
			}
		}

		public abstract class OrderTransaction : AbstractTransaction
		{
			[Event]
			public abstract Order CreateOrder(IReference<Customer> customer);

			[Event]
			public abstract Product CreateProduct(string name);

			[Event]
			public abstract Customer CreateCustomer(string name);
		}

		public abstract class Order
		{
			private readonly List<Line> _lines = new List<Line>();
			// ReSharper disable once MemberHidesStaticFromOuterClass
			public IReference<Customer> Customer { get; }
			public IEnumerable<Line> Lines => _lines;

			protected Order(IReference<Customer> customer)
			{
				Customer = customer;
			}

			[Event]
			public virtual void AddLine(Line source)
			{
				_lines.Add(source);
			}

			[Event]
			public virtual void DeleteLine(Line source)
			{
				_lines.Remove(source);
			}

			[Event]
			public abstract Line CreateLine(IReference<Product> product);

			public class Line
			{
				// ReSharper disable once MemberHidesStaticFromOuterClass
				public IReference<Product> Product { get; }

				protected Line(IReference<Product> product)
				{
					Product = product;
				}
			}
		}

		public class Product
		{
			public string Name { get; }

			protected Product(string name)
			{
				Name = name;
			}
		}

		public class Customer
		{
			public string Name { get; }

			protected Customer(string name)
			{
				Name = name;
			}
		}
	}
}
