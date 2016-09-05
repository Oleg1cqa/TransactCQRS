// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Cassandra;
using TransactCQRS.EventStore;
using Xunit;

namespace TransactCQRS.Tests
{
	public class AbstractTransactionCheckerBehavior
	{
		public static IEnumerable<object[]> GetTestRepositories()
		{
			yield return new object[] { new EventStore.MemoryRepository.Repository() };
			var session = Cluster.Builder()
				.AddContactPoints("127.0.0.1", "127.0.0.2", "127.0.0.3")
				.Build()
				.Connect();
			const string keyspace = "test_6";
			session.CreateKeyspaceIfNotExists(keyspace);
			session.ChangeKeyspace(keyspace);
			yield return new object[] { new EventStore.CassandraRepository.Repository(session) };
		}

		[Theory]
		[MemberData(nameof(GetTestRepositories))]
		public void ShouldReadCommittedTransaction(AbstractRepository repository)
		{
			repository.Queue = new TransactionChecker(repository, true);
			string transactionId;
			using (var transaction = repository.StartTransaction<OrderTransaction>("Started ShouldReadTransaction test."))
			{
				transaction.CreateCustomer("TestName");
				transaction.Commit();
				transactionId = transaction.GetIdentity();
			}
			Assert.NotNull(repository.GetTransaction<OrderTransaction>(transactionId));
		}

		[Theory]
		[MemberData(nameof(GetTestRepositories))]
		public void ShouldFailOnReadFailedTransaction(AbstractRepository repository)
		{
			repository.Queue = new TransactionChecker(repository, false);
			string transactionId;
			using (var transaction = repository.StartTransaction<OrderTransaction>("Started ShouldReadTransaction test."))
			{
				transaction.CreateCustomer("TestName");
				transaction.Commit();
				transactionId = transaction.GetIdentity();
			}
			Assert.Throws<ArgumentOutOfRangeException>(() => repository.GetTransaction<OrderTransaction>(transactionId));
		}

		public class TransactionChecker : AbstractTransactionChecker
		{
			private readonly bool _result;

			public TransactionChecker(ITransactionTrailer trailer, bool result) : base(trailer)
			{
				_result = result;
			}

			public override bool CheckTransaction<TTransaction>(TTransaction source)
			{
				return _result;
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
