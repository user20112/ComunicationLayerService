using Pac_LiteService.Comunications;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
namespace Pac_LiteService
{
    public partial class PacLiteService : ServiceBase
    {
        int LogggingLevel = 5;
        public PacLiteService()
        {
            InitializeComponent();
            Console.WriteLine("Initialized!");
        }

        protected override void OnStart(string[] args)
        {
            DiagnosticOut("Started up.",3);
            CallOnStart();
        }

        /// <summary>
        ///  Called on service stop
        /// </summary>
        protected override void OnStop()
        {
            base.OnStop();
            CallOnStop();
        }

        public void DiagnosticOut(string message,int LoggingLevel)
        {
            try
            {
                if(LoggingLevel<=LogggingLevel)
                {
                    using (StreamWriter DiagnosticWriter = File.AppendText(@"C:\Users\d.paddock\Desktop\Diagnostic.txt"))
                    {
                        DiagnosticWriter.WriteLine(message);
                    }
                }
            }
            catch
            {
            }
        }

        //above is winforms specific code. below should be portable to service.

        #region Variable Section

        public delegate void FunctionThatFailed(string message);

        private delegate void SetTextCallback(string text);

        private TopicSubscriber MainInputSubsriber;//Main subscriber subs to SNP.Inbound
        public SqlConnection ENGDBConnection;//Connection to the ENGDB default db is SNPDb.
        private EMPPackets EMPPackets;
        private SNPPackets SNPPackets;

        private const string SubTopicName = "SNP.Inbound";
        private const string Broker = "tcp://10.197.10.32:61616";
        private const string ClientID = "SNPService";
        private const string ConsumerID = "SNPService";
        private const string ProdENG_DBDataSource = "10.197.10.26";
        private const string QAENG_DBDataSource = "10.197.10.37";
        private const string ENG_DBUserID = "camstaruser";
        private const string ENG_DBPassword = "c@mst@rus3r";
        private const string ENG_DBInitialCatalog = "Pac-LiteDb";

        private List<Disposable> ThingsToDispose;//whenever you make something that inherits from IDisposable and needs to be disposed add to this. iterates through at end disposing of items.

        private bool fixingconnection = false;

        #endregion Variable Section

        #region Connections/Resources/Misc

        /// <summary>
        ///  called whenever a mqtt message from ActiveMQ is received
        /// </summary>
        private void MainInputSubsriber_OnmessageReceived(string message)
        {
            try
            {
                DiagnosticOut(message,4);//log message and bits when it comes in.
                DiagnosticOut("Packet Header =" + Convert.ToInt32(message[0]).ToString(),3);
                DiagnosticOut("Packet Type=" + Convert.ToInt32(message[1]).ToString(),3);
                DiagnosticOut("SNPID=" + Convert.ToInt32(message[2]).ToString(),3);
                switch (Convert.ToInt32(message[0]))//switch packet header
                {
                    case 1://this means its a SNP message

                        switch (Convert.ToInt32(message[1]))//switch Packet Type
                        {
                            //run the procedure in the background dont await as we dont need the return values as it should be void.
                            case 1:
                                Task.Run(() => SNPPackets.IndexSummaryPacket(message));
                                break;

                            case 2:
                                Task.Run(() => SNPPackets.DowntimePacket(message));
                                break;

                            case 3:
                                Task.Run(() => SNPPackets.ShortTimeStatisticPacket(message));
                                break;

                            case 252:
                                Task.Run(() => SNPPackets.DeleteMachinePacket(message));
                                break;

                            case 253:
                                Task.Run(() => SNPPackets.EditMachinePacket(message));
                                break;

                            case 254:
                                Task.Run(() => SNPPackets.NewMachinePacket(message));
                                Thread.Sleep(100);
                                break;

                            default:
                                break;
                        }
                        break;

                    case 2://this means its a SNP message
                        switch (Convert.ToInt32(message[1]))//switch Packet Type
                        {
                            //run the procedure in the background dont await as we dont need the return values as it should be void.
                            case 1:
                                Task.Run(() => EMPPackets.IndexPacket(message));
                                break;

                            case 2:
                                Task.Run(() => EMPPackets.WarningPacket(message));
                                break;

                            default:
                                break;
                        }
                        break;

                    default:
                        break;
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Collection of MQTT Connection setup
        /// </summary>
        private void MQTTConnections()
        {
            try
            {
                DiagnosticOut("Connecting MainSubscriber",2);
                MainInputSubsriber = new TopicSubscriber(SubTopicName, Broker, ClientID, ConsumerID);
                MainInputSubsriber.OnMessageReceived += new MessageReceivedDelegate(MainInputSubsriber_OnmessageReceived);
                ThingsToDispose.Add(new Disposable(nameof(MainInputSubsriber), MainInputSubsriber));//add to reference pile so it disposes of itself properly.
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString(),1); }
            try
            {
                DiagnosticOut("Connecting SNPPublisher",2);
                SNPPackets.Publisher = new TopicPublisher(SNPPackets.TopicName, Broker);
                ThingsToDispose.Add(new Disposable(nameof(SNPPackets.Publisher), SNPPackets.Publisher));
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString(),1); }
            try
            {
                DiagnosticOut("Connecting SNPPublisher",2);
                EMPPackets.Publisher = new TopicPublisher(EMPPackets.TopicName, Broker);
                ThingsToDispose.Add(new Disposable(nameof(EMPPackets.Publisher), EMPPackets.Publisher));
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString(),1); }
        }

        /// <summary>
        /// Collection of SQL Connection setup
        /// </summary>
        private void SQLConnections()
        {
            try
            {
                // Build connection string
                DiagnosticOut("Connecting SQL Database",2);
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                builder.DataSource = QAENG_DBDataSource;
                builder.UserID = ENG_DBUserID;
                builder.Password = ENG_DBPassword;
                builder.InitialCatalog = ENG_DBInitialCatalog;
                ENGDBConnection = new SqlConnection(builder.ConnectionString);
                ENGDBConnection.Open();
                ThingsToDispose.Add(new Disposable(nameof(ENGDBConnection), ENGDBConnection));
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString(),1); }
        }

        /// <summary>
        /// Collection of TCP Connection setup
        /// </summary>
        private void TCPConnections()
        {
        }

        /// <summary>
        /// Collection of UDP Connection setup
        /// </summary>
        private void UDPConnections()
        {
            DiagnosticOut("Connecting to MDE",2);
            try
            {
                SNPPackets.MDEClient = new UdpClient(SNPPackets.MDEOutPort);
                ThingsToDispose.Add(new Disposable(nameof(SNPPackets.MDEClient), SNPPackets.MDEClient));
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString(),1); }
        }

        /// <summary>
        /// Call On stop of service
        /// </summary>
        private void CallOnStop()
        {
            try
            {
                SNPPackets.MDEClient.Close();//close UDP connections to.
            }
            catch
            {
            }
            foreach (Disposable disposable in ThingsToDispose)
            {
                try
                {
                    disposable.Dispose();//dispose of connections on stop they will be reestablished on start.
                    DiagnosticOut(disposable.Name + "Has been Disconected and Disposed",2);
                }
                catch (Exception ex) { DiagnosticOut(disposable.Name + ex.ToString(),1); }
            }
        }

        /// <summary>
        /// Call On start of service
        /// </summary>
        private void CallOnStart()
        {
            EMPPackets = new EMPPackets(this);
            SNPPackets = new SNPPackets(this);
            try
            {
                ThingsToDispose = new List<Disposable>();
                Task.Run(() => MQTTConnections());//open all MQTT Connections
                Task.Run(() => SQLConnections());//open alll SQL Connections
                Task.Run(() => TCPConnections());//open all TCPConnections
                Task.Run(() => UDPConnections());//open all UDP Connections
            }
            catch (Exception ex)
            {
                DiagnosticOut(ex.ToString(),1);
            }
        }

        /// <summary>
        /// Called whenever there seems to be no sql connection
        /// </summary>
        public void ReastablishSQL(FunctionThatFailed functionThatFailed, string message)
        {
            if (fixingconnection)
            {
                while (fixingconnection)
                {
                    Thread.Sleep(100);
                }
            }
            else
            {
                fixingconnection = true;
                try
                {
                    DiagnosticOut("Connecting SQL Database",2);
                    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                    builder.DataSource = QAENG_DBDataSource;
                    builder.UserID = ENG_DBUserID;
                    builder.Password = ENG_DBPassword;
                    builder.InitialCatalog = ENG_DBInitialCatalog;
                    ENGDBConnection = new SqlConnection(builder.ConnectionString);
                    ENGDBConnection.Open();
                    ThingsToDispose.Add(new Disposable(nameof(ENGDBConnection), ENGDBConnection));
                }
                catch (Exception ex) { DiagnosticOut(ex.ToString(),1); }
                fixingconnection = false;
            }
            functionThatFailed(message);
        }

        #endregion Connections/Resources/Misc
    }
}