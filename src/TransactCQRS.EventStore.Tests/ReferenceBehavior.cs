// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TransactCQRS.EventStore.Tests
{
	public class ReferenceBehavior
	{
		[Fact]
		public void ShouldReferencedCorrectly()
		{
			var repository = new MemoryRepository.Repository();

			string product1Id;
			string product2Id;
			string orderId;
			using (var transaction = repository.CreateTransaction<OrderTransaction>("Create products."))
			{
				var product1 = transaction.CreateProduct("product1");
				var product2 = transaction.CreateProduct("product2");
				transaction.Commit();
				product1Id = product1.GetIdentity();
				product2Id = product2.GetIdentity();
			}
			using (var transaction = repository.CreateTransaction<OrderTransaction>("Create order."))
			{
				var customer = transaction.CreateCustomer("Customer name");
				var order = transaction.CreateOrder(customer.GetReference());
				order.AddLine(order.CreateLine(transaction.GetEntity<Product>(product1Id).GetReference()));
				order.AddLine(order.CreateLine(transaction.GetEntity<Product>(product2Id).GetReference()));
				transaction.Commit();
				orderId = order.GetIdentity();
			}
			using (var transaction = repository.CreateTransaction<OrderTransaction>("Create order."))
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
