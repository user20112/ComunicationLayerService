using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Data.SqlClient;
using Newtonsoft.Json.Linq;
using System.Net.Sockets;

namespace Pac_LiteService
{
    public partial class Service1 : ServiceBase
    {
        const string SubTopicName = "Pac-Lite.Inbound";
        const string TestTopicName = "Pac-Lite.Outbound";
        const string Broker = "tcp://10.197.10.32:61616";
        const string ClientID = "Pac-LiteService";
        const string ConsumerID = "Pac-LiteService";
        const string CamstarUsername = "UserName";      // update me
        const string CamstarPassword = "Password";      // update me
        const string ENG_DBDataSource = "localhost";    // update me
        const string ENG_DBUserID = "sa";               // update me
        const string ENG_DBPassword = "password";       // update me
        const string ENG_DBInitialCatalog = "master";   // update me
        public TopicPublisher TestPublisher;
        private delegate void SetTextCallback(string text);
        private TopicSubscriber Subscriber;
        private SqlConnection ENGDBConnection;
        NetworkStream stream;
        TcpClient client;
        Int32 TCPport = 1300;// update me
        string TCPserver = ""; // update me 
        public Service1()
        {
            InitializeComponent();
            Console.WriteLine("Initialized!");
        }
        /// <summary>
        ///  Called on service start
        /// </summary>
        protected override void OnStart(string[] args)
        {
            try
            {
                Console.WriteLine("Connecting MainSubscriber");
                Subscriber = new TopicSubscriber(SubTopicName, Broker, ClientID, ConsumerID);
                Subscriber.OnMessageReceived += new MessageReceivedDelegate(subscriber_OnMessageReceived);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            try
            {
                Console.WriteLine("Connecting TestPublisher");
                TestPublisher = new TopicPublisher(TestTopicName, Broker);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            try
            {
                // Build connection string
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                builder.DataSource = ENG_DBDataSource;
                builder.UserID = ENG_DBUserID;
                builder.Password = ENG_DBPassword;
                builder.InitialCatalog = ENG_DBInitialCatalog;
                // Connect to SQL
                Console.Write("Connecting to SQL Server ... ");
                ENGDBConnection = new SqlConnection(builder.ConnectionString);
                ENGDBConnection.Open();
            }
            catch (SqlException ex)
            {
                Console.WriteLine(ex);
            }
            Console.WriteLine("Connecting TCP to Camstar");
            try
            {
                TcpClient client = new TcpClient(TCPserver, TCPport);//connect
                stream = client.GetStream();//get stream to read and write to.
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        /// <summary>
        ///  Called on service stop
        /// </summary>
        protected override void OnStop()
        {
            try
            {
                TestPublisher.Dispose();//dispose of connections on stop they will be reestablished on start.
                Subscriber.Dispose();
                ENGDBConnection.Dispose();
                stream.Close();
                client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        /// <summary>
        ///  called whenever a mqtt message from Pac-Lite is received
        /// </summary>
        void subscriber_OnMessageReceived(string Message)
        {
            Console.WriteLine(Message);//log message and bits when it comes in.
            Console.WriteLine("Packet Header =" + Convert.ToInt32(Message[0]).ToString());
            Console.WriteLine("Packet Type=" + Convert.ToInt32(Message[1]).ToString());
            Console.WriteLine("Pac-LiteID=" + Convert.ToInt32(Message[2]).ToString());
            switch (Convert.ToInt32(Message[0]))//switch packet header
            {
                case 1://this means its a Pac-Lite message

                    switch (Convert.ToInt32(Message[1]))//switch Packet Type
                    {
                        case 252:
                            Task.Run(() => CamstarTestPacket(Message));//run it in the background dont await as we dont need the return values.
                            Console.WriteLine("CamstarTestPacket Received!");
                            break;
                        case 253:
                            Task.Run(() => ActiveMQTestPacket(Message));//run it in the background dont await as we dont need the return values.
                            Console.WriteLine("ActiveMQTestPacketReceived!");
                            break;
                        case 254:
                            Task.Run(() => SQLTestPacket(Message));//run it in the background dont await as we dont need the return values.
                            Console.WriteLine("SQLTestPacketReceived!");
                            break;
                    }
                    break;
            }
        }
        /// <summary>
        ///  Test Packet Heading to Camstar with XML
        /// </summary>
        private void CamstarTestPacket(string Message)//camstar xml is the messiest packet due to the XML they are expecting
        {                                              // i consider you warned below is a thruput packet it is going to be mostly hardcoded for these packets.
            string JsonString = Message.Substring(7, 246);//grab json data from the end.
            JObject ReceivedPacket = JsonConvert.DeserializeObject(JsonString) as JObject;
            StringBuilder PacketStringBuilder = new StringBuilder();
            PacketStringBuilder.Append("<__InSite __version=\"1.1\" __encryption=\"2\"><__session><__connect><user><__name>");
            PacketStringBuilder.Append(CamstarUsername);//username
            PacketStringBuilder.Append("</__name></user><password __encrypted=\"no\">");
            PacketStringBuilder.Append(CamstarPassword);//password
            PacketStringBuilder.Append("</password></__connect><__filter><__allowUntaggedInstances><![CDATA[3]]></__allowUntaggedInstances></__filter></__session><__service __serviceType=\"ResourceThruput\"><__utcOffset><![CDATA[-04:00:00]]></__utcOffset><__inputData><MfgOrder><__name><![CDATA[]]></__name></MfgOrder><Product><__name><![CDATA[");
            PacketStringBuilder.Append(ReceivedPacket);//productNaed
            PacketStringBuilder.Append("]]></__name><__useROR><![CDATA[true]]></__useROR></Product><Qty><![CDATA[");
            PacketStringBuilder.Append(ReceivedPacket["Good"]);//qty
            PacketStringBuilder.Append("]]></Qty><Resource><__name><![CDATA[");
            PacketStringBuilder.Append(ReceivedPacket["Machine"]);//resource
            PacketStringBuilder.Append("]]></__name></Resource><ResourceGroup><__name><![CDATA[");
            PacketStringBuilder.Append(ReceivedPacket["Line"]);//resourceGroup
            PacketStringBuilder.Append("]]></__name></ResourceGroup><UOM><__name><![CDATA[");
            PacketStringBuilder.Append(ReceivedPacket["UOM"]);//UOM
            PacketStringBuilder.Append("]]></__name>/</UOM></__inputData><__perform><__eventName><![CDATA[GetWIPMsgs]]></__eventName></__perform><__requestData><CompletionMsg /><WIPMsgMgr><WIPMsgs><AcknowledgementRequired /><MsgAcknowledged /><MsgText /><PasswordRequired /><WIPMsgDetails /></WIPMsgs></WIPMsgMgr></__requestData></__service></__InSite>");
        }
        /// <summary>
        ///  Test Packet Heading to ActiveMQ with MQTT
        /// </summary>
        private void ActiveMQTestPacket(string Message)
        {
            string JsonString = Message.Substring(7, 246);//grab json data from the end.
            JObject ReceivedPacket = JsonConvert.DeserializeObject(JsonString) as JObject;
            //do whatever to the data before sending it on
            TestPublisher.SendMessage(JsonConvert.SerializeObject(ReceivedPacket));//convert back to json for other aplications ( or whatever format)
        }
        /// <summary>
        ///  Test Packet Heading to ENGDB with SQL
        /// </summary>
        private void SQLTestPacket(string Message)
        {
            string JsonString = Message.Substring(7, 246);//grab json data from the end.
            JObject ReceivedPacket = JsonConvert.DeserializeObject(JsonString) as JObject;
            StringBuilder SQLStringBuilder = new StringBuilder(); 
            SQLStringBuilder.Append("USE " + ReceivedPacket["Line"] + "; ");//string builder append just appends data to the end of a string
            SQLStringBuilder.Append("INSERT " + ReceivedPacket["Machine"] + "(");
            IList<string> keys = ReceivedPacket.Properties().Select(p => p.Name).ToList();//gets list of all keys in json object
            string KeySection = "";
            string ValueSection = "";
            foreach (string key in keys)//foreach key 
            {
                if (key != "Machine" && key != "Line")//except machine and line as they are the DB and Table respectivly
                {
                    KeySection += key + ", ";//Make a key
                    ValueSection += "@" + key + ", ";//and value Reference to be replaced later
                }
            }
            KeySection = KeySection.Substring(0, KeySection.Length - 2);
            ValueSection = ValueSection.Substring(0, ValueSection.Length - 2);//remove the extra ", " that gets appended
            SQLStringBuilder.Append(KeySection + ")");
            SQLStringBuilder.Append("VALUES (" + ValueSection + ");");//append both to the command string
            string SQLString = SQLStringBuilder.ToString();//convert to string
            using (SqlCommand command = new SqlCommand(SQLString, ENGDBConnection))
            {
                foreach (string key in keys)//foreach key
                {
                    if (key != "Machine" && key != "Line") //again not machine and line
                    {
                        command.Parameters.AddWithValue("@" + key, ReceivedPacket[key]);//replace each value reference with value sanitizing as you go
                    }
                }
                int rowsAffected = command.ExecuteNonQuery();// execute the command returning number of rows affected
                Console.WriteLine(rowsAffected + " row(s) inserted");//logit
            }
        }
    }
}
