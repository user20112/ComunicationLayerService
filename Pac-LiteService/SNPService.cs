﻿using SNPService.Comunications;
using SNPService.Packets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace SNPService
{
    public partial class SNPService : ServiceBase
    {
        #region Variable Section

        public delegate void FunctionThatFailed(string message);                                                        //Delegate for the function that failed to be passed to restablish connection. gets called after connection is reastablished

        private delegate void SetTextCallback(string text);                                                             //delegate for the function that is to be called when a message is received from the topic subscriber

        private TopicSubscriber MainInputSubsriber;                                                                     //Main subscriber subs to SNP.Inbound
        public static SqlConnectionStringBuilder ENGDBConnection;                                                       //Connection to the ENGDB default db is SNPDb.
        private EMPPackets EMPPackets;                                                                                  //collection of all EMP Packets and functions
        private SNPPackets SNPPackets;                                                                                  //collection of all snp packets and functions
        private ControlPackets ControlPackets;                                                                          //collection of all Control packets and functions
        private ChainStretchPackets ChainStretchPackets;                                                                //collection of all Chain Stretch packets and functions
        private List<Disposable> ThingsToDispose;                                                                       //whenever you make something that inherits from IDisposable and needs to be disposed add to this. iterates through at end disposing of items.
        public static ConcurrentQueue<DiagnosticItem> DiagnosticItems = new ConcurrentQueue<DiagnosticItem>();          //queue for storing Diagnostic Items
        public static int LogggingLevel;                                                                                //what logging level the service has selected
        public static bool Listening;                                                                                   //is the service listening to non control packets
        public static bool Sending;                                                                                     //is the service sending packets out to the real world
        private bool running = false;                                                                                   //controls the diagnostic thread. when false the thread drops through and ends.
        private string SubTopicName;                                                                                    //Topic the Main Subscriber is subbed to
        private string Broker;                                                                                          //IP of the broker we are connecting to
        private string ClientID;                                                                                        //Client ID for the SNP Service
        private string ConsumerID;                                                                                      //Consumer ID for the SNP service
        private static string ENG_DBDataSource;                                                                         //Engineering Database Ip Address
        private static string ENG_DBUserID;                                                                             //Engineering databse user used to comunicate
        private static string ENG_DBPassword;                                                                           //Engineering databse password used to comunicate
        private static string ENG_DBInitialCatalog;                                                                     //Engineering Database that we are talking to
        private static bool fixingconnection = false;                                                                   //set high when we are fixing connection to stop every broken packet from trying but allowing the first to

        #endregion Variable Section

        #region Service Section

        public SNPService()
        {
            InitializeComponent();
            DiagnosticItems.Enqueue(new DiagnosticItem("Hello World!", 1));                                            //say hello to the world
        }

        protected override void OnStart(string[] args)
        {
            CallOnStart();
            DiagnosticItems.Enqueue(new DiagnosticItem("Started up.", 3));                                              // report making it through startup
        }

        /// <summary>
        ///  Called on service stop
        /// </summary>
        protected override void OnStop()
        {
            base.OnStop();
            CallOnStop();
            DiagnosticItems.Enqueue(new DiagnosticItem("Stopped", 3));                                                  // report making it through stoping
        }

        /// <summary>
        /// Function for the diagnostic thread. loops untill running is set false displaying anything enqueed into diagnostic items to the diagnostic file.
        /// </summary>
        public void DiagnosticThread()
        {
            while (running)
            {
                try
                {
                    if (DiagnosticItems.Count > 0)
                    {
                        DiagnosticItem Result = new DiagnosticItem("Diagnostic Threading Issue.", 1);
                        DiagnosticItems.TryDequeue(out Result);
                        if (Result.message != "Diagnostic Threading Issue.")
                        {
                            string DiagnosticMessage = Result.message.Replace(",", ""); ;                               //message to be output to the File
                            DiagnosticMessage += ", TimeStamp:" + DateTime.Now.ToString() + ", LoggingLevelNeeded: " + Result.logginglevel.ToString() + ", LoggingLevelSelected" + LogggingLevel.ToString();//add all sorts of diagnostic datas
                            if (Result.logginglevel <= LogggingLevel)                                                   //if we are asking to see this data
                            {
                                using (StreamWriter DiagnosticWriter = File.AppendText(ConfigurationManager.AppSettings["DiagnosticFile"]))// @"C:\Users\d.paddock\Desktop\Diagnostic.csv")) defualt
                                {
                                    DiagnosticWriter.WriteLine(DiagnosticMessage);                                      //output it to file
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { }                                                                               //catch any errors and cry becouse we cant log them.
            }
        }

        #endregion Service Section

        #region Connections/Resources/Misc

        /// <summary>
        ///  called whenever a mqtt message from ActiveMQ is received
        /// </summary>
        private void MainInputSubsriber_OnmessageReceived(string message)
        {
            try
            {
                DiagnosticItems.Enqueue(new DiagnosticItem(message, 4));                                                //log message and bits when it comes in.
                DiagnosticItems.Enqueue(new DiagnosticItem("Packet Header =" + Convert.ToInt32(message[0]).ToString(), 3));//log the header
                DiagnosticItems.Enqueue(new DiagnosticItem("Packet Type=" + Convert.ToInt32(message[1]).ToString(), 3));//and type
                DiagnosticItems.Enqueue(new DiagnosticItem("SNPID=" + Convert.ToInt32(message[2]).ToString(), 3));      //and SNP ID
                switch (Convert.ToInt32(message[0]))                                                                    //switch packet header
                {
                    case 1:                                                                                             //this means its a SNP message
                        if (Listening && Sending)
                        {
                            switch (Convert.ToInt32(message[1]))                                                        //switch Packet Type
                            {
                                //run the procedure in the background dont await as we dont need the return values as it should be void.
                                case 1:                                                                                 //Index Summary Packet
                                    Task.Run(() => SNPPackets.IndexSummaryPacket(message));
                                    break;

                                case 2:                                                                                 //Downtime Packet
                                    Task.Run(() => SNPPackets.DowntimePacket(message));
                                    break;

                                case 3:                                                                                 //Short Time Statistics  Packet
                                    Task.Run(() => SNPPackets.ShortTimeStatisticPacket(message));
                                    break;

                                case 4:                                                                                 //Product Changeover Packet
                                    Task.Run(() => SNPPackets.ProductChangeOverPacket(message));
                                    break;

                                case 252:                                                                               //Delete Machine Packet
                                    Task.Run(() => SNPPackets.DeleteMachinePacket(message));
                                    break;

                                case 253:                                                                               //Edit Machine Packet
                                    Task.Run(() => SNPPackets.EditMachinePacket(message));
                                    break;

                                case 254:                                                                               //New Machine Packet
                                    Task.Run(() => SNPPackets.NewMachinePacket(message));
                                    Thread.Sleep(100);
                                    break;

                                default:                                                                                //Unrecognized  Packet
                                    break;
                            }
                        }
                        else                                                                                            //if you are silenced and receive a message log it
                            DiagnosticItems.Enqueue(new DiagnosticItem("Received a packet But I am either not sending or Listening!", 2));
                        break;

                    case 2:                                                                                             //this means its an EMP message
                        if (Listening && Sending)
                        {
                            switch (Convert.ToInt32(message[1]))                                                        //switch Packet Type
                            {
                                //run the procedure in the background dont await as we dont need the return values as it should be void.
                                case 1:
                                    Task.Run(() => EMPPackets.IndexPacket(message));                                    //Index  Packet
                                    break;

                                case 2:
                                    Task.Run(() => EMPPackets.WarningPacket(message));                                  //Warning  Packet
                                    break;

                                default:                                                                                //UnRecognized Packet
                                    break;
                            }
                        }
                        else                                                                                            //if you are silenced or deafend and receive a packet logit
                            DiagnosticItems.Enqueue(new DiagnosticItem("Received a packet But I am either not sending or Listening!", 2));
                        break;

                    case 3:                                                                                             //this means its a Control message
                        switch (Convert.ToInt32(message[1]))                                                            //switch Packet Type
                        {
                            //run the procedure in the background dont await as we dont need the return values as it should be void.
                            case 1:                                                                                     //Logging Level Packet
                                Task.Run(() => ControlPackets.LoggingLevel(message));
                                break;
                            //Silence Packet
                            case 2:
                                Task.Run(() => ControlPackets.Silence(message));
                                break;

                            case 3:                                                                                     //Deafen Packet
                                Task.Run(() => ControlPackets.Deafen(message));
                                break;

                            default:                                                                                    //UnRecognized Packet
                                break;
                        }
                        break;

                    case 4:
                        if (Listening && Sending)
                        {                                                                                           //this means its a Control message
                            switch (Convert.ToInt32(message[1]))                                                            //switch Packet Type
                            {
                                //run the procedure in the background dont await as we dont need the return values as it should be void.
                                case 1:                                                                                     //Index packet
                                    Task.Run(() => ChainStretchPackets.Index(message));
                                    break;

                                default:                                                                                    //UnRecognized Packet
                                    break;
                            }
                        }
                        break;

                    case 254:
                        if (Listening && Sending)
                        {                                                                                       //this means its a Control message
                            switch (Convert.ToInt32(message[1]))                                                            //switch Packet Type
                            {
                                //run the procedure in the background dont await as we dont need the return values as it should be void.
                                case 1:                                                                                     //Index packet
                                    Task.Run(() => GenericPackets.RunSQLCommand(message));
                                    break;

                                default:                                                                                    //UnRecognized Packet
                                    break;
                            }
                        }
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)                                                                                        //catch exceptions
            {
                DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));                                          //log it
            }
        }

        /// <summary>
        /// Collection of MQTT Connection setup
        /// </summary>
        private void MQTTConnections()
        {
            try
            {
                DiagnosticItems.Enqueue(new DiagnosticItem("Connecting MainSubscriber", 2));
                MainInputSubsriber = new TopicSubscriber(SubTopicName, Broker, ClientID, ConsumerID);                   //connect the main Subscriber
                MainInputSubsriber.OnMessageReceived += new MessageReceivedDelegate(MainInputSubsriber_OnmessageReceived);//add the deligate for when a message is received
                ThingsToDispose.Add(new Disposable(nameof(MainInputSubsriber), MainInputSubsriber));                    //add to reference pile so it disposes of itself properly.
            }
            catch (Exception ex) { DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1)); }                     //logit
            //try
            //{
            //     DiagnosticItems.Enqueue (new DiagnosticItem("Connecting SNPPublisher", 2));
            //    SNPPackets.Publisher = new TopicPublisher(SNPPackets.TopicName, Broker);
            //    ThingsToDispose.Add(new Disposable(nameof(SNPPackets.Publisher), SNPPackets.Publisher));
            //}
            //catch (Exception ex) {  DiagnosticItems.Enqueue (new DiagnosticItem(ex.ToString(), 1)); }
            try
            {
                DiagnosticItems.Enqueue(new DiagnosticItem("Connecting EMP Publisher", 2));                             //connect the EMp Publisher
                EMPPackets.Publisher = new TopicPublisher(EMPPackets.TopicName, Broker);
                ThingsToDispose.Add(new Disposable(nameof(EMPPackets.Publisher), EMPPackets.Publisher));                //add it to things to dispose
            }
            catch (Exception ex) { DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1)); }                     //logit
        }

        /// <summary>
        /// Collection of SQL Connection setup
        /// </summary>
        private void SQLConnections()
        {
            try
            {
                // Build connection string
                DiagnosticItems.Enqueue(new DiagnosticItem("Connecting SQL Database", 2));
                ENGDBConnection = new SqlConnectionStringBuilder();                                                     //create a string builder to connect to the database
                ENGDBConnection.DataSource = ENG_DBDataSource;                                                          //give it the IP
                ENGDBConnection.UserID = ENG_DBUserID;                                                                  //and the username
                ENGDBConnection.Password = ENG_DBPassword;                                                              //password
                ENGDBConnection.InitialCatalog = ENG_DBInitialCatalog;                                                  //and finally the starting database
                //ENGDBConnection.IntegratedSecurity = true;
            }
            catch (Exception ex) { DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1)); }                     //logit
        }

        /// <summary>
        /// Collection of TCP Connection setup
        /// </summary>
        private void TCPConnections()                                                                                   // no TCP Connections anymore :/ still able though!
        {
        }

        /// <summary>
        /// Collection of UDP Connection setup
        /// </summary>
        private void UDPConnections()                                                                                   //no udp connections anymore :/ still able though!
        {
            // DiagnosticItems.Enqueue (new DiagnosticItem("Connecting to MDE", 2);
            //try
            //{
            //    SNPPackets.MDEClient = new UdpClient(SNPPackets.MDEOutPort);
            //    ThingsToDispose.Add(new Disposable(nameof(SNPPackets.MDEClient), SNPPackets.MDEClient));
            //}
            //catch (Exception ex) {  DiagnosticItems.Enqueue (new DiagnosticItem(ex.ToString(), 1); }//logit
        }

        /// <summary>
        /// Call On stop of service
        /// </summary>
        private void CallOnStop()
        {
            running = false;                                                                                            //stop the diagnostics thread
            //try
            //{
            //    SNPPackets.MDEClient.Close();//close UDP connections to.
            //}
            //catch
            //{
            //}
            foreach (Disposable disposable in ThingsToDispose)                                                          //foreach thing to dispose
            {
                try
                {
                    disposable.Dispose();                                                                               //try to dispose it ( they will be regenerated on start
                    DiagnosticItems.Enqueue(new DiagnosticItem(disposable.Name + "Has been Disconected and Disposed", 2));//logit
                }
                catch (Exception ex) { DiagnosticItems.Enqueue(new DiagnosticItem(disposable.Name + ex.ToString(), 1)); }//logit o.o
            }
        }

        /// <summary>
        /// Call On start of service
        /// </summary>
        private void CallOnStart()
        {
            try
            {
                if (ConfigurationManager.AppSettings["ResetENGDBPassword"] != "")                                       //if the reset engineering password needs to be reset
                {
                    Encryptor.UpdateEngDBPassword(ConfigurationManager.AppSettings["ResetENGDBPassword"], true);        //do so
                }
                if (ConfigurationManager.AppSettings["ResetCamstarPassword"] != "")                                     //if the camstar password needs to be reset
                {
                    Encryptor.UpdateCamstarPassword(ConfigurationManager.AppSettings["ResetCamstarPassword"], true);      //do so
                }
                running = true;                                                                                         //stops diagnostic thread from dropping through until onstop is called.
                Task.Run(() => DiagnosticThread());                                                                     //start diagnostic thread. ( jsut loops displaying errors.
                SubTopicName = ConfigurationManager.AppSettings["MainTopicName"];                                       //load everything from the app settings
                Broker = ConfigurationManager.AppSettings["BrokerIP"];
                ClientID = ConfigurationManager.AppSettings["ClientID"];
                ConsumerID = ConfigurationManager.AppSettings["ConsumerID"];
                ENG_DBDataSource = ConfigurationManager.AppSettings["ENGDBIP"];
                ENG_DBUserID = ConfigurationManager.AppSettings["ENGDBUser"];
                ENG_DBPassword = Encryptor.EncryptOrDecrypt(ConfigurationManager.AppSettings["ENGDBPassword"]);
                ENG_DBInitialCatalog = ConfigurationManager.AppSettings["ENGDBDatabase"];
                LogggingLevel = Convert.ToInt32(ConfigurationManager.AppSettings["LogggingLevel"]);
                Listening = Convert.ToInt32(ConfigurationManager.AppSettings["Listening"]) == 1;
                Sending = Convert.ToInt32(ConfigurationManager.AppSettings["Sending"]) == 1;
                EMPPackets = new EMPPackets();                                                                          //generate the packet classes
                SNPPackets = new SNPPackets();
                ControlPackets = new ControlPackets();
                ChainStretchPackets = new ChainStretchPackets();
                ThingsToDispose = new List<Disposable>();                                                               //reset list of objects that need to be disposed
                Task.Run(() => MQTTConnections());                                                                      //open all MQTT Connections
                Task.Run(() => SQLConnections());                                                                       //open alll SQL Connections
                Task.Run(() => TCPConnections());                                                                       //open all TCPConnections
                Task.Run(() => UDPConnections());                                                                       //open all UDP Connections
            }
            catch (Exception ex)                                                                                        //catch exceptions
            {
                DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));                                          //logem
            }
        }

        /// <summary>
        /// Called whenever there seems to be no sql connection
        /// </summary>
        public static void ReastablishSQL(FunctionThatFailed functionThatFailed, string message)
        {
            if (fixingconnection)                                                                                       //if we are already fixing the connection
            {
                while (fixingconnection)                                                                                //dont touch anything and just sleep for a little bit
                {
                    Thread.Sleep(100);
                }
            }
            else                                                                                                        //otherwise fix the connection
            {
                fixingconnection = true;                                                                                //and tell others not to
                try
                {
                    DiagnosticItems.Enqueue(new DiagnosticItem("Connecting SQL Database", 2));                          //logggggggggiittttttttt
                    ENGDBConnection = new SqlConnectionStringBuilder();                                                 //builder to connect to the database
                    ENGDBConnection.DataSource = ENG_DBDataSource;                                                      //pass it the ip username password and starting database
                    ENGDBConnection.UserID = ENG_DBUserID;
                    ENGDBConnection.Password = ENG_DBPassword;
                    ENGDBConnection.InitialCatalog = ENG_DBInitialCatalog;
                }
                catch (Exception ex) { DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1)); }                 //logit
                fixingconnection = false;                                                                               //unlock it
            }
            functionThatFailed(message);                                                                                //recall the function that failed
        }

        #endregion Connections/Resources/Misc
    }
}