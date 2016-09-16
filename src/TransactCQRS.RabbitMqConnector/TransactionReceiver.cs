// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace TransactCQRS.RabbitMqConnector
{
	public class TransactionReceiver
	{
		private readonly AbstractRepository _repository;
		private readonly IModel _model;
		private readonly string _consumerTag;

		public Action<AbstractTransaction> OnReceived { get; set; }

		public TransactionReceiver(AbstractRepository repository, IModel model, string queueName)
		{
			_repository = repository;
			_model = model;
			var consumer = new EventingBasicConsumer(_model);
			consumer.Received += Receive;
			_consumerTag = _model.BasicConsume(queueName, false, consumer);
		}

		private void Receive(object sender, BasicDeliverEventArgs arguments)
		{
			var identity = System.Text.Encoding.UTF8.GetString(arguments.Body);
			var transactionType = System.Text.Encoding.UTF8.GetString((byte[])arguments.BasicProperties.Headers["transactionType"]);
			OnReceived?.Invoke(GetTransaction(identity, transactionType));
			_model.BasicAck(arguments.DeliveryTag, false);
		}

		private AbstractTransaction GetTransaction(string identity, string transactionType)
		{
			return (AbstractTransaction)typeof(AbstractRepository).GetMethod(nameof(AbstractRepository.GetTransaction))
				.MakeGenericMethod(Type.GetType(transactionType))
				.Invoke(_repository, new object[] { identity });
		}

		public void Cancel()
		{
			_model.BasicCancel(_consumerTag);
		}
	}
}
