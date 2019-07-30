﻿using Camstar.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SNPService
{
    internal class SNPPackets
    {
        #region Variable Section

        private SNPService Controller;                                                  // Contains either the service or form that owns this class.
        //public TopicPublisher Publisher;                                                    // publishes to the Pac-Light Outbound topic

        //public UdpClient MDEClient;                                                         // depreciated comunication to MDE over udp
        //public string TopicName = "SNP.Outbound";                                           // Test Output topic
        private string CamstarUsername;                                                     // username used to comunicate with camstar

        private string CamstarPassword;                                                     // password used to ocmunicate with camstar
        private string CamstarIP;                                                           // IP of the Camstar System you are talking to

        //private string MDEIP;                                                               // currently my ip for MDEing. once it is known to be working i have to get this ip from gerry.
        private int CamstarPort;                                                            // Port of the Camstar system you are talking to

        //private int MDEClientPort;                                                          // depreciated used to comunicate to MDE over UDP ( the receiveing port of MDE
        //public int MDEOutPort;                                                              // depreciated used to comunicate to MDE over UDP ( the sending port to MDE

        public SNPPackets(SNPService controller)
        {
            Controller = controller;                                                        //set the owner of this class
            CamstarUsername = ConfigurationManager.AppSettings["CamstarUsername"];          // pull Camstar username
            CamstarPassword = ConfigurationManager.AppSettings["CamstarPassword"];          //and camstar password
            CamstarIP = ConfigurationManager.AppSettings["CamstarIP"];                      //and camstar ip
            //MDEIP = ConfigurationManager.AppSettings["MDEIP"];                              //and MDE Ip deprecieted as MDE is no longer used
            CamstarPort = Convert.ToInt32(ConfigurationManager.AppSettings["CamstarPort"]); //and Camstar Port
            //MDEClientPort = Convert.ToInt32(ConfigurationManager.AppSettings["MDEClientPort"]);//and MDE Ports deprecieted as mde is no longer used
            //MDEOutPort = Convert.ToInt32(ConfigurationManager.AppSettings["MDEOutPort"]); //from app.config
        }

        #endregion Variable Section

        #region Packet Section

        /// <summary>
        /// Called whenever a new machine is detected
        /// Creates all required databases and entries for the machine detaield in the packet.
        /// </summary>
        public void NewMachinePacket(string message)
        {
            try                                                                             //try loop in case command fails.
            {
                Controller.DiagnosticOut("New Machine Packet!", 2);                             // log the packet as have been received
                string jsonString = message.Substring(7, message.Length - 7);                   //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;  //convert the json to an object
                string machineName = receivedPacket["Machine"].ToString();                      //get the important sections of the packet out ( that arent errors)
                string Line = receivedPacket["Line"].ToString();
                string Theo = receivedPacket["Theo"].ToString();
                int snp_ID = Convert.ToInt32((byte)message[2]);                                 //get the SNP ID from the message header
                string Plant = receivedPacket["Plant"].ToString();
                string Engineer = receivedPacket["Engineer"].ToString();
                string Errors = "";                                                             //Start Errors as blank
                try                                                                             //If this fails break dont break the application
                {
                    string ErrorString = receivedPacket["Errors"].ToString();                   //Get all errors passed in
                    if (ErrorString.Length > 0)
                    {
                        string[] ErrorArray = ErrorString.Split(',');                               // break up csv errors
                        foreach (string error in ErrorArray)                                        //foreach error add it to the Errors Section
                        {
                            Errors += "[" + error + "] [bit] NOT NULL DEFAULT 0, ";                               //set the feild type to bit and allow it to be nullable
                        }
                        Errors = Errors.Substring(0, Errors.Length - 2);
                    }                        //remove extra comma and space
                }
                catch (Exception ex)                                                                          //if this fails set it to defualt no errors
                {
                    Controller.DiagnosticOut(ex.ToString(), 1);
                    Errors = "";
                }
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append(" USE [" + ConfigurationManager.AppSettings["ENGDBDatabase"] + " ] ");//load the MachineInfoEntry
                sqlStringBuilder.Append(" insert into MachineInfoTable (MachineName, Line, SNPID , Theo,Plant , Engineer) values( @machine , @Line , @SNPID , @Theo, @Plant , @Engineer);");
                string SQLString = sqlStringBuilder.ToString();                                    //Convert the builder to the string
                using (SqlCommand command = new SqlCommand(SQLString, Controller.ENGDBConnection))
                {                                                                           //Commmand Time!
                    command.Parameters.AddWithValue("@machine", machineName);               //replace all parameters with their respective values
                    command.Parameters.AddWithValue("@Line", Line);
                    command.Parameters.AddWithValue("@SNPID", snp_ID);
                    command.Parameters.AddWithValue("@Theo", Theo);
                    command.Parameters.AddWithValue("@Plant", Plant);
                    command.Parameters.AddWithValue("@Engineer", Engineer);
                    int rowsAffected = command.ExecuteNonQuery();                           //execute the command returning number of rows affected
                    Controller.DiagnosticOut(rowsAffected + " row(s) inserted", 2);         //logit
                }
                bool Missing = CheckForDatabase(Line);

                sqlStringBuilder = new StringBuilder();                                     //this builder will build the SQL String
                if (Missing)                                                                //if we dont have a database yet
                {
                    sqlStringBuilder.Append(" CREATE DATABASE [" + Line + "];");            //create one
                    SQLString = sqlStringBuilder.ToString();                                    //Convert the builder to the string
                    using (SqlCommand command = new SqlCommand(SQLString, Controller.ENGDBConnection))
                    {                                                                           //Commmand Time!
                        int rowsAffected = command.ExecuteNonQuery();                           //execute the command returning number of rows affected
                        Controller.DiagnosticOut(rowsAffected + " databases created", 2);         //logit
                    }
                }
                sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append(" USE [" + Line + "] ");                            //Load the create tables with defualt table information using MachineName as the resource name and line as the database name
                sqlStringBuilder.Append(" CREATE TABLE [dbo].[" + machineName + "ShortTimeStatistics](");
                sqlStringBuilder.Append("	[MachineID] [int] NOT NULL,Timestamp [datetime2] NOT NULL, [Good] [bit] NOT NULL, [Bad] [bit] NOT NULL, [Empty] [bit] NOT NULL, [Attempt] [bit] NOT NULL, [Other] [bit] NOT NULL, [HeadNumber] [int] NOT NULL," + Errors);
                sqlStringBuilder.Append(" ) ON [PRIMARY] ");
                sqlStringBuilder.Append(" CREATE TABLE [dbo].[" + machineName + "](");
                sqlStringBuilder.Append(" 	[EntryID] [int] IDENTITY(1,1) NOT NULL,	[MachineID] [int] NULL,	[Good] [int] NULL,	[Bad] [int] NULL,	[Empty] [int] NULL,	[Indexes] [int] NULL,	[NAED] [varchar](20) NULL,	[UOM] [varchar](10) NULL,	[Timestamp] [datetime2] NULL) ON [PRIMARY] ");
                sqlStringBuilder.Append(" CREATE TABLE [dbo].[" + machineName + "DownTimes](");
                sqlStringBuilder.Append(" 	[Timestamp] [datetime2] NULL,	[MReason] [varchar](255) NULL,	[UReason] [varchar](255) NULL,	[NAED] [varchar](20) NULL,	[MachineID] [int] NULL,	[StatusCode] [nvarchar](30) NULL,	[Code] [int] NULL) ON [PRIMARY]; ");
                SQLString = sqlStringBuilder.ToString();                                    //Convert the builder to the string
                using (SqlCommand command = new SqlCommand(SQLString, Controller.ENGDBConnection))
                {                                                                           //Commmand Time!
                    int rowsAffected = command.ExecuteNonQuery();                           //execute the command returning number of rows affected
                    Controller.DiagnosticOut(rowsAffected + " row(s) inserted", 2);         //logit
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if the connection crashed
                {
                    Controller.ReastablishSQL(NewMachinePacket, message);                   //reastablish it
                }
                Controller.DiagnosticOut(ex.ToString(), 1);                                 //if not handled log it and move on
            }
        }

        /// <summary>
        /// Updates an existing machine based of machine name. addeds errors as new columns as well.
        /// </summary>
        public void EditMachinePacket(string message)
        {
            try                                                                             //try loop in case command fails.
            {
                Controller.DiagnosticOut("Edit Machine Packet!", 2);                            // log the packet as have been received
                string jsonString = message.Substring(7, message.Length - 7);                   //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;  //Convert it into a jobject
                string machineName = receivedPacket["Machine"].ToString();                      //gather important sections into variables
                string Line = receivedPacket["Line"].ToString();
                string Theo = receivedPacket["Theo"].ToString();
                string Engineer = receivedPacket["Engineer"].ToString();
                int snp_ID = Convert.ToInt32((byte)message[2]);                                 //get snp id from message header

                StringBuilder sqlStringBuilder = new StringBuilder();                       //string builder to build the sql
                sqlStringBuilder.Append(" USE [" + ConfigurationManager.AppSettings["ENGDBDatabase"] + " ] ");//load the edit command using Machine as the resource name
                sqlStringBuilder.Append(" update MachineInfoTable set Line = @Line, SNPID = @SNPID , Theo = @Theo, Engineer = @Engineer  where MachineName = @machine;");
                string SQLString = sqlStringBuilder.ToString();                             //convert the builder to a string
                using (SqlCommand command = new SqlCommand(SQLString, Controller.ENGDBConnection))
                {                                                                           //command time!
                    command.Parameters.AddWithValue("@machine", machineName);               //replace all parameters with values
                    command.Parameters.AddWithValue("@Line", Line);
                    command.Parameters.AddWithValue("@SNPID", snp_ID);
                    command.Parameters.AddWithValue("@Theo", Theo);
                    command.Parameters.AddWithValue("@Engineer", Engineer);
                    int rowsAffected = command.ExecuteNonQuery();                           // execute the command returning number of rows affected
                    Controller.DiagnosticOut(rowsAffected + " row(s) inserted", 2);         //logit
                }
                sqlStringBuilder = new StringBuilder();                                     //reset string builder for next command
                sqlStringBuilder.Append(" USE [" + Line + "] ");                             //load alter table command
                sqlStringBuilder.Append("Alter Table [" + receivedPacket["Machine"] + "ShortTimeStatistics] ADD ");
                string ErrorString = receivedPacket["Errors"].ToString();                   //grab all errors passed in
                string[] ErrorArray = ErrorString.Split(',');                               //divide the csv of errors
                string Errors = "";                                                         //this string is added to the sql
                foreach (string error in ErrorArray)                                        //foreach error add it to the Errors Section
                {
                    Errors += ("[" + error + "] [bit] NOT NULL DEFAULT 0, ");                             //column is a bit feild that is nullable
                }
                Errors = Errors.Substring(0, Errors.Length - 2);                            //remove extra space and comma
                sqlStringBuilder.Append(Errors + ";");                                      //append a semicolon
                SQLString = sqlStringBuilder.ToString();                                    //Convert builder to string
                using (SqlCommand command = new SqlCommand(SQLString, Controller.ENGDBConnection))
                {                                                                           //Comand Time Again!
                    int rowsAffected = command.ExecuteNonQuery();                           // execute the command returning number of rows affected
                    Controller.DiagnosticOut(rowsAffected + " row(s) inserted", 2);         //logit
                }
            }
            catch (Exception ex)                                                            //catch exceptions
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if the connection crashed
                {
                    Controller.ReastablishSQL(EditMachinePacket, message);                  //reestablish connection
                }
                Controller.DiagnosticOut(ex.ToString(), 1);                                 //else logit and move on
            }
        }

        /// <summary>
        /// Deletes existing machine ( deletes the Machine Info entry and all tables asociated with it
        /// </summary>
        public void DeleteMachinePacket(string message)
        {
            Controller.DiagnosticOut("Delete Machine Packet!", 2);                          // log the packet as have been received
            string jsonString = message.Substring(7, message.Length - 7);                   //grab json data from the end.
            JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;  //convert it to a Jobject
            string Line = receivedPacket["Line"].ToString();
            string machineName = receivedPacket["Machine"].ToString();                      //get the machine name in a nice easy feild
            try                                                                             //try loop in case command fails.
            {
                StringBuilder sqlStringBuilder = new StringBuilder();                       //string builder for sql command
                sqlStringBuilder.Append(" USE [" + ConfigurationManager.AppSettings["ENGDBDatabase"] + " ] ");//load sql command and edit for machine name as the resource name
                sqlStringBuilder.Append(" delete from MachineInfoTable where MachineName = @machine;");//drop the reference
                string SQLString = sqlStringBuilder.ToString();                             //Convert builder to string
                using (SqlCommand command = new SqlCommand(SQLString, Controller.ENGDBConnection))
                {                                                                           //Comand Time!
                    command.Parameters.AddWithValue("@machine", receivedPacket["Machine"].ToString());//replace parameters with values
                    int rowsAffected = command.ExecuteNonQuery();                           // execute the command returning number of rows affected
                    Controller.DiagnosticOut(rowsAffected + " row(s) inserted", 2);         //logit
                }
                sqlStringBuilder = new StringBuilder();                                     //clear string builder
                sqlStringBuilder.Append(" USE [" + Line + "] ");                            //build next section
                sqlStringBuilder.Append("drop table [" + machineName + "];");
                sqlStringBuilder.Append("drop table [" + machineName + "DownTimes];");
                sqlStringBuilder.Append("drop table [" + machineName + "ShortTimeStatistics];");
                SQLString = sqlStringBuilder.ToString();                                    //Convert builder to string
                using (SqlCommand command = new SqlCommand(SQLString, Controller.ENGDBConnection))
                {                                                                           //Comand Time!
                    int rowsAffected = command.ExecuteNonQuery();                           //execute the command returning number of rows affected
                    Controller.DiagnosticOut(rowsAffected + " row(s) inserted", 2);         //logit
                }
            }
            catch (Exception ex)                                                            //catche exceptions
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if the connection crashed
                {
                    Controller.ReastablishSQL(DeleteMachinePacket, message);                //restablish the connection
                }
                Controller.DiagnosticOut(ex.ToString(), 1);                                 //else logit and move on
            }
        }

        /// <summary>
        /// Summary packet received every fifteen minutes from the plc. Send a throughput packet to camstar and record the data in sql
        /// </summary>
        public void IndexSummaryPacket(string message)
        {
            Controller.DiagnosticOut("Fifteen Minute Packet Received!", 3);                 //logit
            Task.Run(() => SQLIndexSummary(message));                                       //save data to sql async, return value doesnt matter
            Task.Run(() => CamstarIndexSummary(message));                                   //send data to Camstar, return value doesnt matter
        }

        /// <summary>
        ///  Packet sent each time there is a Downtime received from SNP record in sql and send a downtime packet to camstar
        /// </summary>
        public void DowntimePacket(string message)
        {
            Controller.DiagnosticOut("DownTime Packet Received!", 3);                       //logit
            Task.Run(() => SQLDownTimePacket(message));                                     //Save data to SQL, return value doesnt matter
            Task.Run(() => CamstarDowntimePacket(message));                                 //send data to Camstar, return value doesnt matter
        }

        /// <summary>
        ///  Packet sent at each index
        /// </summary>
        public void ShortTimeStatisticPacket(string message)
        {
            Controller.DiagnosticOut("Short Time Statistic Packet Received!", 3);           //logit
            Task.Run(() => SQLShortTimeStatisticPacket(message));                           //Save data to sql return value doesnt matter
            //Task.Run(() => MDEShortTimeStatisticPacket(message));                         //Send Message to UDP port for MDE (depreciated but kept for incasei t is used elsewhere
        }

        /// <summary>
        /// SQL section of the Index Summary. saves data to the resource table.
        /// </summary>
        private void SQLIndexSummary(string message)
        {
            try                                                                             //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);               //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;//Convert json to object
                StringBuilder sqlStringBuilder = new StringBuilder();
                string[] temp = GetMachineIDAndLine(receivedPacket["Machine"].ToString());
                sqlStringBuilder.Append(" USE [" + temp[1] + "] ");                            //select database
                sqlStringBuilder.Append("INSERT INTO [" + receivedPacket["Machine"].ToString() + "] (");  //start loading the command into the string
                List<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();//gets list of all keys in json object
                string keySection = "";                                                     //stores the key section of the SQL
                string valueSection = "";                                                   //stores the value section of the SQL
                foreach (string key in keys)                                                //foreach key
                {
                    if (key != "Machine")                                                   //except machine as it uses an ID.
                    {
                        keySection += key + ", ";                                           //Make a key
                        valueSection += "@" + key + ", ";                                   //and value Reference to be replaced later
                    }
                }
                keySection += "Timestamp, ";                                                     //Make a Time key since it is generated server side
                valueSection += "@Timestamp, ";                                                  //and value Reference to be replaced later
                keySection += "MachineID ";                                                 //Add a machineID section
                valueSection += temp[0] + " ";                                               //and value
                sqlStringBuilder.Append(keySection + ")");                                  //cap it of
                sqlStringBuilder.Append("Values ( " + valueSection + ");");                 //append both to the command string
                string SQLString = sqlStringBuilder.ToString();                             //Convert Builder to string
                using (SqlCommand command = new SqlCommand(SQLString, Controller.ENGDBConnection))
                {                                                                           //Comand Time!
                    command.Parameters.AddWithValue("@NAED", receivedPacket["NAED"].ToString());//replace parameters with values
                    command.Parameters.AddWithValue("@Good", Convert.ToInt32(receivedPacket["Good"]));
                    command.Parameters.AddWithValue("@Bad", Convert.ToInt32(receivedPacket["Bad"]));
                    command.Parameters.AddWithValue("@Empty", Convert.ToInt32(receivedPacket["Empty"]));
                    command.Parameters.AddWithValue("@Indexes", Convert.ToInt32(receivedPacket["Indexes"]));
                    command.Parameters.AddWithValue("@UOM", receivedPacket["UOM"].ToString());
                    command.Parameters.AddWithValue("@Timestamp", DateTime.Now);                 //add a timestamp
                    command.Parameters.AddWithValue("@Machine", receivedPacket["Machine"].ToString());//add the machine name
                    int rowsAffected = command.ExecuteNonQuery();                           // execute the command returning number of rows affected
                    Controller.DiagnosticOut(rowsAffected + " row(s) inserted", 2);         //logit
                }
            }
            catch (Exception ex)                                                            //catch exceptions
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if the connection crashed
                {
                    Controller.ReastablishSQL(SQLIndexSummary, message);                    //reastablish it
                }
                Controller.DiagnosticOut(ex.ToString(), 1);                                 //else log the error and move on
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
                PacketStringBuilder.Append("<__InSite __encryption=\"2\" __version=\"1.1\"><__session><__connect><user>");
                PacketStringBuilder.Append("<__name>d.paddock</__name>");
                PacketStringBuilder.Append("</user>");
                PacketStringBuilder.Append("<password __encrypted=\"yes\">d81b896ee9a9697df6334a1df6f7e8286c9866e0eb243f3c4ca9b80fc0e38dd3d8c0ccef78401ff7</password>");
                PacketStringBuilder.Append("</__connect></__session><__service __serviceType=\"ResourceThruput\"><__utcOffset><![CDATA[-04:00:00]]></__utcOffset><__inputData><MfgOrder>");
                PacketStringBuilder.Append("<__name>");
                PacketStringBuilder.Append("<![CDATA[]]>");
                PacketStringBuilder.Append("</__name>");
                PacketStringBuilder.Append("</MfgOrder><Product>");
                PacketStringBuilder.Append("<__name>");
                PacketStringBuilder.Append("<![CDATA[" + receivedPacket["NAED"] + "]]>");
                PacketStringBuilder.Append("</__name>");
                PacketStringBuilder.Append("<__useROR><![CDATA[true]]></__useROR>");
                PacketStringBuilder.Append("</Product><Qty>");
                PacketStringBuilder.Append("<![CDATA[" + receivedPacket["Good"] + "]]>");
                PacketStringBuilder.Append("</Qty><Resource>");
                PacketStringBuilder.Append("<__name>");
                PacketStringBuilder.Append("<![CDATA[" + receivedPacket["Machine"] + "]]>");
                PacketStringBuilder.Append("</__name>");
                PacketStringBuilder.Append("</Resource><ResourceGroup>");
                PacketStringBuilder.Append("<__name>");
                PacketStringBuilder.Append("<![CDATA[]]>");
                PacketStringBuilder.Append("</__name>");
                PacketStringBuilder.Append("</ResourceGroup><UOM>");
                PacketStringBuilder.Append("<__name>");
                PacketStringBuilder.Append("<![CDATA[EA]]>");
                PacketStringBuilder.Append("</__name>");
                PacketStringBuilder.Append("</UOM></__inputData>");
                PacketStringBuilder.Append("<__perform><__eventName><![CDATA[GetWIPMsgs]]></__eventName></__perform><__execute/><__requestData><CompletionMsg /><WIPMsgMgr><WIPMsgs><AcknowledgementRequired /><MsgAcknowledged /><MsgText /><PasswordRequired /><WIPMsgDetails /></WIPMsgs></WIPMsgMgr></__requestData></__service></__InSite>");
                DataReceived = Sendmessage(CamstarIP, CamstarPort, PacketStringBuilder.ToString());
            }
            catch (Exception ex) { Controller.DiagnosticOut(ex.ToString(), 2); }
        }

        /// <summary>
        /// SQL section of the Downtime Packet records downtime to the machine downtime table.
        /// </summary>
        private void SQLDownTimePacket(string message)
        {
            try                                                                             //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);               //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;//convert json to jobject
                StringBuilder sqlStringBuilder = new StringBuilder();                       //create a string builder to make the sql string
                string[] temp = GetMachineIDAndLine(receivedPacket["Machine"].ToString());
                sqlStringBuilder.Append(" USE [" + temp[1] + "] ");                            //select database
                sqlStringBuilder.Append("INSERT INTO [" + receivedPacket["Machine"] + "DownTimes] (");//start loading the SQL Command
                List<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();//gets list of all keys in json object
                string keySection = "";                                                     //contains the key section of the sql
                string valueSection = "";                                                   // contains the value section of the sql
                foreach (string key in keys)                                                //foreach key
                {
                    if (key != "Machine")                                                   //except machine as it is used as the table name. and is instead entered as an id
                    {
                        keySection += key + ", ";                                           //Make a key
                        valueSection += "@" + key + ", ";                                   //and value Reference to be replaced later
                    }
                }
                keySection += "MachineID , Timestamp ";                                          //add a machine ID and time key
                valueSection += temp[0] + " ,@Timestamp ";                                        //add a value section as well
                sqlStringBuilder.Append(keySection + ")");                                  //cap it off
                sqlStringBuilder.Append("values (" + valueSection + ");");                  //append both to the command string
                string SQLString = sqlStringBuilder.ToString();                             //convert Builder to string
                using (SqlCommand command = new SqlCommand(SQLString, Controller.ENGDBConnection))
                {                                                                           //Command Time!
                    switch (Convert.ToInt32(receivedPacket["Status"]))
                    {
                        case 0:
                            command.Parameters.AddWithValue("@Status", "Unscheduled");//replace perameters with values
                            break;

                        case 1:
                            command.Parameters.AddWithValue("@Status", "PM");//replace perameters with values
                            break;

                        case 2:
                            command.Parameters.AddWithValue("@Status", "Running");//replace perameters with values
                            break;
                    }
                    command.Parameters.AddWithValue("@NAED", receivedPacket["NAED"].ToString());
                    command.Parameters.AddWithValue("@Code", Convert.ToInt32(receivedPacket["Code"]));
                    command.Parameters.AddWithValue("@MReason", receivedPacket["MReason"].ToString());
                    command.Parameters.AddWithValue("@UReason", receivedPacket["UReason"].ToString());
                    command.Parameters.AddWithValue("@Machine", receivedPacket["Machine"].ToString());
                    command.Parameters.AddWithValue("@Timestamp", DateTime.Now);                 //add a timestamp
                    int rowsAffected = command.ExecuteNonQuery();                           // execute the command returning number of rows affected
                    Controller.DiagnosticOut(rowsAffected + " row(s) inserted", 2);         //logit
                }
            }
            catch (Exception ex)                                                            //catch exceptions
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if connection crashed
                {
                    Controller.ReastablishSQL(SQLDownTimePacket, message);                  //reastablish it
                }
                Controller.DiagnosticOut(ex.ToString(), 1);                                 //else logit and move on
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
                PacketStringBuilder.Append("<__InSite __encryption=\"2\" __version=\"1.1\"><__session><__connect><user>");
                PacketStringBuilder.Append("<__name>d.paddock</__name>");
                PacketStringBuilder.Append("</user>");
                PacketStringBuilder.Append("<password __encrypted=\"yes\">d81b896ee9a9697df6334a1df6f7e8286c9866e0eb243f3c4ca9b80fc0e38dd3d8c0ccef78401ff7</password>");
                PacketStringBuilder.Append("</__connect></__session><__service __serviceType=\"ResourceSetupTransition\"><__utcOffset><![CDATA[-04:00:00]]></__utcOffset><__inputData><Availability><![CDATA[1]]></Availability><Resource>");
                PacketStringBuilder.Append("<__name><![CDATA[" + receivedPacket["Machine"] + "]]></__name>");
                PacketStringBuilder.Append("</Resource><ResourceGroup><__name><![CDATA[]]></__name></ResourceGroup><ResourceStatusCode>");
                switch (Convert.ToInt32(receivedPacket["Status"]))
                {
                    case 0:
                        PacketStringBuilder.Append("<__name><![CDATA[Unscheduled]]></__name>");//if down send down
                        break;

                    case 1:
                        PacketStringBuilder.Append("<__name><![CDATA[Scheduled]></__name>");//Scheduled downtime
                        break;

                    case 2:
                        PacketStringBuilder.Append("<__name><![CDATA[Available]]></__name>");//if running send running
                        break;

                    case 3:
                        PacketStringBuilder.Append("<__name><![CDATA[P/M]]></__name>");//Preventive Maintenence
                        break;
                }
                PacketStringBuilder.Append("</ResourceStatusCode><ResourceStatusReason><__name><![CDATA[]]></__name>");
                PacketStringBuilder.Append("</ResourceStatusReason></__inputData ><__execute /><__requestData ><CompletionMsg /><ACEMessage /><ACEStatus /></__requestData ></__service ></__InSite >");
                DataReceived = Sendmessage(CamstarIP, CamstarPort, PacketStringBuilder.ToString());
            }
            catch (Exception ex) { Controller.DiagnosticOut(ex.ToString(), 2); }
        }

        /// <summary>
        ///  Packet sent at each index to sql records it to the resource table.
        /// </summary>
        private void SQLShortTimeStatisticPacket(string message)
        {
            try                                                                             //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);               //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;//convert it to a jobject
                StringBuilder sqlStringBuilder = new StringBuilder();                       //string builder to create the sql string
                string[] temp = GetMachineIDAndLine(receivedPacket["Machine"].ToString());
                sqlStringBuilder.Append(" USE [" + temp[1] + "] ");                            //select database
                sqlStringBuilder.Append("INSERT INTO [" + receivedPacket["Machine"].ToString() + "ShortTimeStatistics] (");//start building SQL string
                List<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();//gets list of all keys in json object
                string keySection = "";                                                     //stores the key section of the sql
                string valueSection = "";                                                   //stores the value section of the sql
                foreach (string key in keys)                                                //foreach key
                {
                    if (key != "Machine")                                                   //except machine as it is used as the table name. and is loaded as an id
                    {
                        keySection += "[" + key + "], ";                                    //Make a key
                        valueSection += "@" + key + ", ";                                   //and value Reference to be replaced later
                    }
                }
                keySection += "[MachineID], [Timestamp] ";                                  //add a machineIDsection and timestamp
                valueSection += temp[0] + ", @Timestamp ";                                  //add to value section to
                sqlStringBuilder.Append(keySection + ")");                                  //cap it off
                sqlStringBuilder.Append("values ( " + valueSection + ");");//append both to the command string
                string SQLString = sqlStringBuilder.ToString();                             //Convert builder to sql string
                using (SqlCommand command = new SqlCommand(SQLString, Controller.ENGDBConnection))
                {                                                                           //Comand Time!
                    foreach (string key in keys)                                            //foreach key
                    {
                        if (key != "Machine")                                               //Except Machine
                            if (key != "HeadNumber")                                        //and head number
                            {                                                               // convert to bool
                                command.Parameters.AddWithValue("@" + key, 1 == Convert.ToInt32(receivedPacket[key]));
                            }
                            else                                                            //if it is a head number add it as an int
                                command.Parameters.AddWithValue("@" + key, Convert.ToInt32(receivedPacket[key]));
                    }
                    command.Parameters.AddWithValue("@Timestamp", DateTime.Now);            //add a timestamp
                    command.Parameters.AddWithValue("@Machine", receivedPacket["Machine"].ToString());//add teh machine name
                    int rowsAffected = command.ExecuteNonQuery();                           // execute the command returning number of rows affected
                    Controller.DiagnosticOut(rowsAffected + " row(s) inserted", 2);         //logit
                }
            }
            catch (Exception ex)                                                            //catch exceptions
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if connection crashed
                {
                    Controller.ReastablishSQL(SQLShortTimeStatisticPacket, message);        //reestablish it
                }
                Controller.DiagnosticOut(ex.ToString(), 1);                                 //log it
            }
        }

        ///// <summary>
        /////  Packet sent at each index to MDE over UDP Deprecieted currently as MDE has been deprecieted
        ///// </summary>
        //private void MDEShortTimeStatisticPacket(string message)
        //{
        //    try
        //    {
        //        List<byte> bySNPoSend = new List<byte>();                                   //make a byte array for the packet to be sent
        //        List<bool> bits = new List<bool>();                                         //make a bool array to convert to each bit in the byte array
        //        string jsonString = message.Substring(7, message.Length - 7);               //grab json data from the end.
        //        JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;//convert to jobject
        //        List<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();//gets list of all keys in json object
        //        keys.Sort();                                                                //Make sure all keys are alphabetical for easeir documentation
        //        foreach (string key in keys)                                                //foreach key
        //        {
        //            if (key != "Machine" && key != "HeadNumber")                            //that isnt HeadNumber or Machine name
        //                bits.Add(Convert.ToInt32(receivedPacket[key] ?? 0) == 1);           //if the key's value is null set bit to false, otherwise set it to the bit.
        //        }
        //        bySNPoSend.Add((byte)'~');                                                  //add MDE Header byte
        //        for (int Index = 0; Index < bits.Count; Index += 8)                         //foreach group of 8 bools in bits
        //        {
        //            bool[] Bools;                                                           //generate a new bool array
        //            if (bits.Count - Index >= 8)                                            //if there are atleast 8 bools left
        //            {
        //                Bools = new bool[8];                                                //make the bool array length of 8
        //                Array.Copy(bits.ToArray(), Index, Bools, 0, 8);                     //Copy the 8 bits we are on into the new bool array
        //            }
        //            else
        //            {                                                                       //else if less than 8
        //                Bools = new bool[bits.Count - Index];                               //make the bool array the size of the remaining bits
        //                Array.Copy(bits.ToArray(), Index, Bools, 0, bits.Count - Index);    //copy remaining bits to array
        //            }
        //            bySNPoSend.Add(ConvertBoolArrayToByteLeftJustified(Bools));             //turn the bits into a byte Left justified (true true turns to 11000000)
        //        }
        //        string Theo = Convert.ToString(receivedPacket["Theo"]);                     //get theorectical from packet
        //        bySNPoSend.Add((byte)Convert.ToInt32(receivedPacket["HeadNumber"]));        //add the head number to the bytes to send
        //        for (int x = 0; x < Theo.Length; x++)                                       //add each character of the theoretical length to the packet
        //        {
        //            bySNPoSend.Add((byte)Theo[x]);                                          //add the character
        //        }
        //        bySNPoSend.Add((byte)10);                                                   //new line end packet character
        //        MDEClient.Send(bySNPoSend.ToArray(), bySNPoSend.Count, MDEIP, MDEClientPort);//UnComment if your going to use it it will stop build otherwise however
        //    }
        //    catch (Exception ex)                                                            //catch exceptions
        //    {
        //        Controller.DiagnosticOut(ex.ToString(), 1);                                 //logit and move on
        //    }
        //}

        #endregion Packet Section

        #region Connections/Resources/Misc

        /// <summary>
        /// Send message To Camstar and listen for a message back.
        /// </summary>
        private string Sendmessage(string host, int port, string content)
        {
            ServerConnection connection = new ServerConnection();
            //create a server connection
            try
            {
                var connected = connection.Connect(host, port);                             // try connecting on the host and port passed in
                if (!connected) return "";                                                  // return nothing if cant connect
                connection.Send(content);                                                   // send data
                connection.Receive(out var result);                                         // reviece message from server, and store into variable
                connection.Disconnect();                                                    // Close connection
                try
                {
                    return XDocument.Parse(result).ToString();                              // format recieved message into xml
                }
                catch
                {
                    return result;                                                          // if formatting fails just send unformatted back
                }
            }
            catch (Exception ex)                                                            // If an error occurred return null string
            {
                Controller.DiagnosticOut(ex.ToString(), 1);                                 //logit
                return "";                                                                  //return null string
            }
        }

        /// <summary>
        /// Convert up to a byte to 8 bools
        /// </summary>
        private bool[] ConvertByteToBoolArray(byte b)
        {
            bool[] result = new bool[8];                                                    //bool array to return
            for (int i = 0; i < 8; i++)                                                     // check each bit in the byte.
                result[i] = (b & (1 << i)) == 0 ? false : true;                             //if 1 set to true, if 0 set to false
            Array.Reverse(result);                                                          // reverse the array
            return result;                                                                  //return the result
        }

        /// <summary>
        /// Convert up to 8 bools to 1 byte right justfied 0001111
        /// </summary>
        private byte ConvertBoolArrayToByteRightJustified(bool[] source)
        {
            byte result = 0;                                                                //result to return
            int index = 8 - source.Length;                                                  // This assumes the array never contains more than 8 elements!
            foreach (bool b in source)                                                      //foreach bool in the bool array passed in
            {
                if (b)                                                                      // if the element is 'true'
                    result |= (byte)(1 << (7 - index));                                     //set the bit at that position
                index++;                                                                    //increment the position
            }
            return result;                                                                  //return the result
        }

        /// <summary>
        /// From Machine name get Line and machine ID
        /// </summary>
        private string[] GetMachineIDAndLine(string Machine)
        {
            try
            {
                string[] result = new string[2] { "", "" };                                                                //result to return
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append(" USE [" + ConfigurationManager.AppSettings["ENGDBDatabase"] + "] ");//select database
                sqlStringBuilder.Append("select MachineID, Line from MachineInfoTable where MachineName='" + Machine + "';");  //start loading the command into the string
                string SQLString = sqlStringBuilder.ToString();                             //Convert Builder to string
                using (SqlCommand command = new SqlCommand(SQLString, Controller.ENGDBConnection))
                {                                                                           //Comand Time!
                    using (IDataReader dr = command.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            result[0] = dr[0].ToString();
                            result[1] = dr[1].ToString();
                        }
                    }
                }
                return result;
                //return the result
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("There is already an open DataReader"))
                {
                    Thread.Sleep(100);
                    return GetMachineIDAndLine(Machine);
                }
                else
                    Controller.DiagnosticOut(ex.ToString(), 1);                                 //if not handled log it and move on
                return new string[2] { "", "" };
            }
        }

        /// <summary>
        /// From Machine name get Line and machine ID
        /// </summary>
        private bool CheckForDatabase(string Line)
        {
            bool result = true;
            try
            {
                using (SqlCommand command = new SqlCommand("SELECT name from sys.databases where name='" + Line + "'", Controller.ENGDBConnection))
                {
                    using (IDataReader dr = command.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            result = false;
                        }
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("There is already an open DataReader"))
                {
                    Thread.Sleep(100);
                    return CheckForDatabase(Line);
                }
                else
                    Controller.DiagnosticOut(ex.ToString(), 1);                                 //if not handled log it and move on
                return true;
            }
        }

        /// <summary>
        /// Convert up to 8 bools to 1 byte right justfied 11110000
        /// </summary>
        private byte ConvertBoolArrayToByteLeftJustified(bool[] source)
        {
            byte result = 0;                                                                //result to return
            int index = 0;                                                                  //index
            foreach (bool b in source)                                                      //foreach bool in the bools passed in
            {
                if (b)                                                                      // if the element is 'true'
                    result |= (byte)(1 << (7 - index));                                     //set the bit at that position
                index++;                                                                    //increment the position
            }
            return result;                                                                  //return result
        }

        #endregion Connections/Resources/Misc
    }
}