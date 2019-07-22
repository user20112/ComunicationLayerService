using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualVersionofService
{
    internal class EMPPackets
    {
        #region Variable Section

        private Form1 MainForm;
        public const string TopicName = "SNP.Outbound";
        public TopicPublisher Publisher;

        public EMPPackets(Form1 mainform)
        {
            MainForm = mainform;
        }

        #endregion Variable Section

        #region Packet Section

        /// <summary>
        /// Packet Sent every index for the EMP system. Simply insert into SQL for recording ( and grab a time stamp if missing)
        /// </summary>
        public void IndexPacket(string message)
        {
            MainForm.DiagnosticOut("EMPIndexPacketReceived!", 2);
            string SQLString = "";
            try //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append("INSERT INTO EMPTable (");
                IList<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();//gets list of all keys in json object
                string keySection = "";
                string valueSection = "";
                bool MissingStamp = true;
                foreach (string key in keys)//foreach key
                {
                    keySection += key + ", ";//Make a key
                    valueSection += "@" + key + ", ";//and value Reference to be replaced later
                    if (key == "TimeStamp")
                    {
                        MissingStamp = false;
                    }
                }
                if (MissingStamp)
                {
                    keySection += "TimeStamp";//Make a Time key
                    valueSection += "@TimeStamp";//and value Reference to be replaced later
                }
                else
                {
                    valueSection = valueSection.Substring(0, valueSection.Length - 2);
                    keySection = keySection.Substring(0, keySection.Length - 2);
                }
                sqlStringBuilder.Append(keySection + " )");
                sqlStringBuilder.Append("Values ( " + valueSection + " );");//append both to the command string
                SQLString = sqlStringBuilder.ToString();//convert to string
                using (SqlCommand command = new SqlCommand(SQLString, MainForm.ENGDBConnection))
                {
                    foreach (string key in keys)//foreach key
                    {
                        switch (key)
                        {
                            case "Temperature":
                                command.Parameters.AddWithValue("@" + key, Convert.ToDecimal(receivedPacket[key]));
                                break;

                            case "Humidity":
                                command.Parameters.AddWithValue("@" + key, Convert.ToDecimal(receivedPacket[key].ToString()));
                                break;

                            case "FlowRate":
                                command.Parameters.AddWithValue("@" + key, Convert.ToDecimal((receivedPacket[key])));
                                break;

                            case "ChangeOver5Seconds":
                                command.Parameters.AddWithValue("@" + key, Convert.ToDecimal(receivedPacket[key]));
                                break;

                            case "TimeStamp":
                                if (MissingStamp)
                                    command.Parameters.AddWithValue("@" + key, DateTime.Now);
                                else
                                {
                                    string TimeStamp = receivedPacket[key].ToString();
                                    int year = 2000 + Convert.ToInt32(TimeStamp.Substring(0, 2));
                                    int month = Convert.ToInt32(TimeStamp.Substring(3, 2));
                                    int day = Convert.ToInt32(TimeStamp.Substring(6, 2));
                                    int hour = Convert.ToInt32(TimeStamp.Substring(9, 2));
                                    int minute = Convert.ToInt32(TimeStamp.Substring(12, 2));
                                    int second = Convert.ToInt32(TimeStamp.Substring(15, 2));
                                    command.Parameters.AddWithValue("@" + key, new DateTime(year, month, day, hour, minute, second));
                                }
                                break;

                            case "Location":
                                command.Parameters.AddWithValue("@" + key, receivedPacket[key].ToString());
                                break;

                            default:
                                break;
                        }
                    }
                    int rowsAffected = command.ExecuteNonQuery();// execute the command returning number of rows affected
                    MainForm.DiagnosticOut(rowsAffected + " row(s) inserted", 2);//logit
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))
                {
                    MainForm.ReastablishSQL(IndexPacket, message);
                }
                MainForm.DiagnosticOut(ex.ToString(), 1);
            }
        }

        /// <summary>
        /// Packet Sent When there are extremes in either temperature humidity or flowrate/presure
        /// </summary>
        public void WarningPacket(string message)
        {
            MainForm.DiagnosticOut("EMPWarningPacketReceived!", 3);
            Task.Run(() => SQLEMPWarningPacket(message));
            Task.Run(() => MQTTEMPWarningPacket(message));
        }

        /// <summary>
        /// SQL Section of the EMP Warning Packet
        /// </summary>
        private void SQLEMPWarningPacket(string message)
        {
            string SQLString = "";
            try //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append("INSERT INTO EMPWarningTable (");
                IList<string> keys = receivedPacket.Properties().Select(p => p.Name).ToList();//gets list of all keys in json object
                string keySection = "";
                string valueSection = "";
                bool MissingStamp = true;
                foreach (string key in keys)//foreach key
                {
                    keySection += key + ", ";//Make a key
                    valueSection += "@" + key + ", ";//and value Reference to be replaced later
                    if (key == "TimeStamp")
                    {
                        MissingStamp = false;
                    }
                }
                if (MissingStamp)
                {
                    keySection += "TimeStamp";//Make a Time key
                    valueSection += "@TimeStamp";//and value Reference to be replaced later
                }
                else
                {
                    valueSection = valueSection.Substring(0, valueSection.Length - 2);
                    keySection = keySection.Substring(0, keySection.Length - 2);
                }
                sqlStringBuilder.Append(keySection + " )");
                sqlStringBuilder.Append("Values ( " + valueSection + " );");//append both to the command string
                SQLString = sqlStringBuilder.ToString();//convert to string
                using (SqlCommand command = new SqlCommand(SQLString, MainForm.ENGDBConnection))
                {
                    foreach (string key in keys)//foreach key
                    {
                        switch (key)
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
                                    string TimeStamp = receivedPacket[key].ToString();
                                    int year = 2000 + Convert.ToInt32(TimeStamp.Substring(0, 2));
                                    int month = Convert.ToInt32(TimeStamp.Substring(3, 2));
                                    int day = Convert.ToInt32(TimeStamp.Substring(6, 2));
                                    int hour = Convert.ToInt32(TimeStamp.Substring(9, 2));
                                    int minute = Convert.ToInt32(TimeStamp.Substring(12, 2));
                                    int second = Convert.ToInt32(TimeStamp.Substring(15, 2));
                                    command.Parameters.AddWithValue("@" + key, new DateTime(year, month, day, hour, minute, second));
                                }
                                break;

                            case "Location":
                                command.Parameters.AddWithValue("@" + key, receivedPacket[key].ToString());
                                break;

                            default:
                                break;
                        }
                    }
                    int rowsAffected = command.ExecuteNonQuery();// execute the command returning number of rows affected
                    MainForm.DiagnosticOut(rowsAffected + " row(s) inserted", 2);//logit
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))
                {
                    MainForm.ReastablishSQL(WarningPacket, message);
                }
                MainForm.DiagnosticOut(ex.ToString(), 1);
            }
        }

        /// <summary>
        /// MQTT Section of the EMP Warning Packet.
        /// </summary>
        private void MQTTEMPWarningPacket(string message)
        {
            Publisher.SendMessage(message);//forward it to the warning topic
        }

        #endregion Packet Section
    }
}