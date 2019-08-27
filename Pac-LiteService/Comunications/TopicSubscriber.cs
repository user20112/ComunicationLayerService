using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;
using System;
using System.Threading.Tasks;

namespace SNPService.Comunications
{
    public delegate void MessageReceivedDelegate(string message);                                   //CallBack Delegate for whenever we receive a message

    public class TopicSubscriber : IDisposable
    {
        private readonly string topicName = null;                                                   //what topic we are subscribed to
        private readonly IConnectionFactory connectionFactory;                                      //conection factory for making the connection
        private readonly IConnection connection;                                                    //Connection for making the session
        private readonly ISession session;                                                          //session for making the consumer
        private readonly IMessageConsumer consumer;                                                 //Consumer for receiveing MEssages
        private bool isDisposed = false;                                                            //weather this has been disposed of yet

        public event MessageReceivedDelegate OnMessageReceived;                                     //callback delegate for whenever we receive a message

        public TopicSubscriber(string topicName, string brokerUri, string clientId, string consumerId)//set all of the variables
        {
            this.topicName = topicName;
            this.connectionFactory = new ConnectionFactory(brokerUri);
            this.connection = this.connectionFactory.CreateConnection();
            this.connection.ClientId = clientId;
            this.connection.Start();
            this.session = connection.CreateSession();
            ActiveMQTopic topic = new ActiveMQTopic(topicName);
            this.consumer = this.session.CreateDurableConsumer(topic, consumerId, "2 > 1", false);
            this.consumer.Listener += new MessageListener(OnMessage);
        }

        public void OnMessage(IMessage message)                                                     // whenever we get a message
        {
            try
            {
                ITextMessage textMessage = message as ITextMessage;                                     //convert message into ITextMessage
                if (this.OnMessageReceived != null)
                {
                    Task.Run(() => this.OnMessageReceived(textMessage.Text));                                           //fire the message to the MessageDelegate
                }
            }
            catch
            {
                try
                {
                    ActiveMQMessage textMessage = message as ActiveMQMessage;
                    byte[] data = textMessage.Content;
                    string Message = "";
                    for (int x = 0; x < 3; x++)
                    {
                        Message += (char)data[x];
                    }
                    Message += "     ";
                    for (int x = 3; x < data.Length; x++)
                    {
                        Message += (char)data[x];
                    }
                    if (this.OnMessageReceived != null && textMessage != null)
                    {
                        Task.Run(() => this.OnMessageReceived(Message));                                           //fire the message to the MessageDelegate
                    }
                }
                catch
                {
                }
            }
        }

        #region IDisposable Members

        public void Dispose()                                                                       //Dispose Everything
        {
            if (!this.isDisposed)
            {
                this.consumer.Dispose();
                this.session.Dispose();
                this.connection.Dispose();
                this.isDisposed = true;
            }
        }

        #endregion IDisposable Members
    }
}