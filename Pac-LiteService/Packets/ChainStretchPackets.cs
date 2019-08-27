using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SNPService.Resources;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

namespace SNPService.Packets
{
    internal class ChainStretchPackets
    {
        public ChainStretchPackets()
        {
            Dictionary<int, Action<string>> ChainStretchDictionary = new Dictionary<int, Action<string>>();
            ChainStretchDictionary.Add(1, (Action<string>)Index);

            SNPService.Packets.Add(4, ChainStretchDictionary);
        }

        /// <summary>
        /// Records the Chain Stretch information passed to it to the line database in a chainstretch table. if the table doesnt exist yet it generates it.
        /// </summary>
        public void Index(string message)
        {
            try                                                                                     //try loop in case command fails.
            {
                string jsonString = message.Substring(7, message.Length - 7);                       //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;      //Convert json to object
                StringBuilder sqlStringBuilder = new StringBuilder();
                sqlStringBuilder.Append(" USE [EngDb-" + receivedPacket["Line"] + "] ");            //select database
                sqlStringBuilder.Append("IF not EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'ChainStretch') BEGIN CREATE TABLE [dbo].[ChainStretch]");
                sqlStringBuilder.Append("([Timestamp] [datetime2](7) NOT NULL,[Head] [int] NOT NULL,[Stretch] [float] NOT NULL,[B32_Output] [bit] NOT NULL,[B31_Input] [bit] NOT NULL) ON [PRIMARY] END ");                                                        //start loading the command into the string
                sqlStringBuilder.Append("Insert into ChainStretch ([Head],[Stretch],[Timestamp],[B32_Output],[B31_Input]) values (@Head,@Stretch,@Timestamp,@Output,@Input)");
                using (SqlConnection connection = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
                {
                    double stretch = 0;
                    double.TryParse(receivedPacket["Stretch"].ToString(), out stretch);
                    connection.Open();                                                              //open the connection
                    using (SqlCommand command = new SqlCommand(sqlStringBuilder.ToString(), connection))
                    {                                                                               //Comand Time!
                        command.Parameters.AddWithValue("@Head", Convert.ToInt32(receivedPacket["Head"]));//replace parameters with values
                        command.Parameters.AddWithValue("@Stretch", stretch);
                        command.Parameters.AddWithValue("@Input", Convert.ToInt32(receivedPacket["Input"]) == 1);
                        command.Parameters.AddWithValue("@Output", Convert.ToInt32(receivedPacket["Output"]) == 1);
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
                    SNPService.ReastablishSQL(Index, message);                                      //reastablish it
                }
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));           //else log the error and move on
            }
        }
    }
}