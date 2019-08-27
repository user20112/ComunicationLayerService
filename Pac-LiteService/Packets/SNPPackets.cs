using Camstar.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SNPService.Comunications.QRQC;
using SNPService.Resources;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace SNPService
{
    internal class SNPPackets
    {
        #region Variable Section

        //public TopicPublisher Publisher;                                                      // publishes to the Pac-Light Outbound topic
        //public UdpClient MDEClient;                                                           // depreciated comunication to MDE over udp
        //public string TopicName = "SNP.Outbound";                                             // Test Output topic
        //private string MDEIP;                                                                 // currently my ip for MDEing. once it is known to be working i have to get this ip from gerry.
        //private int MDEClientPort;                                                            // depreciated used to comunicate to MDE over UDP ( the receiveing port of MDE
        //public int MDEOutPort;                                                                // depreciated used to comunicate to MDE over UDP ( the sending port to MDE
        public static string CamstarUsername;                                                         // username used to comunicate with camstar

        public static string CamstarPassword;                                                         // password used to ocmunicate with camstar
        public static string CamstarIP;                                                               // IP of the Camstar System you are talking to
        public static int CamstarPort;                                                                // Port of the Camstar system you are talking to

        public SNPPackets()
        {
            CamstarUsername = ConfigurationManager.AppSettings["CamstarUsername"];              // pull Camstar username
            CamstarPassword = Camstar.Util.CryptUtil.Encrypt(Encryptor.EncryptOrDecrypt(ConfigurationManager.AppSettings["CamstarPassword"]));//and camstar password
            CamstarIP = ConfigurationManager.AppSettings["CamstarIP"];                          //and camstar ip
            CamstarPort = Convert.ToInt32(ConfigurationManager.AppSettings["CamstarPort"]);     //and Camstar Port
            //MDEIP = ConfigurationManager.AppSettings["MDEIP"];                                //and MDE Ip deprecieted as MDE is no longer used
            //MDEClientPort = Convert.ToInt32(ConfigurationManager.AppSettings["MDEClientPort"]);//and MDE Ports deprecieted as mde is no longer used
            //MDEOutPort = Convert.ToInt32(ConfigurationManager.AppSettings["MDEOutPort"]);     //from app.config
            Dictionary<int, Action<string>> SNPDictionary = new Dictionary<int, Action<string>>();
            SNPDictionary.Add(1, (Action<string>)IndexSummaryPacket);
            SNPDictionary.Add(2, (Action<string>)DowntimePacket);
            SNPDictionary.Add(3, (Action<string>)ShortTimeStatisticPacket);
            SNPDictionary.Add(4, (Action<string>)ProductChangeOverPacket);
            SNPDictionary.Add(5, (Action<string>)GasAnalyzer);
            SNPDictionary.Add(252, (Action<string>)DeleteMachinePacket);
            SNPDictionary.Add(253, (Action<string>)EditMachinePacket);
            SNPDictionary.Add(254, (Action<string>)NewMachinePacket);
            SNPService.Packets.Add(1, SNPDictionary);
        }

        #endregion Variable Section

        #region Packet Section

        /// <summary>
        /// Called whenever a new machine is detected
        /// Creates all required databases and entries for the machine detaield in the packet.
        /// </summary>
        public void NewMachinePacket(string message)
        {
            try                                                                                     //try loop in case command fails.
            {
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("New Machine Packet!", 2));   // log the packet as have been received
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;      //convert the json to an object
                string machineName = receivedPacket["Machine"].ToString();                          //get the important sections of the packet out ( that arent errors)
                string Line = receivedPacket["Line"].ToString();
                string Theo = receivedPacket["Theo"].ToString();
                int snp_ID = Convert.ToInt32((byte)message[2]);                                     //get the SNP ID from the message header
                string Plant = receivedPacket["Plant"].ToString();
                string Engineer = receivedPacket["Engineer"].ToString();
                string Errors = "";                                                                 //Start Errors as blank
                try                                                                                 //If this fails break dont break the application
                {
                    string ErrorString = receivedPacket["Errors"].ToString();                       //Get all errors passed in
                    if (ErrorString.Length > 0)
                    {
                        string[] ErrorArray = ErrorString.Split(',');                               // break up csv errors
                        foreach (string error in ErrorArray)                                        //foreach error add it to the Errors Section
                        {
                            Errors += "[" + error + "] [bit] NOT NULL DEFAULT 0, ";                 //set the feild type to bit and allow it to be nullable
                        }
                        Errors = Errors.Substring(0, Errors.Length - 2);
                    }                        //remove extra comma and space
                }
                catch (Exception ex)                                                                //if this fails set it to defualt no errors
                {
                    SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));
                    Errors = "";
                }
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append(" USE [" + ConfigurationManager.AppSettings["ENGDBDatabase"] + " ] ");//load the MachineInfoEntry
                sqlStringBuilder.Append(" insert into MachineInfoTable (MachineName, Line, SNPID , "
                    + "Theo,Plant , Engineer) values( @machine , @Line , @SNPID , @Theo, @Plant , @Engineer);");
                string SQLString = sqlStringBuilder.ToString();                                    //Convert the builder to the string
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    connection.Open();                                                              //open the connection
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {                                                                               //Commmand Time!
                        command.Parameters.AddWithValue("@machine", machineName);                   //replace all parameters with their respective values
                        command.Parameters.AddWithValue("@Line", Line);
                        command.Parameters.AddWithValue("@SNPID", snp_ID);
                        command.Parameters.AddWithValue("@Theo", Theo);
                        command.Parameters.AddWithValue("@Plant", Plant);
                        command.Parameters.AddWithValue("@Engineer", Engineer);
                        int rowsAffected = command.ExecuteNonQuery();                               //execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }
                    bool Missing = CheckForDatabase(Line);

                    if (Missing)                                                                    //if we dont have a database yet
                    {
                        sqlStringBuilder = new StringBuilder();                                     //this builder will build the SQL String
                        sqlStringBuilder.Append(" CREATE DATABASE [EngDb-" + Line + "];");          //create one
                        SQLString = sqlStringBuilder.ToString();                                    //Convert the builder to the string
                        using (SqlCommand command = new SqlCommand(SQLString, connection))
                        {                                                                           //Commmand Time!
                            int rowsAffected = command.ExecuteNonQuery();                           //execute the command returning number of rows affected
                            SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " databases created", 2));//logit
                        }
                        sqlStringBuilder = new StringBuilder();                                     //this builder will build the SQL String
                        sqlStringBuilder.Append("use [EngDb-" + Line + "];");
                        sqlStringBuilder.Append("CREATE TABLE [dbo].[Descriptions](");
                        sqlStringBuilder.Append("	[columnIdPK] [int] IDENTITY(1,1) NOT NULL,");
                        sqlStringBuilder.Append("	[Table] [nvarchar](128) NULL,");
                        sqlStringBuilder.Append("	[ColumnId] [nvarchar](128) NULL,");
                        sqlStringBuilder.Append("	[Description] [nvarchar](512) NULL,");
                        sqlStringBuilder.Append("PRIMARY KEY CLUSTERED ");
                        sqlStringBuilder.Append("(");
                        sqlStringBuilder.Append("	[columnIdPK] ASC");
                        sqlStringBuilder.Append(")WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF"
                    + ", ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]");
                        sqlStringBuilder.Append(") ON [PRIMARY]");
                        SQLString = sqlStringBuilder.ToString();                                    //Convert the builder to the string
                        using (SqlCommand command = new SqlCommand(SQLString, connection))
                        {                                                                           //Commmand Time!
                            int rowsAffected = command.ExecuteNonQuery();                           //execute the command returning number of rows affected
                            SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " databases created", 2));//logit
                        }
                    }
                    string STSDB = machineName + "ShortTimeStatistics";                             //Stores the Database string for short time statistics
                    string DB = machineName;                                                        //Main index sumary
                    string DTDB = machineName + "DownTimes";                                        //and Machine downtimes respectivly.
                    List<string> Columns = new List<string>(new string[8] { "MachineID", "Timestamp",
                        "Good", "Bad", "Empty", "Attempt", "Input", "Head_number" });               //insert the hardcoded columns and their descriptions
                    List<string> Descriptions = new List<string>(new string[8] {
                        "Machine ID that corolates all info in the Machine Info Table to the Machine in each other table entry.",
                        "Time stamp of a given transaction", "wether the part was good", "wether the part was bad", "weather the head was empty",
                        "wether we attempted to make a part", "Weather we received and input and attempted to make a product", "which head it was on" });
                    List<string> Table = new List<string>(new string[8] { STSDB, STSDB, STSDB, STSDB, STSDB, STSDB, STSDB, STSDB });//insert the tab le they come from
                    foreach (string error in receivedPacket["Errors"].ToString().Split(','))        //split up the errors passsed in and foreach
                    {
                        Columns.Add(error);                                                         //set the columnname
                        Descriptions.Add(error);                                                    //and description to the error name
                        Table.Add(STSDB);                                                           //and the database to the ShortTimeStatistics Database.
                    }
                    Columns.AddRange(new string[15] { "MachineID", "Timestamp", "Good", "Bad", "Empty",
                        "Indexes", "NAED", "UOM", "Timestamp", "MReason", "UReason", "NAED", "MachineID", "StatusCode", "Code" });//add hardcoded column names and descriptions
                    Descriptions.AddRange(new string[15] { "Machine ID that corolates all info in the Machine Info Table to the Machine in each other table entry.",
                        "Time stamp of a given transaction", "Number of good parts produced", "Number of bad parts produced", "Number of times the head was empty during an index",
                        "Number of indexes passed", "Product we are curren tly producing", "Unit of Mesure for the product we are producing", "Time stamp of a given transaction",
                        "Machine reason for a downtime", "User defined reason for a downtime", "The product we are currently producing",
                        "Machine ID that corolates all info in the Machine Info Table to the Machine in each other table entry."
                        , "Code for which status the machine is in (2 running 1 scheduled downtime 0 unschedled downtime 3 PM",
                        "Code for why the machine went down for camstar" });
                    Table.AddRange(new string[15] { DB, DB, DB, DB, DB, DB, DB, DB, DTDB, DTDB, DTDB, DTDB, DTDB, DTDB, DTDB });//these ones are all part of the DowntimeDatabase.
                    int x = 0;
                    sqlStringBuilder = new StringBuilder();                                         //used to build the isnert querry
                    sqlStringBuilder.Append(" USE [EngDb-" + Line + "] ");                          //use the data base that the line corolates to.
                    foreach (string Column in Columns)                                              //foreach value in the columns list.
                    {
                        sqlStringBuilder.Append(" Insert into Descriptions ( [Table] , [ColumnId] , [Description]) ");//fill the sql database
                        sqlStringBuilder.Append("values('" + Table[x] + "','" + Column + "','" + Descriptions[x] + "' ); ");//with the three different feilds
                        x++;                                                                        //increment table and descriptions to be the same index as the Column.
                    }
                    SQLString = sqlStringBuilder.ToString();                                        //Convert the builder to the string
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {                                                                               //Commmand Time!
                        int rowsAffected = command.ExecuteNonQuery();                               //execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }
                    sqlStringBuilder = new StringBuilder();
                    sqlStringBuilder.Append(" USE [EngDb-" + Line + "] ");                          //Load the create tables with defualt table information using MachineName as the resource name and line as the database name
                    sqlStringBuilder.Append(" CREATE TABLE [dbo].[" + machineName + "ShortTimeStatistics](");
                    sqlStringBuilder.Append("	[MachineID] [int] NOT NULL,Timestamp [datetime2] NOT NULL, [Good] "
                    + "[bit] NOT NULL, [Bad] [bit] NOT NULL, [Empty] [bit] NOT NULL, [Attempt] [bit] NOT NULL, "
                    + "[Input] [bit] NOT NULL, [Other] [bit] NOT NULL, [Head_number] [int] NOT NULL," + Errors);
                    sqlStringBuilder.Append(" ) ON [PRIMARY] ");                                    //Create the Short Time Statistics Index Summary and Downtime Tables.
                    sqlStringBuilder.Append(" CREATE TABLE [dbo].[" + machineName + "](");
                    sqlStringBuilder.Append(" 	[EntryID] [int] IDENTITY(1,1) NOT NULL,	[MachineID] [int] NULL,	"
                    + "[Good] [int] NULL,	[Bad] [int] NULL,	[Empty] [int] NULL,	[Indexes] [int] NULL,	[NAED]"
                    + " [varchar](20) NULL,	[UOM] [varchar](10) NULL,	[Timestamp] [datetime2] NULL) ON [PRIMARY] ");
                    sqlStringBuilder.Append(" CREATE TABLE [dbo].[" + machineName + "DownTimes](");
                    sqlStringBuilder.Append(" 	[Timestamp] [datetime2] NULL,	[MReason] [varchar](255) NULL,	"
                    + "[UReason] [varchar](255) NULL,	[NAED] [varchar](20) NULL,	[MachineID] [int] NULL,	"
                    + "[StatusCode] [nvarchar](30) NULL,	[Code] [int] NULL) ON [PRIMARY]; ");
                    SQLString = sqlStringBuilder.ToString();                                        //Convert the builder to the string
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {                                                                               //Commmand Time!
                        int rowsAffected = command.ExecuteNonQuery();                               //execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if the connection crashed
                {
                    SNPService.ReastablishSQL(NewMachinePacket, message);                           //reastablish it
                }
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));           //if not handled log it and move on
            }
        }

        /// <summary>
        /// Updates an existing machine based of machine name. addeds errors as new columns as well.
        /// </summary>
        public void EditMachinePacket(string message)
        {
            try                                                                                     //try loop in case command fails.
            {
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Edit Machine Packet!", 2));  // log the packet as have been received
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;      //Convert it into a jobject
                string machineName = receivedPacket["Machine"].ToString();                          //gather important sections into variables
                string Line = receivedPacket["Line"].ToString();
                string Theo = receivedPacket["Theo"].ToString();
                string Engineer = receivedPacket["Engineer"].ToString();
                int snp_ID = Convert.ToInt32((byte)message[2]);                                     //get snp id from message header

                StringBuilder sqlStringBuilder = new StringBuilder();                               //string builder to build the sql
                sqlStringBuilder.Append(" USE [" + ConfigurationManager.AppSettings["ENGDBDatabase"] + " ] ");//load the edit command using Machine as the resource name
                sqlStringBuilder.Append(" update MachineInfoTable set Line = @Line, SNPID = @SNPID , "
                    + "Theo = @Theo, Engineer = @Engineer  where MachineName = @machine;");
                string SQLString = sqlStringBuilder.ToString();                                     //convert the builder to a string
                string[] ErrorArray;
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    connection.Open();                                                              //open the connection
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {                                                                               //command time!
                        command.Parameters.AddWithValue("@machine", machineName);                   //replace all parameters with values
                        command.Parameters.AddWithValue("@Line", Line);
                        command.Parameters.AddWithValue("@SNPID", snp_ID);
                        command.Parameters.AddWithValue("@Theo", Theo);
                        command.Parameters.AddWithValue("@Engineer", Engineer);
                        int rowsAffected = command.ExecuteNonQuery();                               // execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }
                    sqlStringBuilder = new StringBuilder();                                         //reset string builder for next command
                    sqlStringBuilder.Append(" USE [EngDb-" + Line + "] ");                          //load alter table command
                    sqlStringBuilder.Append("Alter Table [" + receivedPacket["Machine"] + "ShortTimeStatistics] ADD ");
                    string ErrorString = receivedPacket["Errors"].ToString();                       //grab all errors passed in
                    ErrorArray = ErrorString.Split(',');                                            //divide the csv of errors
                    string Errors = "";                                                             //this string is added to the sql
                    foreach (string error in ErrorArray)                                            //foreach error add it to the Errors Section
                    {
                        Errors += ("[" + error + "] [bit] NOT NULL DEFAULT 0, ");                   //column is a bit feild that is nullable
                    }
                    Errors = Errors.Substring(0, Errors.Length - 2);                                //remove extra space and comma
                    sqlStringBuilder.Append(Errors + ";");                                          //append a semicolon
                    SQLString = sqlStringBuilder.ToString();                                        //Convert builder to string
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {                                                                               //Comand Time Again!
                        int rowsAffected = command.ExecuteNonQuery();                               // execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }

                    string STSDB = machineName + "ShortTimeStatistics";                             //Stores the Short Time Statistics Database Name.
                    sqlStringBuilder = new StringBuilder();                                         //used to build queries
                    sqlStringBuilder.Append(" USE [EngDb-" + Line + "] ");
                    foreach (string error in ErrorArray)
                    {
                        sqlStringBuilder.Append(" Insert into Descriptions ( [Table] , [ColumnId] , [Description]) ");
                        sqlStringBuilder.Append("values('" + STSDB + "','" + error + "','" + error + "' ); ");
                    }
                    SQLString = sqlStringBuilder.ToString();                                        //Convert the builder to the string
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {                                                                               //Commmand Time!
                        int rowsAffected = command.ExecuteNonQuery();                               //execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }
                }
            }
            catch (Exception ex)                                                                    //catch exceptions
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if the connection crashed
                {
                    SNPService.ReastablishSQL(EditMachinePacket, message);                          //reestablish connection
                }
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));           //else logit and move on
            }
        }

        /// <summary>
        /// Deletes existing machine ( deletes the Machine Info entry and all tables asociated with it
        /// </summary>
        public void DeleteMachinePacket(string message)
        {
            SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Delete Machine Packet!", 2));    // log the packet as have been received
            string jsonString = message.Substring(7, message.Length - 7);                           //grab json data from the end.
            JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;          //convert it to a Jobject
            string Line = receivedPacket["Line"].ToString();
            string machineName = receivedPacket["Machine"].ToString();                              //get the machine name in a nice easy feild
            try                                                                                     //try loop in case command fails.
            {
                StringBuilder sqlStringBuilder = new StringBuilder();                               //string builder for sql command
                sqlStringBuilder.Append(" USE [" + ConfigurationManager.AppSettings["ENGDBDatabase"] + " ] ");//load sql command and edit for machine name as the resource name
                sqlStringBuilder.Append(" delete from MachineInfoTable where MachineName = @machine;");//drop the reference
                string SQLString = sqlStringBuilder.ToString();                                     //Convert builder to string
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    connection.Open();                                                              //open the connection
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {                                                                               //Comand Time!
                        command.Parameters.AddWithValue("@machine", receivedPacket["Machine"].ToString());//replace parameters with values
                        int rowsAffected = command.ExecuteNonQuery();                               // execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));         //logit
                    }
                    sqlStringBuilder = new StringBuilder();                                         //clear string builder
                    sqlStringBuilder.Append(" USE [EngDb-" + Line + "] ");                          //build next section
                    sqlStringBuilder.Append("drop table [" + machineName + "];");
                    sqlStringBuilder.Append("drop table [" + machineName + "DownTimes];");
                    sqlStringBuilder.Append("drop table [" + machineName + "ShortTimeStatistics];");
                    SQLString = sqlStringBuilder.ToString();                                        //Convert builder to string
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {                                                                               //Comand Time!
                        int rowsAffected = command.ExecuteNonQuery();                               //execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }
                }
            }
            catch (Exception ex)                                                                    //catche exceptions
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if the connection crashed
                {
                    SNPService.ReastablishSQL(DeleteMachinePacket, message);                        //restablish the connection
                }
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));           //else logit and move on
            }
        }

        /// <summary>
        /// Summary packet received every fifteen minutes from the plc. Send a throughput packet to camstar and record the data in sql
        /// </summary>
        public void IndexSummaryPacket(string message)
        {
            SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Fifteen Minute Packet Received!", 3));//logit
            Task.Run(() => SQLIndexSummary(message));                                               //save data to sql async, return value doesnt matter
            Task.Run(() => CamstarIndexSummary(message));                                           //send data to Camstar, return value doesnt matter
        }

        /// <summary>
        ///  Packet sent each time there is a Downtime received from SNP record in sql and send a downtime packet to camstar
        /// </summary>
        public void DowntimePacket(string message)
        {
            SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("DownTime Packet Received!", 3)); //logit
            Task.Run(() => SQLDownTimePacket(message));                                             //Save data to SQL, return value doesnt matter
            Task.Run(() => CamstarDowntimePacket(message));                                         //send data to Camstar, return value doesnt matter
            Task.Run(() => QRQCDownTimePacket(message));                                            //Update QRQC Application for the machine
        }

        /// <summary>
        ///  Packet sent at each index
        /// </summary>
        public void ShortTimeStatisticPacket(string message)
        {
            SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Short Time Statistic Packet Received!", 3));//logit
            Task.Run(() => SQLShortTimeStatisticPacket(message));                                   //Save data to sql return value doesnt matter
            //Task.Run(() => MDEShortTimeStatisticPacket(message));                                 //Send Message to UDP port for MDE (depreciated but kept for incasei t is used elsewhere
        }

        /// <summary>
        /// Packet sent when a product is switched over.
        /// </summary>
        public void ProductChangeOverPacket(string message)
        {
            SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Short Time Statistic Packet Received!", 3));//logit
            Task.Run(() => CamstarProductChangePacket(message));
        }

        /// <summary>
        /// Packet sent When the two sensors are checked for HLine Hydrometer and vacume sensor
        public void GasAnalyzer(string message)
        {
            SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Gas Analyzer Packet received", 3));//logit
            try                                                                                     //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;      //Convert json to object
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append("USE [EngDb-" + receivedPacket["Line"] + "] ");            //select database
                sqlStringBuilder.Append("IF not EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'GasAnalyzerData') BEGIN CREATE TABLE [dbo].[GasAnalyzerData] ");
                sqlStringBuilder.Append("([InternalWaterPercent] [decimal](6, 4) NULL,[InternalPresureReading] [decimal](6, 4) NULL,[ExternalWaterPercent] [decimal](6, 4) NULL,[Timestamp] [datetime2](7) NULL,[Head_number] [int] NULL) ON [PRIMARY] END ");                                                        //start loading the command into the string
                sqlStringBuilder.Append("delete from [GasAnalyzerData] WHERE Timestamp < DATEADD(DAY, -" + receivedPacket["DaysToRetain"].ToString() + ", GETDATE()) ");                                                        //start loading the command into the string
                sqlStringBuilder.Append("Insert into GasAnalyzerData ([Head_number],[InternalWaterPercent],[ExternalWaterPercent],[Timestamp],[InternalPresureReading]) values (@Head_number,@InternalWaterPercent,@ExternalWaterPercent,@Timestamp,@InternalPresureReading) ");
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    connection.Open();                                                              //open the connection
                    using (SqlCommand command = new SqlCommand(sqlStringBuilder.ToString(), connection))
                    {                                                                               //Comand Time!
                        double InternalWaterPercent = 0;
                        double ExternalWaterPercent = 0;
                        double InternalPresureReading = 0;
                        double.TryParse(receivedPacket["InternalWaterPercent"].ToString(), out InternalWaterPercent);
                        double.TryParse(receivedPacket["ExternalWaterPercent"].ToString(), out ExternalWaterPercent);
                        double.TryParse(receivedPacket["InternalPresureReading"].ToString(), out InternalPresureReading);
                        command.Parameters.AddWithValue("@Head_number", Convert.ToInt32(receivedPacket["Head_number"]));//replace parameters with values
                        command.Parameters.AddWithValue("@InternalWaterPercent", InternalWaterPercent);
                        command.Parameters.AddWithValue("@ExternalWaterPercent", ExternalWaterPercent);
                        command.Parameters.AddWithValue("@InternalPresureReading", InternalPresureReading);
                        command.Parameters.AddWithValue("@TimeStamp", DateTime.Now);
                        int rowsAffected = command.ExecuteNonQuery();                               // execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }
                }
            }
            catch (Exception ex)                                                                    //catch exceptions
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if the connection crashed
                {
                    SNPService.ReastablishSQL(GasAnalyzer, message);                                      //reastablish it
                }
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));           //else log the error and move on
            }
        }

        /// <summary>
        /// SQL section of the Index Summary. saves data to the resource table.
        /// </summary>
        private void SQLIndexSummary(string message)
        {
            try                                                                                     //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;      //Convert json to object
                StringBuilder sqlStringBuilder = new StringBuilder();
                string[] MachineAndLine = GetMachineIDAndLine(receivedPacket["Machine"].ToString());
                sqlStringBuilder.Append(" USE [EngDb-" + MachineAndLine[1] + "] ");                 //select database
                sqlStringBuilder.Append("INSERT INTO [" + receivedPacket["Machine"].ToString() + "] (");  //start loading the command into the string
                List<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();       //gets list of all keys in json object
                string keySection = "";                                                             //stores the key section of the SQL
                string valueSection = "";                                                           //stores the value section of the SQL
                foreach (string key in keys)                                                        //foreach key
                {
                    if (key != "Machine")                                                           //except machine as it uses an ID.
                    {
                        keySection += key + ", ";                                                   //Make a key
                        valueSection += "@" + key + ", ";                                           //and value Reference to be replaced later
                    }
                }
                keySection += "Timestamp, ";                                                        //Make a Time key since it is generated server side
                valueSection += "@Timestamp, ";                                                     //and value Reference to be replaced later
                keySection += "MachineID ";                                                         //Add a machineID section
                valueSection += MachineAndLine[0] + " ";                                            //and value
                sqlStringBuilder.Append(keySection + ")");                                          //cap it of
                sqlStringBuilder.Append("Values ( " + valueSection + ");");                         //append both to the command string
                string SQLString = sqlStringBuilder.ToString();                                     //Convert Builder to string
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    connection.Open();                                                              //open the connection
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {                                                                               //Comand Time!
                        command.Parameters.AddWithValue("@NAED", receivedPacket["NAED"].ToString());//replace parameters with values
                        command.Parameters.AddWithValue("@Good", Convert.ToInt32(receivedPacket["Good"]));
                        command.Parameters.AddWithValue("@Bad", Convert.ToInt32(receivedPacket["Bad"]));
                        command.Parameters.AddWithValue("@Empty", Convert.ToInt32(receivedPacket["Empty"]));
                        command.Parameters.AddWithValue("@Indexes", Convert.ToInt32(receivedPacket["Indexes"]));
                        command.Parameters.AddWithValue("@UOM", receivedPacket["UOM"].ToString());
                        command.Parameters.AddWithValue("@Timestamp", DateTime.Now);                //add a timestamp
                        command.Parameters.AddWithValue("@Machine", receivedPacket["Machine"].ToString());//add the machine name
                        int rowsAffected = command.ExecuteNonQuery();                               // execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }
                }
            }
            catch (Exception ex)                                                                    //catch exceptions
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if the connection crashed
                {
                    SNPService.ReastablishSQL(SQLIndexSummary, message);                            //reastablish it
                }
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));           //else log the error and move on
            }
        }

        /// <summary>
        /// Camstar section of the Fifteen Minut Packet sends a throughput packet for the resource named for the machine.
        /// </summary>
        private void CamstarIndexSummary(string message)
        {
            try
            {
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                StringBuilder PacketStringBuilder = new StringBuilder();
                PacketStringBuilder.Append("<__InSite __encryption=\"2\" __version=\"1.1\"><__session><__connect><user>");//load in start of connection string
                PacketStringBuilder.Append("<__name>" + CamstarUsername + "</__name>");             //load in the username
                PacketStringBuilder.Append("</user>");
                PacketStringBuilder.Append("<password __encrypted=\"yes\">" + CamstarPassword + "</password>");//and password ( already encrypted check where it gets loaded from app config.)
                PacketStringBuilder.Append("</__connect></__session><__service __serviceType=\"ResourceThruput\">"
                    + "<__utcOffset><![CDATA[-04:00:00]]></__utcOffset><__inputData><MfgOrder>");
                PacketStringBuilder.Append("<__name>");                                             //<v^ load the Service base setup
                PacketStringBuilder.Append("<![CDATA[]]>");
                PacketStringBuilder.Append("</__name>");
                PacketStringBuilder.Append("</MfgOrder><Product>");
                PacketStringBuilder.Append("<__name>");
                PacketStringBuilder.Append("<![CDATA[" + receivedPacket["NAED"] + "]]>");           //load the NAED
                PacketStringBuilder.Append("</__name>");
                PacketStringBuilder.Append("<__useROR><![CDATA[true]]></__useROR>");
                PacketStringBuilder.Append("</Product><Qty>");
                PacketStringBuilder.Append("<![CDATA[" + receivedPacket["Good"] + "]]>");           //Load the Quantity
                PacketStringBuilder.Append("</Qty><Resource>");
                PacketStringBuilder.Append("<__name>");
                PacketStringBuilder.Append("<![CDATA[" + receivedPacket["Machine"] + "]]>");        //load the resource we are talking to
                PacketStringBuilder.Append("</__name>");
                PacketStringBuilder.Append("</Resource><ResourceGroup>");                           //load the rest of the hardcoded stuff.
                PacketStringBuilder.Append("<__name>");
                PacketStringBuilder.Append("<![CDATA[]]>");
                PacketStringBuilder.Append("</__name>");
                PacketStringBuilder.Append("</ResourceGroup><UOM>");
                PacketStringBuilder.Append("<__name>");
                PacketStringBuilder.Append("<![CDATA[EA]]>");
                PacketStringBuilder.Append("</__name>");
                PacketStringBuilder.Append("</UOM></__inputData>");                                 //dont forget the Execute. the packets you scrape from camstar are missing it but it is highly important to add one after service/input data.
                PacketStringBuilder.Append("<__perform><__eventName><![CDATA[GetWIPMsgs]]></__eventName>"
                    + "</__perform><__execute/><__requestData><CompletionMsg /><WIPMsgMgr><WIPMsgs><AcknowledgementRequired />"
                    + "<MsgAcknowledged /><MsgText /><PasswordRequired /><WIPMsgDetails /></WIPMsgs></WIPMsgMgr></__requestData></__service></__InSite>");
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(Sendmessage(CamstarIP, CamstarPort, PacketStringBuilder.ToString()), 2)); //send it and grab the data.
            }
            catch (Exception ex) { SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 2)); }
        }

        /// <summary>
        /// Camstar section of the Fifteen Minut Packet sends a throughput packet for the resource named for the machine.
        /// </summary>
        private void CamstarProductChangePacket(string message)
        {
            try
            {
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;      //convert to jobject
                StringBuilder PacketStringBuilder = new StringBuilder();                            //this builder will be used for the Camstar Packet
                PacketStringBuilder.Append("<__InSite __encryption=\"2\" __version=\"1.1\"><__session><__connect><user>");
                PacketStringBuilder.Append("<__name>" + CamstarUsername + "</__name>");             // Load defualt setup and insert Username
                PacketStringBuilder.Append("</user>");                                              //v and password
                PacketStringBuilder.Append("<password __encrypted=\"yes\">" + CamstarPassword + "</password></__connect></__session>");
                PacketStringBuilder.Append("<__service __serviceType=\"CollectResourceData\">"
                    + "<__utcOffset><![CDATA[-04:00:00]]></__utcOffset><__inputData><DataCollectionDef>"
                    + "<__name><![CDATA[HIL-Running-ProductType]]></__name><__useROR><![CDATA[true]]>"
                    + "</__useROR></DataCollectionDef><ParametricData __action=\"create\" __CDOTypeName=\"DataPointSummary\">"
                    + "<DataPointDetails><__listItem __listItemAction=\"add\" __CDOTypeName=\"DataPointDetails\"><DataPoint>"
                    + "<__name><![CDATA[ProductType]]></__name><__parent __CDOTypeName=\"UserDataCollectionDef\"><__Id>"
                    + "<![CDATA[001c6180000000ca]]></__Id><__name><![CDATA[HIL-Running-ProductType]]></__name><__useROR>"
                    + "<![CDATA[true]]></__useROR></__parent></DataPoint><DataType><![CDATA[4]]></DataType><DataValue>"
                    + "<![CDATA[" + receivedPacket["NAED"] + "]]></DataValue></__listItem></DataPointDetails>"
                    + "<OverrideDataPointLimits><![CDATA[True]]></OverrideDataPointLimits></ParametricData>"
                    + "<Resource><__name><![CDATA[" + receivedPacket["Machine"] + "]]></__name></Resource>"
                    + "</__inputData><__execute/><__perform><__eventName><![CDATA[GetWIPMsgs]]></__eventName>"
                    + "</__perform><__requestData><CompletionMsg /><WIPMsgMgr><WIPMsgs><AcknowledgementRequired />"
                    + "<MsgAcknowledged /><MsgText /><PasswordRequired /><WIPMsgDetails /></WIPMsgs></WIPMsgMgr>"
                    + "</__requestData></__service></__InSite>");
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(Sendmessage(CamstarIP, CamstarPort, PacketStringBuilder.ToString()), 2)); //cap it off with the rest of the Hardcoded info and the Machine Name and NAED.
            }
            catch (Exception ex) { SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 2)); }
        }

        /// <summary>
        /// SQL section of the Downtime Packet records downtime to the machine downtime table.
        /// </summary>
        private void SQLDownTimePacket(string message)
        {
            try                                                                                     //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;      //convert json to jobject
                StringBuilder sqlStringBuilder = new StringBuilder();                               //create a string builder to make the sql string
                string[] MachineAndLine = GetMachineIDAndLine(receivedPacket["Machine"].ToString());
                sqlStringBuilder.Append(" USE [EngDb-" + MachineAndLine[1] + "] ");                 //select Useing Line
                sqlStringBuilder.Append("INSERT INTO [" + receivedPacket["Machine"] + "DownTimes] (");//start loading the SQL Command
                List<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();       //gets list of all keys in json object
                string keySection = "";                                                             //contains the key section of the sql
                string valueSection = "";                                                           // contains the value section of the sql
                foreach (string key in keys)                                                        //foreach key
                {
                    if (key != "Machine" && key != "Status")                                       //except machine as it is used as the table name. and is instead entered as an id
                    {
                        keySection += key + ", ";                                                   //Make a key
                        valueSection += "@" + key + ", ";                                           //and value Reference to be replaced later
                    }
                }
                keySection += "MachineID , Timestamp ";                                             //add a machine ID and time key
                valueSection += MachineAndLine[0] + " ,@Timestamp ";                                //add a value section as well
                sqlStringBuilder.Append(keySection + ")");                                          //cap it off
                sqlStringBuilder.Append("values (" + valueSection + ");");                          //append both to the command string
                string SQLString = sqlStringBuilder.ToString();                                     //convert Builder to string
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    connection.Open();                                                              //open the connection
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {                                                                               //Command Time!
                        switch (Convert.ToInt32(receivedPacket["StatusCode"]))
                        {
                            case 0:
                                command.Parameters.AddWithValue("@StatusCode", "Unscheduled");      //replace perameters with values
                                break;

                            case 1:
                                command.Parameters.AddWithValue("@StatusCode", "Scheduled Down");   //replace perameters with values
                                break;

                            case 2:
                                command.Parameters.AddWithValue("@StatusCode", "Running");          //replace perameters with values
                                break;

                            case 3:
                                command.Parameters.AddWithValue("@StatusCode", "P/M");              //replace perameters with values
                                break;
                        }
                        command.Parameters.AddWithValue("@NAED", receivedPacket["NAED"].ToString());
                        command.Parameters.AddWithValue("@Code", Convert.ToInt32(receivedPacket["Code"]));
                        command.Parameters.AddWithValue("@MReason", receivedPacket["MReason"].ToString());
                        command.Parameters.AddWithValue("@UReason", receivedPacket["UReason"].ToString());
                        command.Parameters.AddWithValue("@Machine", receivedPacket["Machine"].ToString());
                        command.Parameters.AddWithValue("@Timestamp", DateTime.Now);                //add a timestamp
                        int rowsAffected = command.ExecuteNonQuery();                               //execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }
                }
            }
            catch (Exception ex)                                                                    //catch exceptions
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if connection crashed
                {
                    SNPService.ReastablishSQL(SQLDownTimePacket, message);                          //reastablish it
                }
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));           //else logit and move on
            }
        }

        private void QRQCDownTimePacket(string message)
        {
            DateTime HolderTime = DateTime.Now;
            try                                                                                     //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;      //convert json to jobjectif (Convert.ToInt32(receivedPacket["Status"]))
                Line line = LoadResources(receivedPacket["Machine"].ToString());                //generate a line from the machine name
                StringBuilder sqlStringBuilder = new StringBuilder();                           //create a string builder to make the sql string
                string[] temp = GetMachineIDAndLine(receivedPacket["Machine"].ToString());      //Grabs Machine in [0] and line in [1]
                sqlStringBuilder.Append(" USE [" + ConfigurationManager.AppSettings["QRQCDatabase"] + "] ");//select database
                sqlStringBuilder.Append("INSERT INTO [QRQC_Detail] (ResourceID,StatusID,StatusBegin,"
                + "ProductID,Thru,Goal) values (@ResourceID , @StatusID , @StatusBegin , @ProductID , @Thru , @Goal)");//start loading the SQL Command
                string SQLString = sqlStringBuilder.ToString();                                 //convert Builder to string
                string ResourceId = GetResourceID(receivedPacket["Machine"].ToString());
                string ProductID = GetProductId(receivedPacket["NAED"].ToString());
                int Thru = GetOutTheo(receivedPacket["NAED"].ToString(), line);
                int Goal = GetOutGoal(receivedPacket["NAED"].ToString(), line);
                //int Goal = Convert.ToInt32(GetTheo(receivedPacket["Machine"].ToString(), temp[1]));
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    connection.Open();                                                          //open the connection
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {
                        HolderTime = DateTime.Now;                                              //Command Time!
                        command.Parameters.AddWithValue("@StatusBegin", HolderTime);            //add a timestamp
                        command.Parameters.AddWithValue("@ResourceID", ResourceId);             //add rest of values
                        switch (Convert.ToInt32(receivedPacket["StatusCode"]))                  //convert status id to QRQC Status ID
                        {
                            case 0://Unscheduled
                                command.Parameters.AddWithValue("@StatusID", 0);
                                break;

                            case 1://scheduled
                                command.Parameters.AddWithValue("@StatusID", 2);
                                break;

                            case 2://Running
                                command.Parameters.AddWithValue("@StatusID", 0);
                                break;

                            case 3://PM
                                command.Parameters.AddWithValue("@StatusID", 1);
                                break;
                        }
                        command.Parameters.AddWithValue("@ProductID", ProductID);
                        command.Parameters.AddWithValue("@Thru", Thru);
                        command.Parameters.AddWithValue("@Goal", Goal);
                        int rowsAffected = command.ExecuteNonQuery();                           //execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }
                    UpdateQRQC(new Instructions(false, HolderTime, line));
                    SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Changed QRQC Status", 2));
                }
            }
            catch (Exception ex)                                                                    //catch exceptions
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if connection crashed
                {
                    SNPService.ReastablishSQL(SQLDownTimePacket, message);                          //reastablish it
                }
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));           //else logit and move on
            }
        }

        /// <summary>
        /// Camstar section of the Downtime Packet sends a downtime packet to camstar.
        /// </summary>
        private void CamstarDowntimePacket(string message)
        {
            try
            {
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                StringBuilder PacketStringBuilder = new StringBuilder();
                PacketStringBuilder.Append("<__InSite __encryption=\"2\" __version=\"1.1\"><__session><__connect><user>");
                PacketStringBuilder.Append("<__name>" + CamstarUsername + "</__name>");
                PacketStringBuilder.Append("</user>");
                PacketStringBuilder.Append("<password __encrypted=\"yes\">" + CamstarPassword + "</password>");
                PacketStringBuilder.Append("</__connect></__session><__service __serviceType="
                    + "\"ResourceSetupTransition\"><__utcOffset><![CDATA[-04:00:00]]></__utcOffset><__inputData><Availability><![CDATA[1]]></Availability><Comments><![CDATA[MReason:" + receivedPacket["MReason"] + " UReason:" + receivedPacket["UReason"] + "]]></Comments><Resource>");
                PacketStringBuilder.Append("<__name><![CDATA[" + receivedPacket["Machine"] + "]]></__name>");
                PacketStringBuilder.Append("</Resource><ResourceGroup><__name><![CDATA[]]></__name></ResourceGroup><ResourceStatusCode>");
                switch (Convert.ToInt32(receivedPacket["StatusCode"]))
                {
                    case 0:
                        PacketStringBuilder.Append("<__name><![CDATA[Unscheduled]]></__name>");     //if down send down
                        break;

                    case 1:
                        PacketStringBuilder.Append("<__name><![CDATA[Scheduled]]></__name>");        //Scheduled downtime
                        break;

                    case 2:
                        PacketStringBuilder.Append("<__name><![CDATA[Available]]></__name>");       //if running send running
                        break;

                    case 3:
                        PacketStringBuilder.Append("<__name><![CDATA[P/M]]></__name>");             //Preventive Maintenence
                        break;
                }
                PacketStringBuilder.Append("</ResourceStatusCode><ResourceStatusReason><__name><![CDATA[]]></__name>");
                PacketStringBuilder.Append("</ResourceStatusReason></__inputData ><__execute /><__requestData >"
                    + "<CompletionMsg /><ACEMessage /><ACEStatus /></__requestData ></__service ></__InSite >");
                string temp = Sendmessage(CamstarIP, CamstarPort, PacketStringBuilder.ToString());
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(temp, 2));
            }
            catch (Exception ex) { SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 2)); }
        }

        /// <summary>
        ///  Packet sent at each index to sql records it to the resource table.
        /// </summary>
        private void SQLShortTimeStatisticPacket(string message)
        {
            try                                                                                     //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;      //convert it to a jobject
                StringBuilder sqlStringBuilder = new StringBuilder();                               //string builder to create the sql string
                string[] MachineAndLine = GetMachineIDAndLine(receivedPacket["Machine"].ToString());
                sqlStringBuilder.Append(" USE [EngDb-" + MachineAndLine[1] + "] ");                 //select database
                sqlStringBuilder.Append("INSERT INTO [" + receivedPacket["Machine"].ToString() + "ShortTimeStatistics] (");//start building SQL string
                List<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();       //gets list of all keys in json object
                string keySection = "";                                                             //stores the key section of the sql
                string valueSection = "";                                                           //stores the value section of the sql
                foreach (string key in keys)                                                        //foreach key
                {
                    if (key != "Machine")                                                           //except machine as it is used as the table name. and is loaded as an id
                    {
                        keySection += "[" + key + "], ";                                            //Make a key
                        valueSection += "@" + key + ", ";                                           //and value Reference to be replaced later
                    }
                }
                keySection += "[MachineID], [Timestamp], [Input] ";                                  //add a machineIDsection and timestamp
                valueSection += MachineAndLine[0] + ", @Timestamp, @Input ";                         //add to value section to
                sqlStringBuilder.Append(keySection + ")");                                          //cap it off
                sqlStringBuilder.Append("values ( " + valueSection + ");");                         //append both to the command string
                string SQLString = sqlStringBuilder.ToString();                                     //Convert builder to sql string
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    connection.Open();                                                              //open the connection
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {
                        command.Parameters.AddWithValue("@Input", 1 == Convert.ToInt32(receivedPacket["Attempt"]));//Comand Time!
                        foreach (string key in keys)                                                //foreach key
                        {
                            if (key != "Machine")                                                   //Except Machine
                                if (key != "Head_number")                                           //and head number
                                {                                                                   //convert to bool
                                    command.Parameters.AddWithValue("@" + key, 1 == Convert.ToInt32(receivedPacket[key]));
                                }
                                else                                                                //if it is a head number add it as an int
                                    command.Parameters.AddWithValue("@" + key, Convert.ToInt32(receivedPacket[key]));
                        }
                        command.Parameters.AddWithValue("@Timestamp", DateTime.Now);                //add a timestamp
                        command.Parameters.AddWithValue("@Machine", receivedPacket["Machine"].ToString());//add teh machine name
                        int rowsAffected = command.ExecuteNonQuery();                               // execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }
                }
            }
            catch (Exception ex)                                                                    //catch exceptions
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if connection crashed
                {
                    SNPService.ReastablishSQL(SQLShortTimeStatisticPacket, message);                //reestablish it
                }
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));           //log it
            }
        }

        ///// <summary>
        /////  Packet sent at each index to MDE over UDP Deprecieted currently as MDE has been deprecieted
        ///// </summary>
        //private void MDEShortTimeStatisticPacket(string message)
        //{
        //    try
        //    {
        //        List<byte> bySNPoSend = new List<byte>();                                         //make a byte array for the packet to be sent
        //        List<bool> bits = new List<bool>();                                               //make a bool array to convert to each bit in the byte array
        //        string jsonString = message.Substring(7, message.Length - 7);                     //grab json data from the end.
        //        JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;    //convert to jobject
        //        List<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();     //gets list of all keys in json object
        //        keys.Sort();                                                                      //Make sure all keys are alphabetical for easeir documentation
        //        foreach (string key in keys)                                                      //foreach key
        //        {
        //            if (key != "Machine" && key != "Head_number")                                 //that isnt HeadNumber or Machine name
        //                bits.Add(Convert.ToInt32(receivedPacket[key] ?? 0) == 1);                 //if the key's value is null set bit to false, otherwise set it to the bit.
        //        }
        //        bySNPoSend.Add((byte)'~');                                                        //add MDE Header byte
        //        for (int Index = 0; Index < bits.Count; Index += 8)                               //foreach group of 8 bools in bits
        //        {
        //            bool[] Bools;                                                                 //generate a new bool array
        //            if (bits.Count - Index >= 8)                                                  //if there are atleast 8 bools left
        //            {
        //                Bools = new bool[8];                                                      //make the bool array length of 8
        //                Array.Copy(bits.ToArray(), Index, Bools, 0, 8);                           //Copy the 8 bits we are on into the new bool array
        //            }
        //            else
        //            {                                                                             //else if less than 8
        //                Bools = new bool[bits.Count - Index];                                     //make the bool array the size of the remaining bits
        //                Array.Copy(bits.ToArray(), Index, Bools, 0, bits.Count - Index);          //copy remaining bits to array
        //            }
        //            bySNPoSend.Add(ConvertBoolArrayToByteLeftJustified(Bools));                   //turn the bits into a byte Left justified (true true turns to 11000000)
        //        }
        //        string Theo = Convert.ToString(receivedPacket["Theo"]);                           //get theorectical from packet
        //        bySNPoSend.Add((byte)Convert.ToInt32(receivedPacket["Head_number"]));             //add the head number to the bytes to send
        //        for (int x = 0; x < Theo.Length; x++)                                             //add each character of the theoretical length to the packet
        //        {
        //            bySNPoSend.Add((byte)Theo[x]);                                                //add the character
        //        }
        //        bySNPoSend.Add((byte)10);                                                         //new line end packet character
        //        MDEClient.Send(bySNPoSend.ToArray(), bySNPoSend.Count, MDEIP, MDEClientPort);     //UnComment if your going to use it it will stop build otherwise however
        //    }
        //    catch (Exception ex)                                                                  //catch exceptions
        //    {
        //        SNPService. DiagnosticItems.Enqueue (new DiagnosticItem(ex.ToString(), 1);        //logit and move on
        //    }
        //}

        #endregion Packet Section

        #region Connections/Resources/Misc

        /// <summary>
        /// Send message To Camstar and listen for a message back.
        /// </summary>
        public string Sendmessage(string host, int port, string content)
        {
            ServerConnection connection = new ServerConnection();
            //create a server connection
            try
            {
                var connected = connection.Connect(host, port);                                     // try connecting on the host and port passed in
                if (!connected) return "";                                                          // return nothing if cant connect
                connection.Send(content);                                                           // send data
                connection.Receive(out var result);                                                 // reviece message from server, and store into variable
                connection.Disconnect();                                                            // Close connection
                try
                {
                    return XDocument.Parse(result).ToString();                                      // format recieved message into xml
                }
                catch
                {
                    return result;                                                                  // if formatting fails just send unformatted back
                }
            }
            catch (Exception ex)                                                                    // If an error occurred return null string
            {
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));           //logit
                return "";                                                                          //return null string
            }
        }

        /// <summary>
        /// Convert up to a byte to 8 bools
        /// </summary>
        private bool[] ConvertByteToBoolArray(byte b)
        {
            bool[] result = new bool[8];                                                            //bool array to return
            for (int i = 0; i < 8; i++)                                                             // check each bit in the byte.
                result[i] = (b & (1 << i)) == 0 ? false : true;                                     //if 1 set to true, if 0 set to false
            Array.Reverse(result);                                                                  // reverse the array
            return result;                                                                          //return the result
        }

        /// <summary>
        /// Convert up to 8 bools to 1 byte right justfied 0001111
        /// </summary>
        private byte ConvertBoolArrayToByteRightJustified(bool[] source)
        {
            byte result = 0;                                                                        //result to return
            int index = 8 - source.Length;                                                          // This assumes the array never contains more than 8 elements!
            foreach (bool b in source)                                                              //foreach bool in the bool array passed in
            {
                if (b)                                                                              // if the element is 'true'
                    result |= (byte)(1 << (7 - index));                                             //set the bit at that position
                index++;                                                                            //increment the position
            }
            return result;                                                                          //return the result
        }

        /// <summary>
        /// From Machine name get Line and machine ID
        /// </summary>
        private string[] GetMachineIDAndLine(string Machine)
        {
            try
            {
                string[] result = new string[2] { "", "" };                                         //result to return
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append(" USE [" + ConfigurationManager.AppSettings["ENGDBDatabase"] + "] ");//select database
                sqlStringBuilder.Append("select MachineID, Line from MachineInfoTable where MachineName='" + Machine + "';");  //start loading the command into the string
                string SQLString = sqlStringBuilder.ToString();                                     //Convert Builder to string
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    connection.Open();                                                              //open the connection
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {                                                                               //Comand Time!
                        using (IDataReader dr = command.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                result[0] = dr[0].ToString();                                       //read the machine
                                result[1] = dr[1].ToString();                                       //and line into the result array.
                            }
                        }
                    }
                    return result;                                                                  //return that result
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("There is already an open DataReader"))
                {
                    Thread.Sleep(100);
                    return GetMachineIDAndLine(Machine);
                }
                else
                    SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));       //if not handled log it and move on
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
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    connection.Open();                                                              //open the connection
                    using (SqlCommand command = new SqlCommand("SELECT name from sys.databases where name='EngDb-" + Line + "'", connection))
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
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("There is already an open DataReader"))
                {
                    Thread.Sleep(100);
                    return CheckForDatabase(Line);
                }
                else
                    SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));       //if not handled log it and move on
                return true;
            }
        }

        /// <summary>
        /// Convert up to 8 bools to 1 byte right justfied 11110000
        /// </summary>
        private byte ConvertBoolArrayToByteLeftJustified(bool[] source)
        {
            byte result = 0;                                                                        //result to return
            int index = 0;                                                                          //index
            foreach (bool b in source)                                                              //foreach bool in the bools passed in
            {
                if (b)                                                                              // if the element is 'true'
                    result |= (byte)(1 << (7 - index));                                             //set the bit at that position
                index++;                                                                            //increment the position
            }
            return result;                                                                          //return result
        }

        public Line LoadResources(string machine)                                                   //returns true if at least one loaded
        {
            try
            {
                string query = "SELECT * FROM [QRQC].[dbo].[QRQC_Config_view] Where ResourceName ='" + machine + "';";// select the line from the config view where it is the machine were looking for
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    connection.Open();                                                              //open the connection
                    SqlCommand command = new SqlCommand(query, connection);                         //create a command from the query and connection
                    using (SqlDataReader reader = command.ExecuteReader())                          //Command Time!
                    {
                        while (reader.Read())                                                       //while we are still reading values ( should only grab one but just incase.
                        {
                            Line ReadLine = new Line();                                             //this will hold the line read from sql.
                            ReadLine.DisplayName = reader.GetString(0);                             //grab the display name ( HIL-XS-Fim for example
                            ReadLine.Automatic = reader.GetBoolean(1);                              //weather the line is automatic ( if we are using it it is)
                            ReadLine.GoodProductPerEntry = reader.GetInt16(2);                      //not sure but it needs to be set to talk to the QRQC Service
                            ReadLine.GoodProductSQLVar = reader.GetString(3);                       //""
                            ReadLine.TableName = reader.GetString(4);                               //grab the table name. its the name of the table the data for the line is located in
                            ReadLine.CStart = reader.GetInt32(5);                                   // Shift C Start time
                            ReadLine.CEnd = reader.GetInt32(6);                                     //ShiftC End time
                            ReadLine.AStart = reader.GetInt32(7);                                   //Shift A start time
                            ReadLine.AEnd = reader.GetInt32(8);                                     //shift a end time
                            ReadLine.BStart = reader.GetInt32(9);                                   //shift b start time
                            ReadLine.BEnd = reader.GetInt32(10);                                    //shift b end time
                            ReadLine.Name = reader.GetString(11);                                   //name of the machine ( this is the resource ID
                            ReadLine.ProductInputSQLVar = reader.GetString(12);                     //""
                            ReadLine.isFinalMachine = reader.GetBoolean(13);                        //weather this machine is the final on the line.
                            ReadLine.FUDGE = reader.GetDouble(14);                                  //used to make the graph look slightly worse to workers and better to administrators.
                            return ReadLine;                                                        //return the new line
                        }
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if connection crashed
                {
                    SNPService.ReastablishSQL(SNPService.DoNothing, machine);                                  //reestablish it
                    return LoadResources(machine);
                }
                return null;
            }
        }

        //Below is QRQC Code. AnyChanges i made broke it so im not happy with what it looks like but im not sure how to fix it...
        public void UpdateQRQC(Instructions i)
        {
            try
            {
                JavaScriptSerializer jss = new JavaScriptSerializer();
                string serializedObject = jss.Serialize(i);                                         //serialize the object for trasmission
                string SERVERIP = ConfigurationManager.AppSettings["QRQC_Service_SERVERIP"];        //grab the server ip from config
                IPAddress ipAddress = IPAddress.Parse(SERVERIP);                                    //parse the ip
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, 61752);                             //
                Socket sender = new Socket(ipAddress.AddressFamily,                                 // Create a TCP/IP  socket.
                    SocketType.Stream, ProtocolType.Tcp);                                           //
                sender.Connect(remoteEP);                                                           // Connect the socket to the remote endpoint.
                byte[] msg = Encoding.ASCII.GetBytes(serializedObject + "<EOF>");                   // Encode the data string into a byte array.
                sender.Send(msg);                                                                   //send the data and dispose of and close the conection
                sender.Shutdown(SocketShutdown.Both);
                sender.Close();
            }
            catch (Exception ex)
            {
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));           //log it
            }
        }

        /// <summary>
        /// Converts Naed string to MDE/MDI ProductID string
        /// </summary>
        public string GetProductFamilyId(string ProductName)
        {
            string ProductFamilyId = "";                                                            //initialize as empty
            string productTable = ConfigurationManager.AppSettings["camProductTable"];              //this is the table that stores all product information
            string productBaseTable = ConfigurationManager.AppSettings["camProductBaseTable"];      //this is the table for the bases
            string query = "SELECT ProductFamilyId FROM " + productTable + " WHERE ProductBaseId=(SELECT "
                    + "ProductBaseId FROM " + productBaseTable + " WHERE ProductName='" + ProductName + "')";//select the product id where the product name is correct
            using (SqlConnection con = new SqlConnection())                                         //create the connection
            {
                con.ConnectionString = ConfigurationManager.AppSettings["DBCamstarConnectionString"] + "User Id= camstaruser; Password= c@mst@rus3r;";
                con.Open();
                try
                {
                    using (SqlCommand command = new SqlCommand(query, con))                         //submit command
                    {
                        using (SqlDataReader reader = command.ExecuteReader())                      //read the values back
                        {
                            if (reader.Read())
                            {
                                if (!reader.IsDBNull(0))                                            //if not null
                                {
                                    ProductFamilyId = reader.GetString(0);                          //this is the ID for the NAED
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));       //logit
                }
            }
            return ProductFamilyId;                                                                 //were done!
        }

        public int GetOutTheo(string ProductName, Line Line) //gets hour theoretical/thru of product/family/line in that order of importance
        {
            double t = 0; //we'll call this a timespan for now

            string speedTable = ConfigurationManager.AppSettings["speedTable"];

            string query = "SELECT * FROM " + speedTable + " WHERE ResourceName='" + Line.DisplayName + "' AND ProductId='" + GetProductId(ProductName) + "'";

            using (SqlConnection con = new SqlConnection())
            {
                con.ConnectionString = ConfigurationManager.AppSettings["QRQCConnectionString"];
                con.Open();
                try
                {
                    SqlCommand command = new SqlCommand(query, con);
                    SqlDataReader reader = command.ExecuteReader();

                    if (reader.HasRows)
                    {
                        reader.Read();
                        t = reader.GetDouble(4);
                    }
                    else
                    {
                        reader.Close();
                        query = "SELECT * FROM " + speedTable + " WHERE ResourceName='" + Line.DisplayName + "' AND ProductFamilyId='" + GetProductFamilyId(ProductName) + "'";
                        SqlCommand command2 = new SqlCommand(query, con);
                        SqlDataReader reader2 = command2.ExecuteReader();
                        if (reader2.HasRows)
                        {
                            reader2.Read();
                            t = reader2.GetDouble(4);
                        }
                        else
                        {
                            reader2.Close();
                            query = "SELECT * FROM " + speedTable + "WHERE ResourceName='" + Line.DisplayName + "'";
                            SqlCommand command3 = new SqlCommand(query, con);
                            SqlDataReader reader3 = command3.ExecuteReader();
                            if (reader3.HasRows)
                            {
                                reader3.Read();
                                t = reader3.GetDouble(4);
                                reader3.Close();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));
                }
            }

            return Convert.ToInt32(t);
        }

        public string GetProductId(string ProductName)
        {
            string id = "";                                                                         //initialize empty
            string dbTable = ConfigurationManager.AppSettings["QRQC_ProductNameId_view"];           //grab table from app config
            string query = "SELECT ProductId FROM " + dbTable + " WHERE ProductName='" + ProductName + "'"; //select the Product id where the name is the same
            using (SqlConnection con = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
            {
                con.Open();                                                                         //Make and open conection
                try
                {
                    using (SqlCommand command = new SqlCommand(query, con))                         //start

                    {
                        using (SqlDataReader reader = command.ExecuteReader())                      //comand time!
                        {
                            if (reader.Read())
                            {
                                id = reader.GetString(0);                                           //grab the product ID
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));       //Log It
                }
            }
            return id;
        }

        public int GetOutGoal(string ProductName, Line Line) //gets goal of product/family/line in that order of importance
        {
            double t = 0;

            string speedTable = System.Configuration.ConfigurationManager.AppSettings["speedTable"];

            string query = "SELECT * FROM " + speedTable + " WHERE ResourceName='" + Line.DisplayName + "' AND ProductId='" + GetProductId(ProductName) + "'";

            using (SqlConnection con = new SqlConnection())
            {
                con.ConnectionString = ConfigurationManager.AppSettings["QRQCConnectionString"];
                con.Open();
                try
                {
                    SqlCommand command = new SqlCommand(query, con);
                    SqlDataReader reader = command.ExecuteReader();

                    if (reader.HasRows)
                    {
                        reader.Read();
                        t = reader.GetDouble(3);
                    }
                    else
                    {
                        reader.Close();
                        query = "SELECT * FROM " + speedTable + " WHERE ResourceName='" + Line.DisplayName + "' AND ProductFamilyId='" + GetProductFamilyId(ProductName) + "'";
                        SqlCommand command2 = new SqlCommand(query, con);
                        SqlDataReader reader2 = command2.ExecuteReader();
                        if (reader2.HasRows)
                        {
                            reader2.Read();
                            t = reader2.GetDouble(3);
                        }
                        else
                        {
                            reader2.Close();
                            query = "SELECT * FROM " + speedTable + "WHERE ResourceName='" + Line.DisplayName + "'";
                            SqlCommand command3 = new SqlCommand(query, con);
                            SqlDataReader reader3 = command3.ExecuteReader();
                            if (reader3.HasRows)
                            {
                                reader3.Read();
                                t = reader3.GetDouble(3);
                                reader3.Close();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));
                }
            }

            return Convert.ToInt32(t);
        }

        public string GetResourceID(string ResourceName)
        {
            string resourceId = "";                                                                     //stores the return value
            string sql = "SELECT ResourceId FROM [QRQC].[dbo].[CAMSTAR_Resources] WHERE ResourceName='" + ResourceName + "'";
            using (SqlConnection con = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))  //^ query to select the resource id from resource display name.
            {
                con.Open();                                                                             //open a connection to the ENGineering DB
                using (SqlCommand command = new SqlCommand(sql, con))                                   //creat the command
                {
                    using (SqlDataReader reader = command.ExecuteReader())                              //execute the command
                    {
                        while (reader.Read())                                                           //read the values into the resourceID
                        {
                            if (!reader.IsDBNull(0))
                            {
                                resourceId = (string)reader[0];
                            }
                        }
                    }
                }
            }
            return resourceId;                                                                          //and return
        }

        #endregion Connections/Resources/Misc
    }
}