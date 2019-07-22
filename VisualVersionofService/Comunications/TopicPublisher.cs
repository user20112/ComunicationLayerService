﻿using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;
using System;

namespace VisualVersionofService
{
    public class TopicPublisher : IDisposable
    {
        private readonly string topicName = null;
        private readonly IConnectionFactory connectionFactory;
        private readonly IConnection connection;
        private readonly ISession session;
        private readonly IMessageProducer producer;
        private bool isDisposed = false;

        public TopicPublisher(string topicName, string brokerUri)
        {
            this.topicName = topicName;
            this.connectionFactory = new ConnectionFactory(brokerUri);
            this.connection = this.connectionFactory.CreateConnection();
            this.connection.Start();
            this.session = connection.CreateSession();
            ActiveMQTopic topic = new ActiveMQTopic(topicName);
            this.producer = this.session.CreateProducer(topic);
        }

        public void SendMessage(string message)
        {
            if (!this.isDisposed)
            {
                ITextMessage textMessage = this.session.CreateTextMessage(message);
                this.producer.Send(textMessage);
            }
            else
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.producer.Dispose();
                this.session.Dispose();
                this.connection.Dispose();
                this.isDisposed = true;
            }
        }

        #endregion IDisposable Members
    }
}