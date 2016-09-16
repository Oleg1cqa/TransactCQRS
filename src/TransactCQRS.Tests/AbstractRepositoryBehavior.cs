// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cassandra;
using Xunit;

namespace TransactCQRS.Tests
{
	public class AbstractRepositoryBehavior
	{
		private static readonly Cluster Cluster;

		static AbstractRepositoryBehavior()
		{
			Cluster = Cluster.Builder()
				.AddContactPoints("127.0.0.1", "127.0.0.2", "127.0.0.3")
				.Build();
		}

		public static IEnumerable<object[]> GetTestRepositories()
		{
			yield return new object[] {new MemoryRepository.Repository()};
			var result = Cluster.Connect();
			const string keyspace = "test_15";
			result.CreateKeyspaceIfNotExists(keyspace);
			result.ChangeKeyspace(keyspace);
			yield return new object[] {new EventStore.CassandraRepository.Repository(result)};
		}

		[Theory]
		[MemberData(nameof(GetTestRepositories))]
		public void ShouldReadEntitiesManyTimes(AbstractRepository repository)
		{
			var transaction = repository.StartTransaction<TestTransaction>("Started ShouldGetCommitEntity test.");
			var entity = transaction.CreateTestEntity("TestName");
			entity.MakeOperation1(456);
			entity.MakeOperation2(456);
			transaction.Commit();
			var identity = entity.GetIdentity();
			Parallel.For(0, 10, id =>
			{
				for (var i = 0; i < 500; i++)
				{
					var newTransaction = repository.StartTransaction<TestTransaction>("Started ShouldGetCommitEntity test part 2.");
					var result = newTransaction.GetEntity<TestEntity>(identity);
					Assert.NotNull(result);
					Assert.Equal("TestName", result.Name);
					Assert.Equal("AfterMakeOperation2", result.State);
					Assert.Equal(456, result.Testparametr);
				}
			});
		}

		[Theory]
		[MemberData(nameof(GetTestRepositories))]
		public void ShouldWriteEntitiesManyTimes(AbstractRepository repository)
		{
			var cancelation = new CancellationTokenSource();
			var cancelToken = cancelation.Token;
			Task.Run(() =>
			{
				while (!cancelToken.IsCancellationRequested)
				{
					try
					{
						repository.GetWaitingTransactions()
							.ToList()
							.ForEach(item => item.Load().Commit());
					}
					catch (Exception)
					{
						// Ignore errors
					}
				}
				cancelToken.ThrowIfCancellationRequested();
			}, cancelToken);
			try
			{
				Parallel.For(0, 10, id =>
				{
					for (var i = 0; i < 500; i++)
					{
						var transaction = repository.StartTransaction<TestTransaction>("Started ShouldGetCommitEntity test.");
						var entity = transaction.CreateTestEntity("TestName");
						entity.MakeOperation1(456);
						entity.MakeOperation2(456);
						transaction.Save();
					}
				});
			}
			finally
			{
				cancelation.Cancel();
			}
		}

		[Theory]
		[MemberData(nameof(GetTestRepositories))]
		public void ShouldGetWaitedTransactionsManyTimes(AbstractRepository repository)
		{
			var cancelation = new CancellationTokenSource();
			var cancelToken = cancelation.Token;
			Task.Run(() =>
			{
				while (!cancelToken.IsCancellationRequested)
				{
					try
					{
						repository.GetWaitingTransactions()
							.ToList()
							.ForEach(item => item.Load().Commit());
					}
					catch
					{
						//Ignore errors.
					}
				}
				cancelToken.ThrowIfCancellationRequested();
			}, cancelToken);
			try
			{
				Parallel.For(0, 10, id =>
				{
					for (var i = 0; i < 50; i++)
					{
						var transaction = repository.StartTransaction<TestTransaction>("Started ShouldGetCommitEntity test.");
						var entity = transaction.CreateTestEntity("TestName");
						entity.MakeOperation1(456);
						transaction.Save();
						var identity = entity.GetIdentity();
						var newTransaction = repository.StartTransaction<TestTransaction>("Started ShouldGetCommitEntity test part 2.");
						TestEntity result = null;
						// ReSharper disable once AccessToDisposedClosure
						SpinWait.SpinUntil(() => newTransaction.TryGetEntity(identity, out result), TimeSpan.FromSeconds(5));
						Assert.NotNull(result);
						Assert.Equal("TestName", result.Name);
						Assert.Equal("AfterMakeOperation1", result.State);
						Assert.Equal(456, result.Testparametr);
					}
				});
			}
			finally
			{
				cancelation.Cancel();
			}
		}

		[Theory]
		[MemberData(nameof(GetTestRepositories))]
		public void ShouldReadCommittedTransaction(AbstractRepository repository)
		{
			repository.OnTransactionSaved = (item) => item.Commit();
			var transaction = repository.StartTransaction<OrderTransaction>("Started ShouldReadTransaction test.");
			transaction.CreateCustomer("TestName");
			transaction.Save();
			var transactionId = transaction.GetIdentity();
			Assert.NotNull(repository.GetTransaction<OrderTransaction>(transactionId));
		}

		[Theory]
		[MemberData(nameof(GetTestRepositories))]
		public void ShouldFailOnReadFailedTransaction(AbstractRepository repository)
		{
			repository.OnTransactionSaved = (item) => item.Rollback();
			var transaction = repository.StartTransaction<OrderTransaction>("Started ShouldReadTransaction test.");
			transaction.CreateCustomer("TestName");
			transaction.Save();
			var transactionId = transaction.GetIdentity();
			Assert.Throws<ArgumentOutOfRangeException>(() => repository.GetTransaction<OrderTransaction>(transactionId));
		}

		[Theory]
		[MemberData(nameof(GetTestRepositories))]
		public void ShouldGetCommitedEntity(AbstractRepository repository)
		{
			var transaction = repository.StartTransaction<TestTransaction>("Started ShouldGetCommitEntity test.");
			var entity = transaction.CreateTestEntity("TestName");
			entity.MakeOperation1(456);
			transaction.Commit();
			var identity = entity.GetIdentity();
			var newTransaction = repository.StartTransaction<TestTransaction>("Started ShouldGetCommitEntity test part 2.");
			var result = newTransaction.GetEntity<TestEntity>(identity);

			Assert.Equal("TestName", result.Name);
			Assert.Equal("AfterMakeOperation1", result.State);
			Assert.Equal(456, result.Testparametr);
		}

		[Theory]
		[MemberData(nameof(GetTestRepositories))]
		public void ShouldSerializeDeserializeTranzaction(AbstractRepository repository)
		{
			var transaction = repository.StartTransaction<TestTransaction>("Started test transaction.");
			var entity = transaction.CreateTestEntity("TestName");
			entity.MakeOperation1(456);
			transaction.SetCreator("Oleg");
			transaction.Commit();
			var identity = transaction.GetIdentity();
			var result = repository.GetTransaction<TestTransaction>(identity);

			Assert.Equal("Started test transaction.", result.Description);
			Assert.Equal("Oleg", result.Creator);
		}

		[Theory]
		[MemberData(nameof(GetTestRepositories))]
		public void ShouldReferencedCorrectly(AbstractRepository repository)
		{
			var transaction = repository.StartTransaction<OrderTransaction>("Create products.");
			var product1 = transaction.CreateProduct("product1");
			var product2 = transaction.CreateProduct("product2");
			transaction.Commit();
			var product1Id = product1.GetIdentity();
			var product2Id = product2.GetIdentity();
			transaction = repository.StartTransaction<OrderTransaction>("Create order.");
			var customer = transaction.CreateCustomer("Customer name");
			var order = transaction.CreateOrder(customer.GetReference());
			order.AddLine(order.CreateLine(transaction.GetEntity<Product>(product1Id).GetReference()));
			order.AddLine(order.CreateLine(transaction.GetEntity<Product>(product2Id).GetReference()));
			transaction.Commit();
			var orderId = order.GetIdentity();
			transaction = repository.StartTransaction<OrderTransaction>("Create order.");
			order = transaction.GetEntity<Order>(orderId);
			customer = order.Customer.Load();
			var product = order.Lines.First().Product.Load();

			Assert.Equal("product1", product.Name);
			Assert.Equal("Customer name", customer.Name);
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
