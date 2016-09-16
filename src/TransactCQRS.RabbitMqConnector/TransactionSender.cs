// Copyright (c) Starodub Oleg. All Rights Reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using RabbitMQ.Client;

namespace TransactCQRS.RabbitMqConnector
{
	public class TransactionSender
	{
		private readonly IModel _model;
		private readonly string _exchangeName;
		private readonly string _routingKey;

		public TransactionSender(IModel model, string exchangeName, string routingKey)
		{
			_model = model;
			_exchangeName = exchangeName;
			_routingKey = routingKey;
		}

		public void Send(AbstractTransaction source)
		{
			var messageBody = System.Text.Encoding.UTF8.GetBytes(source.GetIdentity());
			var type = System.Text.Encoding.UTF8.GetBytes(source.BaseType.AssemblyQualifiedName);
			var props = _model.CreateBasicProperties();
			props.ContentType = "text/plain";
			props.DeliveryMode = 2;
			props.Headers = new Dictionary<string, object> {{"transactionType", type}};
			_model.BasicPublish(_exchangeName, _routingKey, props, messageBody);
		}
	}
}
