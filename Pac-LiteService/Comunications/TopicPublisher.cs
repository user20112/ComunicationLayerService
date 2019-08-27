using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;
using System;

namespace SNPService.Comunications
{
    public class TopicPublisher : IDisposable
    {
        private readonly string topicName = null;                               //topic name we are going to publish to
        private readonly IConnectionFactory connectionFactory;                  //Connection Factory used to create the connection
        private readonly IConnection connection;                                //the connection we receive data through
        private readonly ISession session;                                      //the session we use from the connection
        private readonly IMessageProducer producer;                             //the producer that receives the messages from the topic and session
        private bool isDisposed = false;

        public TopicPublisher(string topicName, string brokerUri)               //set all the variables
        {
            this.topicName = topicName;
            this.connectionFactory = new ConnectionFactory(brokerUri);
            this.connection = this.connectionFactory.CreateConnection();
            this.connection.Start();
            this.session = connection.CreateSession();
            ActiveMQTopic topic = new ActiveMQTopic(topicName);
            this.producer = this.session.CreateProducer(topic);
        }

        public void SendMessage(string message)                                 //send a message to the topic
        {
            if (!this.isDisposed)
            {
                ITextMessage textMessage = this.session.CreateTextMessage(message);
                this.producer.Send(textMessage);                                //send the message
            }
            else
            {
                throw new ObjectDisposedException(this.GetType().FullName);     //if it breaks throw an exception
            }
        }

        #region IDisposable Members

        public void Dispose()                                                   //dispose of everything.
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