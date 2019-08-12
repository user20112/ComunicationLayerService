using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNPService.Packets
{
    class GenericPackets
    {
        GenericPackets()
        {

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
    }
}
