using Camstar.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SNPService.Resources;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Xml.Linq;

namespace SNPService.Packets
{
    internal class GenericPackets
    {
        public GenericPackets()
        {
            Dictionary<int, Action<string>> GenericDictionary = new Dictionary<int, Action<string>>();
            GenericDictionary.Add(1, (Action<string>)RunSQLCommand);
            GenericDictionary.Add(2, (Action<string>)RunCamstarService);
            SNPService.Packets.Add(254, GenericDictionary);
        }

        /// <summary>
        /// Records the Chain Stretch information passed to it to the line database in a chainstretch table. if the table doesnt exist yet it generates it.
        /// </summary>
        public void RunSQLCommand(string message)
        {
            try                                                                                     //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;      //Convert json to object
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("CustomSQLFunction with contents" + receivedPacket["Command"].ToString(), 3));
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    connection.Open();                                                              //open the connection
                    using (SqlCommand command = new SqlCommand(receivedPacket["Command"].ToString(), connection))
                    {                                                                               //Comand Time!
                        int rowsAffected = command.ExecuteNonQuery();                               // execute the command returning number of rows affected
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(rowsAffected + " row(s) inserted", 2));//logit
                    }
                }
            }
            catch (Exception ex)                                                                    //catch exceptions
            {
                if (ex.Message.Contains("ExecuteNonQuery requires an open and available Connection."))//if the connection crashed
                {
                    SNPService.ReastablishSQL(RunSQLCommand, message);                                      //reastablish it
                }
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));           //else log the error and move on
            }
        }

        public void RunCamstarService(string message)
        {
            try
            {
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                StringBuilder PacketStringBuilder = new StringBuilder();
                PacketStringBuilder.Append("<__InSite __encryption=\"2\" __version=\"1.1\"><__session><__connect><user>");//load in start of connection string
                PacketStringBuilder.Append("<__name>" + SNPPackets.CamstarUsername + "</__name>");             //load in the username
                PacketStringBuilder.Append("</user>");
                PacketStringBuilder.Append("<password __encrypted=\"yes\">" + SNPPackets.CamstarPassword + "</password>");//and password ( already encrypted check where it gets loaded from app config.)
                PacketStringBuilder.Append("</__connect></__session>");
                PacketStringBuilder.Append(receivedPacket["Service"]);
                PacketStringBuilder.Append("</__InSite>");
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(Sendmessage(SNPPackets.CamstarIP, SNPPackets.CamstarPort, PacketStringBuilder.ToString()), 2)); //send it and grab the data.
            }
            catch (Exception ex) { SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 2)); }
        }

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
    }
}