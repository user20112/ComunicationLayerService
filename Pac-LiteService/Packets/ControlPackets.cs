using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SNPService.Resources;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SNPService.Packets
{
    internal class ControlPackets
    {
        public ControlPackets()
        {
            Dictionary<int, Action<string>> ControlDictionary = new Dictionary<int, Action<string>>();
            ControlDictionary.Add(1, (Action<string>)LoggingLevel);
            ControlDictionary.Add(2, (Action<string>)Silence);
            ControlDictionary.Add(3, (Action<string>)Deafen);
            SNPService.Packets.Add(3, ControlDictionary);
        }

        /// <summary>
        /// Sets the logging level of the SNP Service
        /// </summary>
        public void LoggingLevel(string message)
        {
            try
            {
                var OldSetting = SNPService.LogggingLevel;                                                              //grab previos setting
                string jsonString = message.Substring(7, message.Length - 7);                                           //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;                          //convert Json to jobject
                SNPService.LogggingLevel = Convert.ToInt32(receivedPacket["LoggingLevel"]);                             //set logging level
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Logging Level Has been set to" + receivedPacket["LoggingLevel"].ToString(), 1));//Log Log Log everyone wants a log
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)                                      //if intTimInSeconds is set and is not 0
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);                           //a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    SNPService.LogggingLevel = OldSetting;                                                              //sleep for the time requested in seconds before setting the setting back
                    SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Logging Level Has been set to " + OldSetting.ToString(), 1));//loggit
                }
            }
            catch (Exception ex)                                                                                        //catch all errors
            {
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));                               //and definitly dont log them that would be terrible why would you do such a thing i mean cmon we worked so hard on this application and we logged every other error in it so why would we log this one ? we wouldnt it just doesnt make sense so cmon get your head out of the gutter man
            }
        }

        /// <summary>
        /// Stops the program from sending out any messages
        /// </summary>
        public void Silence(string message)
        {
            try
            {
                var OldSetting = SNPService.Sending;                                                                    //grab old setting
                string jsonString = message.Substring(7, message.Length - 7);                                           //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;                          //convert to jobject
                SNPService.Sending = Convert.ToInt32(receivedPacket["Sendbool"]) == 1;                                  //Set the Sending bool
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Sending Has been set to" + receivedPacket["Sendbool"].ToString(), 21));//log it
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)                                      // if int time in seconds is set and not 0
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);                           //a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    SNPService.Sending = OldSetting;                                                                    //sleep for that long in seconds before setting the setting back to its previos state
                    if (OldSetting)
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Sending Has been set to 1", 1));
                    else
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Sending Has been set to 0", 1));
                }
            }
            catch (Exception ex)                                                                                        //catch errors
            {
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));                               //log them
            }
        }

        /// <summary>
        /// Stops the service from listening for any messages.
        /// </summary>
        public void Deafen(string message)
        {
            try
            {
                var OldSetting = SNPService.Listening;                                                                  //grab old setting
                string jsonString = message.Substring(7, message.Length - 7);                                           //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;                          //convert to jobject
                SNPService.Listening = Convert.ToInt32(receivedPacket["ListenBool"]) == 1;                              //set setting
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Listening Has been set to" + receivedPacket["ListenBool"].ToString(), 1));//logit
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)                                      //if inttime inseconds is set and not 0
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);                           //a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    SNPService.Listening = OldSetting;                                                                  //sleep for that long then set the setting back
                    if (OldSetting)
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Listening Has been set to 1", 1));
                    else
                        SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Listening Has been set to 0", 1));
                }
            }
            catch (Exception ex)                                                                                        //catch the errors
            {
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));                               //let it log it
            }
        }
    }
}