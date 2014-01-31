﻿using System;
using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using ServiceStack.Logging;
using ServiceStack.Messaging;

namespace ServiceStack.RabbitMq
{
    public class RabbitMqProducer : IMessageProducer
    {
        public static ILog Log = LogManager.GetLogger(typeof(RabbitMqProducer));
        protected readonly RabbitMqMessageFactory msgFactory;
        public int RetryCount { get; set; }
        public Action OnPublishedCallback { get; set; }

        private IConnection connection;
        public IConnection Connection
        {
            get
            {
                if (connection == null)
                {
                    connection = msgFactory.ConnectionFactory.CreateConnection();
                }
                return connection;
            }
        }

        private IModel channel;
        public IModel Channel
        {
            get
            {
                if (channel == null || !channel.IsOpen)
                {
                    channel = Connection.OpenChannel();
                    //http://www.rabbitmq.com/blog/2012/04/25/rabbitmq-performance-measurements-part-2/
                    //http://www.rabbitmq.com/amqp-0-9-1-reference.html
                    channel.BasicQos(prefetchCount: 20, prefetchSize: 0, global: false);
                }
                return channel;
            }
        }

        public RabbitMqProducer(RabbitMqMessageFactory msgFactory)
        {
            this.msgFactory = msgFactory;
        }

        public void Publish<T>(T messageBody)
        {
            var message = messageBody as IMessage;
            if (message != null)
            {
                Publish(message.ToInQueueName(), message);
            }
            else
            {
                Publish(new Message<T>(messageBody));
            }
        }
 
        public void Publish<T>(IMessage<T> message)
        {
            Publish(message.ToInQueueName(), message);
        }

        public void Publish(string queueName, IMessage message)
        {
            Publish(queueName, message, QueueNames.Exchange);
        }

        public void Publish(string queueName, IMessage message, string exchange)
        {
            var props = Channel.CreateBasicProperties();
            props.SetPersistent(true);
            props.PopulateFromMessage(message);

            var messageBytes = message.Body.ToJson().ToUtf8Bytes();

            PublishMessage(exchange ?? QueueNames.Exchange,
                routingKey: queueName,
                basicProperties: props, body: messageBytes);

            if (OnPublishedCallback != null)
            {
                OnPublishedCallback();
            }
        }

        static HashSet<string> Queues = new HashSet<string>();

        public void PublishMessage(string exchange, string routingKey, IBasicProperties basicProperties, byte[] body)
        {
            try
            {
                if (!Queues.Contains(routingKey))
                {
                    Channel.RegisterQueueByName(routingKey);
                    Queues = new HashSet<string>(Queues) { routingKey };
                }

                Channel.BasicPublish(exchange, routingKey, basicProperties, body);
            }
            catch (OperationInterruptedException ex)
            {
                if (ex.Is404())
                {
                    Channel.RegisterExchangeByName(exchange);

                    Channel.BasicPublish(exchange, routingKey, basicProperties, body);
                }
                throw;
            }
        }

        public BasicGetResult GetMessage(string queueName, bool noAck)
        {
            try
            {
                if (!Queues.Contains(queueName))
                {
                    Channel.RegisterQueueByName(queueName);
                    Queues = new HashSet<string>(Queues) { queueName };
                }

                return Channel.BasicGet(queueName, noAck: noAck);
            }
            catch (OperationInterruptedException ex)
            {
                if (ex.Is404())
                {
                    Channel.RegisterQueueByName(queueName);

                    return Channel.BasicGet(queueName, noAck: noAck);
                }
                throw;
            }
        }

        public virtual void Dispose()
        {
            if (channel != null)
            {
                try
                {
                    channel.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error("Error trying to dispose RabbitMqProducer model", ex);
                } 
                channel = null;
            }
            if (connection != null)
            {
                try
                {
                    connection.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error("Error trying to dispose RabbitMqProducer connection", ex);
                }
                connection = null;
            }
        }
    }
}