using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SNPService.Comunications;
using SNPService.Resources;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNPService.Packets
{
    internal class EMPPackets
    {
        #region Variable Section

        public const string TopicName = "SNP.Outbound";
        public TopicPublisher Publisher;

        public EMPPackets()
        {
            Dictionary<int, Action<string>> EMPDictionary = new Dictionary<int, Action<string>>();
            EMPDictionary.Add(1, (Action<string>)IndexPacket);
            EMPDictionary.Add(2, (Action<string>)WarningPacket);
            SNPService.Packets.Add(2, EMPDictionary);
        }

        #endregion Variable Section

        #region Packet Section

        /// <summary>
        /// Packet Sent When there are extremes in either temperature humidity or flowrate/presure
        /// </summary>
        public void WarningPacket(string message)
        {
            SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("EMPWarningPacketReceived!", 3)); //log it
            Task.Run(() => SQLEMPWarningPacket(message));                                           //run it
            Task.Run(() => MQTTEMPWarningPacket(message));                                          //send it
        }

        /// <summary>
        /// Packet Sent every index for the EMP system. Simply insert into SQL for recording ( and grab a time stamp if missing)
        /// </summary>
        public void IndexPacket(string message)
        {
            SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("EMPIndexPacketReceived!", 2));   //log the packet being received in Diagnostic.
            try                                                                                     //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;      //Convert json to jobject
                StringBuilder sqlStringBuilder = new StringBuilder();                               //string builder used to build sql string
                sqlStringBuilder.Append("INSERT INTO EMPTable (");                                  //start building sql
                List<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();       //gets list of all keys in json object
                string keySection = "";                                                             //stores the key section of the sql
                string valueSection = "";                                                           //stores the value section of the sql
                bool MissingStamp = true;                                                           //used to tell weather or not a timestamp was passed in
                foreach (string key in keys)                                                        //foreach key
                {
                    keySection += key + ", ";                                                       //Make a key
                    valueSection += "@" + key + ", ";                                               //and value Reference to be replaced later
                    if (key == "TimeStamp")                                                         // if we receive a time stamp key we are receiving a timestamp
                        MissingStamp = false;
                }
                if (MissingStamp)                                                                   //if we didnt receive a timestamp
                {
                    keySection += "TimeStamp";                                                      //Make a Time key
                    valueSection += "@TimeStamp";                                                   //and value Reference to be replaced later
                }
                else
                {
                    valueSection = valueSection.Substring(0, valueSection.Length - 2);              //if we have extra data at the end remove it
                    keySection = keySection.Substring(0, keySection.Length - 2);                    //from the key section to
                }
                sqlStringBuilder.Append(keySection + " )");                                         //cap of the key section
                sqlStringBuilder.Append("Values ( " + valueSection + " );");                        //append value section to the command string
                string SQLString = sqlStringBuilder.ToString();                                     //convert vuilder to string
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    connection.Open();                                                              //open the connection
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {                                                                               //Comand Time!
                        foreach (string key in keys)                                                //foreach key
                        {
                            switch (key)
                            {
                                case "Temperature":                                                 //if we have the Temperature convert to decimal and update
                                    command.Parameters.AddWithValue("@" + key, Convert.ToDecimal(receivedPacket[key]));
                                    break;

                                case "Humidity":                                                    //if we have the Humidity convert to decimal and update
                                    command.Parameters.AddWithValue("@" + key, Convert.ToDecimal(receivedPacket[key].ToString()));
                                    break;

                                case "FlowRate":                                                    //if we have the FlowRate convert to decimal and update
                                    command.Parameters.AddWithValue("@" + key, Convert.ToDecimal((receivedPacket[key])));
                                    break;

                                case "ChangeOver5Seconds":                                          //if we have the ChangeOver5Seconds convert to decimal and update
                                    command.Parameters.AddWithValue("@" + key, Convert.ToDecimal(receivedPacket[key]));
                                    break;

                                case "TimeStamp":                                                   //if we have the TimeStamp convert to datetime and update
                                    string TimeStamp = receivedPacket[key].ToString();
                                    int year = 2000 + Convert.ToInt32(TimeStamp.Substring(0, 2));   //grab the Year
                                    int month = Convert.ToInt32(TimeStamp.Substring(3, 2));         //month
                                    int day = Convert.ToInt32(TimeStamp.Substring(6, 2));           //day
                                    int hour = Convert.ToInt32(TimeStamp.Substring(9, 2));          //hour
                                    int minute = Convert.ToInt32(TimeStamp.Substring(12, 2));       //minute
                                    int second = Convert.ToInt32(TimeStamp.Substring(15, 2));       //second from the passed packet
                                    command.Parameters.AddWithValue("@" + key, new DateTime(year, month, day, hour, minute, second));
                                    break;

                                case "Location":                                                    //if we have the Location update it
                                    command.Parameters.AddWithValue("@" + key, receivedPacket[key].ToString());
                                    break;

                                default:
                                    break;
                            }
                        }
                        if (MissingStamp)
                            command.Parameters.AddWithValue("@TimeStamp", DateTime.Now);            //grab the timestamp if missing
                        int rowsAffected = command.ExecuteNonQuery();                               // execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }
                }
            }
            catch (Exception ex)                                                                    //catch exceptions
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if connection broke
                {
                    SNPService.ReastablishSQL(IndexPacket, message);                                //reastablish it
                }
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));           //so if you could log this that would be greaaaaaaat
            }
        }

        /// <summary>
        /// SQL Section of the EMP Warning Packet
        /// </summary>
        private void SQLEMPWarningPacket(string message)
        {
            string SQLString = "";
            try //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;      //convert to jobject
                StringBuilder sqlStringBuilder = new StringBuilder();                               //make sql string builder
                sqlStringBuilder.Append("INSERT INTO EMPWarningTable (");                           //start making sql string
                List<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();       //gets list of all keys in json object
                string keySection = "";                                                             //stores the key section of the sql
                string valueSection = "";                                                           //stores the value section of the sql
                bool MissingStamp = true;                                                           //records weather we got a time stamp or not
                foreach (string key in keys)                                                        //foreach key
                {
                    keySection += key + ", ";                                                       //Make a key
                    valueSection += "@" + key + ", ";                                               //and value Reference to be replaced later
                    if (key == "TimeStamp")                                                         //if we get a TimeStamp record it
                    {
                        MissingStamp = false;
                    }
                }
                if (MissingStamp)                                                                   //If we dont
                {
                    keySection += "TimeStamp";                                                      //Make a Time key
                    valueSection += "@TimeStamp";                                                   //and value Reference to be replaced later
                }
                else
                {
                    valueSection = valueSection.Substring(0, valueSection.Length - 2);              //Next Remove Extra characters
                    keySection = keySection.Substring(0, keySection.Length - 2);                    //Next Remove Extra characters
                }
                sqlStringBuilder.Append(keySection + " )");                                         //and append/capoff the strings
                sqlStringBuilder.Append("Values ( " + valueSection + " );");
                SQLString = sqlStringBuilder.ToString();                                            //convert to string
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    connection.Open();                                                              //open the connection
                    using (SqlCommand command = new SqlCommand(SQLString, connection))
                    {                                                                               //Command  Time Woo!
                        foreach (string key in keys)                                                //foreach key
                        {
                            switch (key)                                                            //replace the values
                            {
                                case "Warning":
                                    command.Parameters.AddWithValue("@" + key, receivedPacket[key].ToString());
                                    break;

                                case "Urgency":
                                    command.Parameters.AddWithValue("@" + key, Convert.ToInt32(receivedPacket[key]));
                                    break;

                                case "TimeStamp":
                                    if (MissingStamp)
                                        command.Parameters.AddWithValue("@" + key, DateTime.Now);
                                    else
                                    {
                                        string TimeStamp = receivedPacket[key].ToString();              //grab the string
                                        int year = 2000 + Convert.ToInt32(TimeStamp.Substring(0, 2));   //and break it up into
                                        int month = Convert.ToInt32(TimeStamp.Substring(3, 2));         //month
                                        int day = Convert.ToInt32(TimeStamp.Substring(6, 2));           //day
                                        int hour = Convert.ToInt32(TimeStamp.Substring(9, 2));          //hour
                                        int minute = Convert.ToInt32(TimeStamp.Substring(12, 2));       //minute
                                        int second = Convert.ToInt32(TimeStamp.Substring(15, 2));       //second
                                        command.Parameters.AddWithValue("@" + key, new DateTime(year, month, day, hour, minute, second));//replace paramet with value
                                    }
                                    break;

                                case "Location":
                                    command.Parameters.AddWithValue("@" + key, receivedPacket[key].ToString());
                                    break;

                                default:
                                    break;
                            }
                        }
                        int rowsAffected = command.ExecuteNonQuery();                               // execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if the connection crashed restablish it
                {
                    SNPService.ReastablishSQL(SQLEMPWarningPacket, message);
                }
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));            //logit
            }
        }

        /// <summary>
        /// MQTT Section of the EMP Warning Packet.
        /// </summary>
        private void MQTTEMPWarningPacket(string message)
        {
            Publisher.SendMessage(message);                                                             //forward it to the warning topic
        }

        #endregion Packet Section
    }
}