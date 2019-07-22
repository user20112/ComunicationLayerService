using Camstar.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace VisualVersionofService
{
    internal class SNPPackets
    {
        #region Variable Section

        private Form1 MainForm;
        public TopicPublisher Publisher;//publishes to the Pac-Light Outbound topic
        public UdpClient MDEClient;
        public string TopicName = "SNP.Outbound";
        private string CamstarUsername;
        private string CamstarPassword;
        private string CamstarIP;
        private string MDEIP;//currently my ip for MDEing. once it is known to be working i have to get this ip from gerry.
        private int CamstarPort;
        private int MDEClientPort;
        public int MDEOutPort;

        public SNPPackets(Form1 mainform)
        {
            MainForm = mainform;
            CamstarUsername = ConfigurationManager.AppSettings["CamstarUsername"];
            CamstarPassword = ConfigurationManager.AppSettings["CamstarPassword"];
            CamstarIP = ConfigurationManager.AppSettings["CamstarIP"];
            MDEIP = ConfigurationManager.AppSettings["MDEIP"];
            CamstarPort = Convert.ToInt32(ConfigurationManager.AppSettings["CamstarPort"]);
            MDEClientPort = Convert.ToInt32(ConfigurationManager.AppSettings["MDEClientPort"]);
            MDEOutPort = Convert.ToInt32(ConfigurationManager.AppSettings["MDEOutPort"]);
        }

        #endregion Variable Section

        #region Packet Section

        /// <summary>
        /// Called whenever a new machine is detected
        /// </summary>
        public void NewMachinePacket(string message)
        {
            string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
            JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
            string machineName = receivedPacket["Machine"].ToString();
            string Line = receivedPacket["Line"].ToString();
            string Theo = receivedPacket["Theo"].ToString();
            int snp_ID = Convert.ToInt32((byte)message[2]);
            string Errors = "[";
            try
            {
                string ErrorString = receivedPacket["Errors"].ToString();
                string[] ErrorArray = ErrorString.Split(',');
                foreach (string error in ErrorArray)//foreach error add it to the Errors Section
                {
                    Errors += error + "] [bit] NOT NULL, [";
                }
                Errors = Errors.Substring(0, Errors.Length - 1);
            }
            catch//no errors are being recorded
            {
                Errors = "";
            }
            try //try loop in case command fails.
            {
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append(" USE [Pac-LiteDb ] ");
                sqlStringBuilder.Append(" CREATE TABLE [dbo].[" + machineName + "ShortTimeStatistics](");
                sqlStringBuilder.Append("	[MachineID] [int] NULL, [Good] [bit] NULL, [Bad] [bit] NULL, [Empty] [bit] NULL, [Attempt] [bit] NULL, " + Errors + " [Other] [bit] NULL, [HeadNumber] [int] NULL ");
                sqlStringBuilder.Append(" ) ON [PRIMARY] ");
                sqlStringBuilder.Append(" CREATE TABLE [dbo].[" + machineName + "](");
                sqlStringBuilder.Append(" 	[EntryID] [int] IDENTITY(1,1) NOT NULL,	[MachineID] [int] NULL,	[Good] [int] NULL,	[Bad] [int] NULL,	[Empty] [int] NULL,	[Indexes] [int] NULL,	[NAED] [varchar](20) NULL,	[UOM] [varchar](10) NULL,	[Time] [datetime] NULL) ON [PRIMARY] ");
                sqlStringBuilder.Append(" CREATE TABLE [dbo].[" + machineName + "DownTimes](");
                sqlStringBuilder.Append(" 	[Time] [datetime] NULL,	[MReason] [varchar](255) NULL,	[UReason] [varchar](255) NULL,	[NAED] [varchar](20) NULL,	[MachineID] [int] NULL,	[Status] [int] NULL) ON [PRIMARY]; ");
                sqlStringBuilder.Append(" insert into MachineInfoTable (MachineName, Line, SNPID , Theo) values( @machine , @Line , @SNPID , @Theo);");
                string SQLString = sqlStringBuilder.ToString();//convert to string
                using (SqlCommand command = new SqlCommand(SQLString, MainForm.ENGDBConnection))
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
                    MainForm.ReastablishSQL(SQLShortTimeStatisticPacket, message);
                }
                MainForm.DiagnosticOut(ex.ToString(), 1);
            }
        }

        /// <summary>
        /// Updates an existing machine based of machine name.
        /// </summary>
        public void EditMachinePacket(string message)
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
                using (SqlCommand command = new SqlCommand(SQLString, MainForm.ENGDBConnection))
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
                    MainForm.ReastablishSQL(SQLShortTimeStatisticPacket, message);
                }
                MainForm.DiagnosticOut(ex.ToString(), 1);
            }
        }

        /// <summary>
        /// Deletes existing machine
        /// </summary>
        public void DeleteMachinePacket(string message)
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
                using (SqlCommand command = new SqlCommand(SQLString, MainForm.ENGDBConnection))
                {
                    command.Parameters.AddWithValue("@machine", receivedPacket["Machine"].ToString());
                    command.ExecuteNonQuery();// execute the command returning number of rows affected
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))
                {
                    MainForm.ReastablishSQL(SQLShortTimeStatisticPacket, message);
                }
                MainForm.DiagnosticOut(ex.ToString(), 1);
            }
        }

        /// <summary>
        /// Summary packet received every fifteen minutes from the plc.
        /// </summary>
        public void IndexSummaryPacket(string message)
        {
            MainForm.DiagnosticOut("Fifteen Minute Packet Received!", 3);
            Task.Run(() => SQLIndexSummary(message));
            Task.Run(() => CamstarIndexSummary(message));
        }

        /// <summary>
        ///  Packet sent each time there is a Downtime received from SNP
        /// </summary>
        public void DowntimePacket(string message)
        {
            MainForm.DiagnosticOut("DownTime Packet Received!", 3);
            Task.Run(() => SQLDownTimePacket(message));//dont care about return.
            Task.Run(() => CamstarDowntimePacket(message));//dont care about return.
        }

        /// <summary>
        ///  Packet sent at each index
        /// </summary>
        public void ShortTimeStatisticPacket(string message)
        {
            MainForm.DiagnosticOut("Short Time Statistic Packet Received!", 3);
            Task.Run(() => SQLShortTimeStatisticPacket(message));
            Task.Run(() => MDEShortTimeStatisticPacket(message));
        }

        /// <summary>
        ///
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
                using (SqlCommand command = new SqlCommand(SQLString, MainForm.ENGDBConnection))
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
                    MainForm.DiagnosticOut(rowsAffected + " row(s) inserted", 2);//logit
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))
                {
                    MainForm.ReastablishSQL(SQLIndexSummary, message);
                }
                MainForm.DiagnosticOut(ex.ToString(), 1);
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
                /*
                <?xml version=\"1.0\" encoding=\"utf-16\"?><__InSite __version=\"1.1\" __encryption=\"2\"><__session><__connect><user>
                <__name>username</__name>
                </user><password __encrypted=\"no\">Password</password>
                </__connect><__filter><__allowUntaggedInstances><![CDATA[3]]></__allowUntaggedInstances></__filter></__session><__service __serviceType=\"ResourceThruput\"><__utcOffset><![CDATA[-04:00:00]]></__utcOffset><__inputData><MfgOrder>
                <__name><![CDATA[MfgOrder]]></__name></MfgOrder><Product>
                <__name><![CDATA[ProductNaed]]></__name><__useROR><![CDATA[true]]></__useROR></Product>
                <Qty><![CDATA[QTY]]></Qty><Resource>
                <__name><![CDATA[RESOURCE]]></__name>
                </Resource><ResourceGroup><__name><![CDATA[]]></__name></ResourceGroup><UOM><__name><![CDATA[UnitOfMesure]]>
                </__name></UOM></__inputData><__perform><__eventName><![CDATA[GetWIPMsgs]]></__eventName></__perform><__requestData><CompletionMsg /><WIPMsgMgr><WIPMsgs><AcknowledgementRequired /><MsgAcknowledged /><MsgText /><PasswordRequired /><WIPMsgDetails /></WIPMsgs></WIPMsgMgr></__requestData></__service></__InSite>
                */
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                StringBuilder PacketStringBuilder = new StringBuilder();
                PacketStringBuilder.Append("<?xml version=\"1.0\" encoding=\"utf-16\"?><__InSite __version=\"1.1\" __encryption=\"2\"><__session><__connect><user><__name>" + CamstarUsername + "</__name></user><password __encrypted=\"yes\">" + Camstar.Util.CryptUtil.Encrypt(CamstarPassword) + "</password></__connect>");
                PacketStringBuilder.Append("<__service __serviceType=\"ResourceThruput\"><__utcOffset><![CDATA[-04:00:00]]></__utcOffset><__inputData><MfgOrder>");
                PacketStringBuilder.Append("<__name><![CDATA[]]></__name></MfgOrder><Product>");
                PacketStringBuilder.Append("< __name >< ![CDATA[" + receivedPacket["Naed"] + "]] ></ __name >< __useROR >< ![CDATA[true]] ></ __useROR ></ Product > ");//productNaed
                PacketStringBuilder.Append("< Qty >< ![CDATA[" + receivedPacket["Good"] + "]] ></ Qty >< Resource >");
                PacketStringBuilder.Append("< __name >< ![CDATA[" + receivedPacket["Machine"] + "]] ></ __name >");//qty
                PacketStringBuilder.Append("</Resource><ResourceGroup><__name><![CDATA[]]></__name></ResourceGroup><UOM><__name><![CDATA[EA]]>");
                PacketStringBuilder.Append("</__name></UOM></__inputData><__perform><__eventName><![CDATA[GetWIPMsgs]]></__eventName></__perform><__requestData><CompletionMsg /><WIPMsgMgr><WIPMsgs><AcknowledgementRequired /><MsgAcknowledged /><MsgText /><PasswordRequired /><WIPMsgDetails /></WIPMsgs></WIPMsgMgr></__requestData></__service></__InSite>");//resource
                DataReceived = Sendmessage(CamstarIP, CamstarPort, PacketStringBuilder.ToString());
            }
            catch (Exception ex) { MainForm.DiagnosticOut(ex.ToString(), 2); }
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
                using (SqlCommand command = new SqlCommand(SQLString, MainForm.ENGDBConnection))
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
                    MainForm.DiagnosticOut(rowsAffected + " row(s) inserted", 2);//logit
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))
                {
                    MainForm.ReastablishSQL(SQLDownTimePacket, message);
                }
                MainForm.DiagnosticOut(ex.ToString(), 1);
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
                /*
            <__service __serviceType=\"ResourceSetupTransition\"><__utcOffset><![CDATA[-04:00:00]]></__utcOffset><__inputData><Availability><![CDATA[1]]></Availability><Resource>
            <__name><![CDATA[zzzCIO_1Loader]]></__name>
            </Resource><ResourceGroup><__name><![CDATA[CIO_1ResourceGrp]]></__name></ResourceGroup><ResourceStatusCode>
            <__name><![CDATA[Alarm]]></__name>
            </ResourceStatusCode><ResourceStatusReason><__name><![CDATA[2]]></__name>
            </ResourceStatusReason></__inputData><__execute /><__requestData><CompletionMsg /><ACEMessage /><ACEStatus /></__requestData></__service></__InSite>
*/
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                StringBuilder PacketStringBuilder = new StringBuilder();
                PacketStringBuilder.Append("<?xml version=\"1.0\" encoding=\"utf-16\"?><__InSite __version=\"1.1\" __encryption=\"2\"><__session><__connect><user><__name>" + CamstarUsername + "</__name></user><password __encrypted=\"yes\">" + Camstar.Util.CryptUtil.Encrypt(CamstarPassword) + "</password></__connect></__session>");
                PacketStringBuilder.Append("<__service __serviceType=\"ResourceSetupTransition\"><__utcOffset><![CDATA[-04:00:00]]></__utcOffset><__inputData><Availability><![CDATA[1]]></Availability><Resource>");
                PacketStringBuilder.Append("<__name><![CDATA[" + receivedPacket["Machine"] + "]]></__name>");
                PacketStringBuilder.Append("</Resource><ResourceGroup><__name><![CDATA[CIO_1ResourceGrp]]></__name></ResourceGroup><ResourceStatusCode>");
                PacketStringBuilder.Append("<__name><![CDATA[Alarm]]></__name>");//Reason
                PacketStringBuilder.Append("</ResourceStatusCode><ResourceStatusReason><__name><![CDATA[2]]></__name>");//resource status code
                PacketStringBuilder.Append("</ResourceStatusReason></__inputData ><__execute /><__requestData ><CompletionMsg /><ACEMessage /><ACEStatus /></__requestData ></__service ></__InSite > ");
                DataReceived = Sendmessage(CamstarIP, CamstarPort, PacketStringBuilder.ToString());
            }
            catch (Exception ex) { MainForm.DiagnosticOut(ex.ToString(), 2); }
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
                using (SqlCommand command = new SqlCommand(SQLString, MainForm.ENGDBConnection))
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
                    MainForm.DiagnosticOut(rowsAffected + " row(s) inserted", 2);//logit
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))
                {
                    MainForm.ReastablishSQL(SQLShortTimeStatisticPacket, message);
                }
                MainForm.DiagnosticOut(ex.ToString(), 1);
            }
        }

        /// <summary>
        ///  Packet sent at each index to MDE over UDP
        /// </summary>
        private void MDEShortTimeStatisticPacket(string message)
        {
            try
            {
                List<byte> bySNPoSend = new List<byte>();
                List<bool> bits = new List<bool>();
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                IList<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();//gets list of all keys in json object
                foreach (string key in keys)
                {
                    if (key != "Machine" && key != "Theo" && key != "HeadNumber")
                        bits.Add(Convert.ToInt32(receivedPacket[key] ?? '0') == 1); // if the key's value is null set bit to false, otherwise set it to the bit.
                }
                bySNPoSend.Add((byte)'~');
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
                    bySNPoSend.Add(ConvertBoolArrayToByteLeftJustified(Bools));
                }
                string Theo = Convert.ToString(receivedPacket["Theo"]);
                bySNPoSend.Add((byte)Convert.ToInt32(receivedPacket["HeadNumber"]));
                for (int x = 0; x < Theo.Length; x++)
                {
                    bySNPoSend.Add((byte)Theo[x]);
                }
                bySNPoSend.Add((byte)10);//new line end character
                MDEClient.Send(bySNPoSend.ToArray(), bySNPoSend.Count, MDEIP, MDEClientPort);
            }
            catch (Exception ex)
            {
                MainForm.DiagnosticOut(ex.ToString(), 1);
            }
        }

        #endregion Packet Section

        #region Connections/Resources/Misc

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
                MainForm.DiagnosticOut(ex.ToString(), 1);
                return "";
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

        #endregion Connections/Resources/Misc
    }
}