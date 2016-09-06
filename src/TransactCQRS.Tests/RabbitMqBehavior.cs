// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using Cassandra;
using RabbitMQ.Client;
using TransactCQRS.RabbitMqConnector;
using Xunit;
using TransactCQRS.EventStore;

namespace TransactCQRS.Tests
{
	public class RabbitMqBehavior
	{
		private static IModel CreateQueue(string exchangeName, string queueName, string routingKey, out IConnection connection)
		{
			connection = new ConnectionFactory { HostName = "localhost" }.CreateConnection();
			var result = connection.CreateModel();
			result.ExchangeDeclare(exchangeName, ExchangeType.Direct);
			result.QueueDeclare(queueName, false, false, false, null);
			result.QueueBind(queueName, exchangeName, routingKey, null);
			return result;
		}

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
		public void ShouldCorrectGetCommitedTransaction(AbstractRepository repository)
		{
			const string exchangeName = "Test exchange";
			const string queueName = "Test queue Name";
			const string routingKey = "routingKey";
			IConnection connection;
			using (var queue = CreateQueue(exchangeName, queueName, routingKey, out connection))
			{
				var finished = false;
				var receiver = new TransactionReceiver(repository, queue, queueName)
				{
					OnReceived = item =>
					{
						finished = true;
						item.Commit();
					}
				};
				repository.OnTransactionSaved = new TransactionSender(queue, exchangeName, routingKey).Send;
				string transactionId;
				using (var transaction = repository.StartTransaction<OrderTransaction>("Started ShouldReadTransaction test."))
				{
					transaction.CreateCustomer("TestName");
					transaction.Save();
					transactionId = transaction.GetIdentity();
				}
				SpinWait.SpinUntil(() => finished, 5000);

				Assert.True(finished);
				Assert.NotNull(repository.GetTransaction<OrderTransaction>(transactionId));

				receiver.Cancel();
				queue.Close(200, "Goodbye");
				connection.Close();
			}
		}

		public abstract class OrderTransaction : AbstractTransaction
		{
			public bool IsCommited { get; internal set; }

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
