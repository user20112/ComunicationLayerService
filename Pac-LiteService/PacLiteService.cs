using Camstar.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pac_LiteService.Comunications;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Pac_LiteService
{
    public partial class PacLiteService : ServiceBase
    {
        public PacLiteService()
        {
            InitializeComponent();
            Console.WriteLine("Initialized!");
        }

        protected override void OnStart(string[] args)
        {
            DiagnosticOut("here");
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
        private void DiagnosticOut(string message)
        {
            try
            {
                using (StreamWriter DiagnosticWriter = File.AppendText(@"C:\Users\d.paddock\Desktop\Diagnostic.txt"))
                {
                    DiagnosticWriter.WriteLine(message);
                }
            }
            catch
            {
            }
        }

        //above is winforms specific code. below should be portable to service.
        private TopicPublisher MainPublisher;//publishes to the Pac-Light Outbound topic

        private TopicSubscriber MainInputSubsriber;//Main subscriber subs to SNP.Inbound
        private SqlConnection ENGDBConnection;//Connection to the ENGDB default db is SNPDb.
        private UdpClient MDEClient;

        public delegate void FunctionThatFailed(string message);

        private const string SubTopicName = "SNP.Inbound";
        private const string TestTopicName = "SNP.Outbound";
        private const string Broker = "tcp://10.197.10.32:61616";
        private const string ClientID = "SNPService";
        private const string ConsumerID = "SNPService";
        private const string CamstarUsername = "AutoLoader";
        private const string CamstarPassword = "@utoLo@der";
        private const string QACamstarIP = "10.197.10.33";
        private const string ProdCamstarIP = "10.197.10.22";
        private const string ProdENG_DBDataSource = "10.197.10.26";
        private const string QAENG_DBDataSource = "10.197.10.37";
        private const string ENG_DBUserID = "camstaruser";
        private const string ENG_DBPassword = "c@mst@rus3r";
        private const string ENG_DBInitialCatalog = "Pac-LiteDb";
        private const string MDEIP = "10.197.18.163";//currently my ip for testing. once it is known to be working i have to get this ip from gerry.
        private const int CamstarPort = 2881;
        private const int MDEClientPort = 11000;
        private const int MDEOutPort = 12000;
        private List<Disposable> ThingsToDispose;//whenever you make something that inherits from IDisposable and needs to be disposed add to this. iterates through at end disposing of items.
        private bool fixingconnection = false;

        private delegate void SetTextCallback(string text);

        /// <summary>
        ///  called whenever a mqtt message from SNP is received
        /// </summary>
        private void MainInputSubsriber_OnmessageReceived(string message)
        {
            try
            {
                DiagnosticOut(message);//log message and bits when it comes in.
                DiagnosticOut("Packet Header =" + Convert.ToInt32(message[0]).ToString());
                DiagnosticOut("Packet Type=" + Convert.ToInt32(message[1]).ToString());
                DiagnosticOut("SNPID=" + Convert.ToInt32(message[2]).ToString());
                switch (Convert.ToInt32(message[0]))//switch packet header
                {
                    case 1://this means its a SNP message

                        switch (Convert.ToInt32(message[1]))//switch Packet Type
                        {
                            //run the procedure in the background dont await as we dont need the return values as it should be void.
                            case 1:
                                Task.Run(() => IndexSummaryPacket(message));
                                break;

                            case 2:
                                Task.Run(() => DowntimePacket(message));
                                break;

                            case 3:
                                Task.Run(() => ShortTimeStatisticPacket(message));
                                break;

                            case 252:
                                Task.Run(() => DeleteMachinePacket(message));
                                break;

                            case 253:
                                Task.Run(() => EditMachinePacket(message));
                                break;

                            case 254:
                                Task.Run(() => NewMachinePacket(message));
                                Thread.Sleep(100);
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
        /// Called whenever a new machine is detected
        /// </summary>
        private void NewMachinePacket(string message)
        {
            string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
            JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
            string machineName = receivedPacket["Machine"].ToString();
            string Line = receivedPacket["Line"].ToString();
            string Theo = receivedPacket["Theo"].ToString();
            int snp_ID = Convert.ToInt32((byte)message[2]);
            try //try loop in case command fails.
            {
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append(" USE [Pac-LiteDb ] ");
                sqlStringBuilder.Append(" CREATE TABLE [dbo].[" + machineName + "ShortTimeStatistics](");
                sqlStringBuilder.Append("	[MachineID] [int] NULL, [Good] [bit] NULL, [Bad] [bit] NULL, [Empty] [bit] NULL, [Attempt] [bit] NULL, [Error1] [bit] NULL, [Error2] [bit] NULL, [Error3] [bit] NULL, [Error4] [bit] NULL, [Other] [bit] NULL, [HeadNumber] [int] NULL ");
                sqlStringBuilder.Append(" ) ON [PRIMARY] ");
                sqlStringBuilder.Append(" CREATE TABLE [dbo].[" + machineName + "](");
                sqlStringBuilder.Append(" 	[EntryID] [int] IDENTITY(1,1) NOT NULL,	[MachineID] [int] NULL,	[Good] [int] NULL,	[Bad] [int] NULL,	[Empty] [int] NULL,	[Indexes] [int] NULL,	[NAED] [varchar](20) NULL,	[UOM] [varchar](10) NULL,	[Time] [datetime] NULL) ON [PRIMARY] ");
                sqlStringBuilder.Append(" CREATE TABLE [dbo].[" + machineName + "DownTimes](");
                sqlStringBuilder.Append(" 	[Time] [datetime] NULL,	[MReason] [varchar](255) NULL,	[UReason] [varchar](255) NULL,	[NAED] [varchar](20) NULL,	[MachineID] [int] NULL,	[Status] [int] NULL) ON [PRIMARY]; ");
                sqlStringBuilder.Append(" insert into MachineInfoTable (MachineName, Line, SNPID , Theo) values( @machine , @Line , @SNPID , @Theo);");
                string SQLString = sqlStringBuilder.ToString();//convert to string
                using (SqlCommand command = new SqlCommand(SQLString, ENGDBConnection))
                {
                    command.Parameters.AddWithValue("@machine", machineName);
                    command.Parameters.AddWithValue("@Line", Line);
                    command.Parameters.AddWithValue("@SNPID", snp_ID);
                    command.Parameters.AddWithValue("@Theo", Theo);
                    command.ExecuteNonQuery();// execute the command returning number of rows affected
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))
                {
                    ReastablishSQL(SQLShortTimeStatisticPacket, message);
                }
                DiagnosticOut(ex.ToString());
            }
        }

        /// <summary>
        /// Updates an existing machine based of machine name.
        /// </summary>
        private void EditMachinePacket(string message)
        {
            string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
            JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
            string machineName = receivedPacket["Machine"].ToString();
            string Line = receivedPacket["Line"].ToString();
            string Theo = receivedPacket["Theo"].ToString();
            int snp_ID = Convert.ToInt32((byte)message[2]);
            try //try loop in case command fails.
            {
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append(" USE [Pac-LiteDb ] ");
                sqlStringBuilder.Append(" update MachineInfoTable set Line = @Line, SNPID = @SNPID , Theo = @Theo where MachineName = @machine;");
                string SQLString = sqlStringBuilder.ToString();//convert to string
                using (SqlCommand command = new SqlCommand(SQLString, ENGDBConnection))
                {
                    command.Parameters.AddWithValue("@machine", machineName);
                    command.Parameters.AddWithValue("@Line", Line);
                    command.Parameters.AddWithValue("@SNPID", snp_ID);
                    command.Parameters.AddWithValue("@Theo", Theo);
                    command.ExecuteNonQuery();// execute the command returning number of rows affected
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))
                {
                    ReastablishSQL(SQLShortTimeStatisticPacket, message);
                }
                DiagnosticOut(ex.ToString());
            }
        }

        /// <summary>
        /// Deletes existing machine
        /// </summary>
        private void DeleteMachinePacket(string message)
        {
            string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
            JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
            string machineName = receivedPacket["Machine"].ToString();
            try //try loop in case command fails.
            {
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append(" USE [Pac-LiteDb ] ");
                sqlStringBuilder.Append(" delete from MachineInfoTable where MachineName = @machine;");//drop the reference
                sqlStringBuilder.Append("drop table [" + machineName + "];");
                sqlStringBuilder.Append("drop table [" + machineName + "DownTimes];");
                sqlStringBuilder.Append("drop table [" + machineName + "ShortTimeStatistics];");
                string SQLString = sqlStringBuilder.ToString();//convert to string
                using (SqlCommand command = new SqlCommand(SQLString, ENGDBConnection))
                {
                    command.Parameters.AddWithValue("@machine", receivedPacket["Machine"].ToString());
                    command.ExecuteNonQuery();// execute the command returning number of rows affected
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))
                {
                    ReastablishSQL(SQLShortTimeStatisticPacket, message);
                }
                DiagnosticOut(ex.ToString());
            }
        }

        /// <summary>
        /// Called whenever there seems to be no sql connection
        /// </summary>
        private void ReastablishSQL(FunctionThatFailed functionThatFailed, string message)
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
                    DiagnosticOut("Connecting SQL Database");
                    SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                    builder.DataSource = QAENG_DBDataSource;
                    builder.UserID = ENG_DBUserID;
                    builder.Password = ENG_DBPassword;
                    builder.InitialCatalog = ENG_DBInitialCatalog;
                    Console.Write("Connecting to SQL Server ... ");
                    ENGDBConnection = new SqlConnection(builder.ConnectionString);
                    ENGDBConnection.Open();
                    ThingsToDispose.Add(new Disposable(nameof(ENGDBConnection), ENGDBConnection));
                }
                catch (Exception ex) { DiagnosticOut(ex.ToString()); }
                fixingconnection = false;
            }
            functionThatFailed(message);
        }

        /// <summary>
        /// SQL section of the Fifteen Minut Packet Inserts the data into a table named after the machine
        /// </summary>
        private void SQLIndexSummary(string message)
        {
            string SQLString = "";
            try //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append("INSERT INTO " + receivedPacket["Machine"] + "(");
                IList<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();//gets list of all keys in json object
                string keySection = "";
                string valueSection = "";
                foreach (string key in keys)//foreach key
                {
                    if (key == "UOM" || key == "Good" || key == "NAED" || key == "Bad" || key == "Empty" || key == "Indexes")//except machine as it is used as the table name.
                    {
                        keySection += key + ", ";//Make a key
                        valueSection += "@" + key + ", ";//and value Reference to be replaced later
                    }
                }
                keySection += "Time, ";//Make a Time key
                valueSection += "@Time, ";//and value Reference to be replaced later
                keySection += "MachineID ";
                valueSection += "MachineID ";
                sqlStringBuilder.Append(keySection + ")");
                sqlStringBuilder.Append("SELECT " + valueSection + "from MachineInfoTable" + " where MachineName = @Machine ;");//append both to the command string
                SQLString = sqlStringBuilder.ToString();//convert to string
                using (SqlCommand command = new SqlCommand(SQLString, ENGDBConnection))
                {
                    foreach (string key in keys)//foreach key
                    {
                        switch (key)
                        {
                            case "UOM":
                                command.Parameters.AddWithValue("@" + key, receivedPacket[key].ToString());
                                break;

                            case "NAED":
                                command.Parameters.AddWithValue("@" + key, receivedPacket[key].ToString());
                                break;

                            case "Good":
                                command.Parameters.AddWithValue("@" + key, Convert.ToInt32(receivedPacket[key]));
                                break;

                            case "Bad":
                                command.Parameters.AddWithValue("@" + key, Convert.ToInt32(receivedPacket[key]));
                                break;

                            case "Empty":
                                command.Parameters.AddWithValue("@" + key, Convert.ToInt32(receivedPacket[key]));
                                break;

                            case "Indexes":
                                command.Parameters.AddWithValue("@" + key, Convert.ToInt32(receivedPacket[key]));
                                break;

                            default:
                                break;
                        }
                    }
                    command.Parameters.AddWithValue("@Time", DateTime.Now);
                    command.Parameters.AddWithValue("@Machine", receivedPacket["Machine"].ToString());
                    int rowsAffected = command.ExecuteNonQuery();// execute the command returning number of rows affected
                    DiagnosticOut(rowsAffected + " row(s) inserted");//logit
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))
                {
                    ReastablishSQL(SQLIndexSummary, message);
                }
                DiagnosticOut(ex.ToString());
            }
        }

        /// <summary>
        /// Camstar section of the Fifteen Minut Packet sends a throughput packet for the resource named for the machine.
        /// </summary>
        private void CamstarIndexSummary(string message)
        {
            string DataReceived;
            try
            {
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                StringBuilder PacketStringBuilder = new StringBuilder();
                PacketStringBuilder.Append("<__InSite __version=\"1.1\" __encryption=\"2\"><__session><__connect><user><__name>");
                PacketStringBuilder.Append(CamstarUsername);//username
                PacketStringBuilder.Append("</__name></user><password __encrypted=\"no\">");
                PacketStringBuilder.Append(CamstarPassword);//password
                PacketStringBuilder.Append("</password></__connect><__filter><__allowUntaggedInstances><![CDATA[3]]></__allowUntaggedInstances></__filter></__session><__service __serviceType=\"ResourceThruput\"><__utcOffset><![CDATA[-04:00:00]]></__utcOffset><__inputData><Product><__name><![CDATA[");
                PacketStringBuilder.Append(receivedPacket["Naed"]);//productNaed
                PacketStringBuilder.Append("]]></__name><__useROR><![CDATA[true]]></__useROR></Product><Qty><![CDATA[");
                PacketStringBuilder.Append(receivedPacket["Good"]);//qty
                PacketStringBuilder.Append("]]></Qty><Resource><__name><![CDATA[");
                PacketStringBuilder.Append(receivedPacket["Machine"]);//resource
                PacketStringBuilder.Append("]]></__name></Resource></__inputData><__perform><__eventName><![CDATA[GetWIPMsgs]]></__eventName></__perform><__requestData><CompletionMsg /><WIPMsgMgr><WIPMsgs><AcknowledgementRequired /><MsgAcknowledged /><MsgText /><PasswordRequired /><WIPMsgDetails /></WIPMsgs></WIPMsgMgr></__requestData></__service></__InSite>");
                DataReceived = Sendmessage(QACamstarIP, CamstarPort, PacketStringBuilder.ToString());
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString()); }
        }

        /// <summary>
        /// Summary packet received every fifteen minutes from the plc.
        /// </summary>
        private void IndexSummaryPacket(string message)
        {
            DiagnosticOut("Fifteen Minute Packet Received!");
            Task.Run(() => SQLIndexSummary(message));
            Task.Run(() => CamstarIndexSummary(message));
        }

        /// <summary>
        /// SQL section of the Downtime Packet records downtime to the machine downtime table.
        /// </summary>
        private void SQLDownTimePacket(string message)
        {
            string downtime;
            string SQLString = "";
            try //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append("INSERT INTO " + receivedPacket["Machine"] + "DownTimes (");
                IList<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();//gets list of all keys in json object
                string keySection = "";
                string valueSection = "";
                foreach (string key in keys)//foreach key
                {
                    if (key == "Status" || key == "Time" || key == "NAED" || key == "MReason" || key == "UReason")//except machine as it is used as the table name.
                    {
                        keySection += key + ", ";//Make a key
                        valueSection += "@" + key + ", ";//and value Reference to be replaced later
                    }
                }
                keySection += "MachineID ";
                valueSection += "MachineID ";
                sqlStringBuilder.Append(keySection + ")");
                sqlStringBuilder.Append("SELECT " + valueSection + "from MachineInfoTable" + " where MachineName = @Machine ;");//append both to the command string
                SQLString = sqlStringBuilder.ToString();//convert to string
                using (SqlCommand command = new SqlCommand(SQLString, ENGDBConnection))
                {
                    foreach (string key in keys)//foreach key
                    {
                        switch (key)
                        {
                            case "Status":
                                command.Parameters.AddWithValue("@" + key, Convert.ToInt32(receivedPacket[key]));
                                break;

                            case "Time":
                                downtime = receivedPacket[key].ToString();
                                int year = 2000 + Convert.ToInt32(downtime.Substring(0, 2));
                                int month = Convert.ToInt32(downtime.Substring(3, 2));
                                int day = Convert.ToInt32(downtime.Substring(6, 2));
                                int hour = Convert.ToInt32(downtime.Substring(9, 2));
                                int minute = Convert.ToInt32(downtime.Substring(12, 2));
                                int second = Convert.ToInt32(downtime.Substring(15, 2));
                                command.Parameters.AddWithValue("@" + key, new DateTime(year, month, day, hour, minute, second));
                                break;

                            case "NAED":
                                command.Parameters.AddWithValue("@" + key, receivedPacket[key].ToString());
                                break;

                            case "MReason":
                                command.Parameters.AddWithValue("@" + key, receivedPacket[key].ToString());
                                break;

                            case "UReason":
                                command.Parameters.AddWithValue("@" + key, receivedPacket[key].ToString());
                                break;

                            default:
                                break;
                        }
                    }
                    command.Parameters.AddWithValue("@Machine", receivedPacket["Machine"].ToString());
                    int rowsAffected = command.ExecuteNonQuery();// execute the command returning number of rows affected
                    DiagnosticOut(rowsAffected + " row(s) inserted");//logit
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))
                {
                    ReastablishSQL(SQLDownTimePacket, message);
                }
                DiagnosticOut(ex.ToString());
            }
        }

        /// <summary>
        /// Camstar section of the Downtime Packet sends a downtime packet to camstar.
        /// </summary>
        private void CamstarDowntimePacket(string message)
        {
            string DataReceived;
            try
            {
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                StringBuilder PacketStringBuilder = new StringBuilder();
                PacketStringBuilder.Append("<__InSite __version=\"1.1\" __encryption=\"2\"><__session><__connect><user><__name>");
                PacketStringBuilder.Append(CamstarUsername);//username
                PacketStringBuilder.Append("</__name></user><password __encrypted=\"no\">");
                PacketStringBuilder.Append(CamstarPassword);//password
                PacketStringBuilder.Append("</password></__connect><__filter><__allowUntaggedInstances><![CDATA[3]]></__allowUntaggedInstances></__filter></__session><__service __serviceType=\"ResourceThruput\"><__utcOffset><![CDATA[-04:00:00]]></__utcOffset><__inputData><Product><__name><![CDATA[");
                PacketStringBuilder.Append(receivedPacket["Naed"]);//productNaed
                PacketStringBuilder.Append("]]></__name><__useROR><![CDATA[true]]></__useROR></Product><Qty><![CDATA[");
                PacketStringBuilder.Append(receivedPacket["Good"]);//qty
                PacketStringBuilder.Append("]]></Qty><Resource><__name><![CDATA[");
                PacketStringBuilder.Append(receivedPacket["Machine"]);//resource
                PacketStringBuilder.Append("]]></__name></Resource></__inputData><__perform><__eventName><![CDATA[GetWIPMsgs]]></__eventName></__perform><__requestData><CompletionMsg /><WIPMsgMgr><WIPMsgs><AcknowledgementRequired /><MsgAcknowledged /><MsgText /><PasswordRequired /><WIPMsgDetails /></WIPMsgs></WIPMsgMgr></__requestData></__service></__InSite>");
                DataReceived = Sendmessage(QACamstarIP, CamstarPort, PacketStringBuilder.ToString());
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString()); }
        }

        /// <summary>
        ///  Packet sent each time there is a Downtime received from SNP
        /// </summary>
        private void DowntimePacket(string message)
        {
            DiagnosticOut("DownTime Packet Received!");
            Task.Run(() => SQLDownTimePacket(message));//dont care about return.
            Task.Run(() => CamstarDowntimePacket(message));//dont care about return.
        }

        /// <summary>
        ///  Packet sent at each index
        /// </summary>
        private void ShortTimeStatisticPacket(string message)
        {
            DiagnosticOut("Short Time Statistic Packet Received!");
            Task.Run(() => SQLShortTimeStatisticPacket(message));
            Task.Run(() => MDEShortTimeStatisticPacket(message));
        }

        /// <summary>
        ///  Packet sent at each index to sql
        /// </summary>
        private void SQLShortTimeStatisticPacket(string message)
        {
            string SQLString = "";
            try //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append("INSERT INTO " + receivedPacket["Machine"].ToString() + "ShortTimeStatistics (");
                IList<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();//gets list of all keys in json object
                string keySection = "[";
                string valueSection = "";
                foreach (string key in keys)//foreach key
                {
                    if (key != "Machine" && key != "Theo")//except machine as it is used as the table name.
                    {
                        keySection += key + "], [";//Make a key
                        valueSection += "@" + key + ", ";//and value Reference to be replaced later
                    }
                }
                keySection += "MachineID] ";
                valueSection += "MachineID ";
                sqlStringBuilder.Append(keySection + ")");
                sqlStringBuilder.Append("SELECT " + valueSection + "from MachineInfoTable" + " where MachineName = @Machine ;");//append both to the command string
                SQLString = sqlStringBuilder.ToString();//convert to string
                using (SqlCommand command = new SqlCommand(SQLString, ENGDBConnection))
                {
                    foreach (string key in keys)//foreach key
                    {
                        if (key != "Machine" && key != "Theo")
                            if (key != "HeadNumber")
                            {
                                command.Parameters.AddWithValue("@" + key, 1 == Convert.ToInt32(receivedPacket[key]));
                            }
                            else
                                command.Parameters.AddWithValue("@" + key, Convert.ToInt32(receivedPacket[key]));
                    }
                    command.Parameters.AddWithValue("@Machine", receivedPacket["Machine"].ToString());
                    int rowsAffected = command.ExecuteNonQuery();// execute the command returning number of rows affected
                    DiagnosticOut(rowsAffected + " row(s) inserted");//logit
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))
                {
                    ReastablishSQL(SQLShortTimeStatisticPacket, message);
                }
                DiagnosticOut(ex.ToString());
            }
        }

        /// <summary>
        ///  Packet sent at each index to MDE over UDP
        /// </summary>
        private void MDEShortTimeStatisticPacket(string message)
        {
            try
            {
                List<byte> bytesToSend = new List<byte>();
                List<bool> bits = new List<bool>();
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                IList<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();//gets list of all keys in json object
                foreach (string key in keys)
                {
                    if (key != "Machine" && key != "Theo" && key != "HeadNumber")
                        bits.Add(Convert.ToInt32(receivedPacket[key] ?? '0') == 1); // if the key's value is null set bit to false, otherwise set it to the bit.
                }
                bytesToSend.Add((byte)'~');
                for (int Index = 0; Index < bits.Count; Index += 8)
                {
                    bool[] Bools;
                    if (bits.Count - Index >= 8)
                    {
                        Bools = new bool[8];
                        Array.Copy(bits.ToArray(), Index, Bools, 0, 8);
                    }
                    else
                    {
                        Bools = new bool[bits.Count - Index];
                        Array.Copy(bits.ToArray(), Index, Bools, 0, bits.Count - Index);
                    }
                    bytesToSend.Add(ConvertBoolArrayToByteLeftJustified(Bools));
                }
                string Theo = Convert.ToString(receivedPacket["Theo"]);
                bytesToSend.Add((byte)Convert.ToInt32(receivedPacket["HeadNumber"]));
                for (int x = 0; x < Theo.Length; x++)
                {
                    bytesToSend.Add((byte)Theo[x]);
                }
                bytesToSend.Add((byte)10);//new line end character
                MDEClient.Send(bytesToSend.ToArray(), bytesToSend.Count, MDEIP, MDEClientPort);
            }
            catch (Exception ex)
            {
                DiagnosticOut(ex.ToString());
            }
        }

        /// <summary>
        /// Collection of MQTT Connection setup
        /// </summary>
        private void MQTTConnections()
        {
            try
            {
                DiagnosticOut("Connecting MainSubscriber");
                MainInputSubsriber = new TopicSubscriber(SubTopicName, Broker, ClientID, ConsumerID);
                MainInputSubsriber.OnMessageReceived += new MessageReceivedDelegate(MainInputSubsriber_OnmessageReceived);
                ThingsToDispose.Add(new Disposable(nameof(MainInputSubsriber), MainInputSubsriber));//add to reference pile so it disposes of itself properly.
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString()); }
            try
            {
                DiagnosticOut("Connecting TestPublisher");
                MainPublisher = new TopicPublisher(TestTopicName, Broker);
                ThingsToDispose.Add(new Disposable(nameof(MainPublisher), MainPublisher));
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString()); }
        }

        /// <summary>
        /// Collection of SQL Connection setup
        /// </summary>
        private void SQLConnections()
        {
            try
            {
                // Build connection string
                DiagnosticOut("Connecting SQL Database");
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                builder.DataSource = QAENG_DBDataSource;
                builder.UserID = ENG_DBUserID;
                builder.Password = ENG_DBPassword;
                builder.InitialCatalog = ENG_DBInitialCatalog;
                // Connect to SQL
                Console.Write("Connecting to SQL Server ... ");
                ENGDBConnection = new SqlConnection(builder.ConnectionString);
                ENGDBConnection.Open();
                ThingsToDispose.Add(new Disposable(nameof(ENGDBConnection), ENGDBConnection));
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString()); }
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
            DiagnosticOut("Connecting to MDE");
            try
            {
                MDEClient = new UdpClient(MDEOutPort);
                ThingsToDispose.Add(new Disposable(nameof(MDEClient), MDEClient));
            }
            catch (Exception ex) { DiagnosticOut(ex.ToString()); }
        }

        /// <summary>
        /// Call On stop of service
        /// </summary>
        private void CallOnStop()
        {
            try
            {
                MDEClient.Close();//close UDP connections to.
            }
            catch
            {
            }
            foreach (Disposable disposable in ThingsToDispose)
            {
                try
                {
                    disposable.Dispose();//dispose of connections on stop they will be reestablished on start.
                    DiagnosticOut(disposable.Name + "Has been Disconected and Disposed");
                }
                catch (Exception ex) { DiagnosticOut(disposable.Name + ex.ToString()); }
            }
        }

        /// <summary>
        /// Call On start of service
        /// </summary>
        private void CallOnStart()
        {
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
                DiagnosticOut(ex.ToString());
            }
        }

        /// <summary>
        /// Convert up to a byte to 8 bools
        /// </summary>
        private bool[] ConvertByteToBoolArray(byte b)
        {
            bool[] result = new bool[8];
            // check each bit in the byte. if 1 set to true, if 0 set to false
            for (int i = 0; i < 8; i++)
                result[i] = (b & (1 << i)) == 0 ? false : true;
            // reverse the array
            Array.Reverse(result);
            return result;
        }

        /// <summary>
        /// Convert up to 8 bools to 1 byte right justfied 0001111
        /// </summary>
        private byte ConvertBoolArrayToByteRightJustified(bool[] source)
        {
            byte result = 0;
            // This assumes the array never contains more than 8 elements!
            int index = 8 - source.Length;
            // Loop through the array
            foreach (bool b in source)
            {
                // if the element is 'true' set the bit at that position
                if (b)
                    result |= (byte)(1 << (7 - index));
                index++;
            }
            return result;
        }

        /// <summary>
        /// Convert up to 8 bools to 1 byte right justfied 11110000
        /// </summary>
        private byte ConvertBoolArrayToByteLeftJustified(bool[] source)
        {
            byte result = 0;
            int index = 0;
            foreach (bool b in source)
            {
                // if the element is 'true' set the bit at that position
                if (b)
                    result |= (byte)(1 << (7 - index));
                index++;
            }
            return result;
        }

        /// <summary>
        /// Send message To Camstar and listen for a message back.
        /// </summary>
        private string Sendmessage(string host, int port, string content)
        {
            var connection = new ServerConnection();
            try
            {
                var connected = connection.Connect(host, port); // try connecting
                if (!connected) return ""; // return nothing if cant connect
                connection.Send(content); // send data
                connection.Receive(out var result); // reviece message from server, and store into variable
                connection.Disconnect(); // Close connection
                string receivemessage;
                try
                {
                    receivemessage = XDocument.Parse(result).ToString(); // format recieved message into xml
                }
                catch
                {
                    receivemessage = result;
                }
                return receivemessage;
            }
            catch (Exception ex) // If an error occurred return null string
            {
                DiagnosticOut(ex.ToString());
                return "";
            }
        }
    }
}