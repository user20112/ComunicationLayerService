using Camstar.Utility;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using VisualVersionofService.Comunications;

namespace VisualVersionofService
{
    public partial class Form1 : Form
    {
        #region Winforms Section

        public static Form1 MainForm;
        private int ListviewIndex = 0;

        public Form1()
        {
            InitializeComponent();
            ColumnHeader columnHeader1 = new ColumnHeader();
            columnHeader1.Text = "Text";
            DiagnosticListView.View = View.Details;
            DiagnosticListView.Columns.Add("Column1", -2);
            DiagnosticListView.GridLines = true;
            MainForm = this;
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            Task.Run(() => CallOnStart());
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            Task.Run(() => CallOnStop());
        }

        public void DiagnosticOut(string message, int LoggingLevel)
        {
            try
            {
                string DiagnosticMessage = "";
                for (int x = 0; x < message.Length; x++)
                {
                    if (message[x] != ',')
                        DiagnosticMessage += message[x];
                }
                DiagnosticMessage += ", TimeStamp:" + DateTime.Now.ToString() + ", LoggingLevelNeeded: " + LoggingLevel.ToString() + ", LoggingLevelSelected" + LogggingLevel.ToString();
                if (LoggingLevel <= LogggingLevel)
                {
                    MainForm.Invoke((MethodInvoker)delegate
                    {
                        MainForm.DiagnosticListView.Items.Add(new ListViewItem(new string[] { ListviewIndex.ToString(), DiagnosticMessage }));
                    });
                    ListviewIndex++;
                }
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString(), 1); }
        }

        //above is winforms specific code. below should be portable to service.

        #endregion Winforms Section

        #region Variable Section

        public int LogggingLevel;
        public bool Listening;
        public bool Sending;

        public delegate void FunctionThatFailed(string message);

        private delegate void SetTextCallback(string text);

        private TopicSubscriber MainInputSubsriber;//Main subscriber subs to SNP.Inbound
        public SqlConnection ENGDBConnection;//Connection to the ENGDB default db is SNPDb.
        private EMPPackets EMPPackets;
        private SNPPackets SNPPackets;
        private ControlPackets ControlPackets;

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
                DiagnosticOut(message, 4);//log message and bits when it comes in.
                DiagnosticOut("Packet Header =" + Convert.ToInt32(message[0]).ToString(), 3);
                DiagnosticOut("Packet Type=" + Convert.ToInt32(message[1]).ToString(), 3);
                DiagnosticOut("SNPID=" + Convert.ToInt32(message[2]).ToString(), 3);
                switch (Convert.ToInt32(message[0]))//switch packet header
                {
                    case 1://this means its a SNP message
                        if (Listening && Sending)
                        {
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
                        }
                        else
                            DiagnosticOut("Received a packet But I am either not sending or Listening!", 2);
                        break;

                    case 2://this means its an EMP message
                        if (Listening && Sending)
                        {
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
                        }
                        else
                            DiagnosticOut("Received a packet But I am either not sending or Listening!", 2);
                        break;

                    case 3://this means its a Control message
                        switch (Convert.ToInt32(message[1]))//switch Packet Type
                        {
                            //run the procedure in the background dont await as we dont need the return values as it should be void.
                            case 1:
                                Task.Run(() => ControlPackets.LoggingLevel(message));
                                break;

                            case 2:
                                Task.Run(() => ControlPackets.Silence(message));
                                break;

                            case 3:
                                Task.Run(() => ControlPackets.Deafen(message));
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
                DiagnosticOut("Connecting MainSubscriber", 2);
                MainInputSubsriber = new TopicSubscriber(SubTopicName, Broker, ClientID, ConsumerID);
                MainInputSubsriber.OnMessageReceived += new MessageReceivedDelegate(MainInputSubsriber_OnmessageReceived);
                ThingsToDispose.Add(new Disposable(nameof(MainInputSubsriber), MainInputSubsriber));//add to reference pile so it disposes of itself properly.
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString(), 1); }
            try
            {
                DiagnosticOut("Connecting SNPPublisher", 2);
                SNPPackets.Publisher = new TopicPublisher(SNPPackets.TopicName, Broker);
                ThingsToDispose.Add(new Disposable(nameof(SNPPackets.Publisher), SNPPackets.Publisher));
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString(), 1); }
            try
            {
                DiagnosticOut("Connecting SNPPublisher", 2);
                EMPPackets.Publisher = new TopicPublisher(EMPPackets.TopicName, Broker);
                ThingsToDispose.Add(new Disposable(nameof(EMPPackets.Publisher), EMPPackets.Publisher));
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString(), 1); }
        }

        /// <summary>
        /// Collection of SQL Connection setup
        /// </summary>
        private void SQLConnections()
        {
            try
            {
                // Build connection string
                DiagnosticOut("Connecting SQL Database", 2);
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                builder.DataSource = QAENG_DBDataSource;
                builder.UserID = ENG_DBUserID;
                builder.Password = ENG_DBPassword;
                builder.InitialCatalog = ENG_DBInitialCatalog;
                ENGDBConnection = new SqlConnection(builder.ConnectionString);
                ENGDBConnection.Open();
                ThingsToDispose.Add(new Disposable(nameof(ENGDBConnection), ENGDBConnection));
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString(), 1); }
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
            DiagnosticOut("Connecting to MDE", 2);
            try
            {
                SNPPackets.MDEClient = new UdpClient(SNPPackets.MDEOutPort);
                ThingsToDispose.Add(new Disposable(nameof(SNPPackets.MDEClient), SNPPackets.MDEClient));
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString(), 1); }
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
                    DiagnosticOut(disposable.Name + "Has been Disconected and Disposed", 2);
                }
                catch (Exception ex) { DiagnosticOut(disposable.Name + ex.ToString(), 1); }
            }
        }

        /// <summary>
        /// Call On start of service
        /// </summary>
        private void CallOnStart()
        {
            LogggingLevel = Convert.ToInt32(ConfigurationManager.AppSettings["LogggingLevel"]);
            Listening = Convert.ToInt32(ConfigurationManager.AppSettings["Listening"]) == 1;
            Sending = Convert.ToInt32(ConfigurationManager.AppSettings["Sending"]) == 1;
            EMPPackets = new EMPPackets(this);
            SNPPackets = new SNPPackets(this);
            ControlPackets = new ControlPackets(this);
            try
            {
                ThingsToDispose = new List<Disposable>();
                Task.Run(() => MQTTConnections());//open all MQTT Connections
                Task.Run(() => SQLConnections());//open alll SQL Connections
                Task.Run(() => TCPConnections());//open all TCPConnections
                Task.Run(() => UDPConnections());//open all UDP Connections
                Task.Run(() => CamstarConnect(ConfigurationManager.AppSettings["CamstarUsername"], ConfigurationManager.AppSettings["CamstarPassword"]));//open Camstar Connection
            }
            catch (Exception ex)
            {
                DiagnosticOut(ex.ToString(), 1);
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
                    DiagnosticOut("Connecting SQL Database", 2);
                    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                    builder.DataSource = QAENG_DBDataSource;
                    builder.UserID = ENG_DBUserID;
                    builder.Password = ENG_DBPassword;
                    builder.InitialCatalog = ENG_DBInitialCatalog;
                    ENGDBConnection = new SqlConnection(builder.ConnectionString);
                    ENGDBConnection.Open();
                    ThingsToDispose.Add(new Disposable(nameof(ENGDBConnection), ENGDBConnection));
                }
                catch (Exception ex) { DiagnosticOut(ex.ToString(), 1); }
                fixingconnection = false;
            }
            functionThatFailed(message);
        }

        /// <summary>
        /// Connects to Camstar with the username and password provided.
        /// </summary>
        private void CamstarConnect(string UserName, string Password)
        {
            MainForm.DiagnosticOut("Connecting Camstar", 2);
            string DataReceived;
            try
            {
                string PacketString = "<__InSite __encryption=\"2\" __version=\"1.1\"><__session><__connect><user><__name>" + UserName + "</__name></user><password __encrypted=\"no\">" + Password + "</password></__connect></__session></__InSite>";
                var connection = new ServerConnection();
                var connected = connection.Connect(ConfigurationManager.AppSettings["CamstarIP"], Convert.ToInt32(ConfigurationManager.AppSettings["CamstarPort"])); // try connecting
                if (!connected) return; // return nothing if cant connect
                connection.Send(PacketString.ToString()); // send data
                connection.Receive(out var result); // reviece message from server, and store into variable
                connection.Disconnect(); // Close connection
                try
                {
                    DataReceived = XDocument.Parse(result).ToString(); // format recieved message into xml
                }
                catch
                {
                    DataReceived = result;
                }
                MainForm.DiagnosticOut(DataReceived, 5);
            }
            catch (Exception ex) { MainForm.DiagnosticOut(ex.ToString(), 1); }
        }

        public void ChangeConfig(string key, string value)
        {
            System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings[key].Value = value;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        #endregion Connections/Resources/Misc
    }
}