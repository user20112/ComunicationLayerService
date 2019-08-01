using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace VisualVersionofService
{
    internal class ControlPackets
    {
        public ControlPackets()
        {
        }

        public void LoggingLevel(string message)
        {
            try
            {
                var OldSetting = Form1.LogggingLevel;                                                              //grab previos setting
                string jsonString = message.Substring(7, message.Length - 7);                                           //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;                          //convert Json to jobject
                Form1.LogggingLevel = Convert.ToInt32(receivedPacket["LoggingLevel"]);                             //set logging level
                Form1.DiagnosticItems.Enqueue(new DiagnosticItem("Logging Level Has been set to" + receivedPacket["LoggingLevel"].ToString(), 1));//Log Log Log everyone wants a log
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)                                      //if intTimInSeconds is set and is not 0
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);                           //a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    Form1.LogggingLevel = OldSetting;                                                              //sleep for the time requested in seconds before setting the setting back
                    Form1.DiagnosticItems.Enqueue(new DiagnosticItem("Logging Level Has been set to " + OldSetting.ToString(), 1));              //loggit
                }
            }
            catch (Exception ex)                                                                                        //catch all errors
            {
                Form1.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));                                                             //and definitly dont log them that would be terrible why would you do such a thing i mean cmon we worked so hard on this application and we logged every other error in it so why would we log this one ? we wouldnt it just doesnt make sense so cmon get your head out of the gutter man
            }
        }

        public void Silence(string message)
        {
            try
            {
                var OldSetting = Form1.Sending;                                                                    //grab old setting
                string jsonString = message.Substring(7, message.Length - 7);                                           //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;                          //convert to jobject
                Form1.Sending = Convert.ToInt32(receivedPacket["Sendbool"]) == 1;                                  //Set the Sending bool
                Form1.DiagnosticItems.Enqueue(new DiagnosticItem("Sending Has been set to" + receivedPacket["Sendbool"].ToString(), 21));         //log it
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)                                      // if int time in seconds is set and not 0
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);                           //a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    Form1.Sending = OldSetting;                                                                    //sleep for that long in seconds before setting the setting back to its previos state
                    if (OldSetting)
                        Form1.DiagnosticItems.Enqueue(new DiagnosticItem("Sending Has been set to 1", 1));
                    else
                        Form1.DiagnosticItems.Enqueue(new DiagnosticItem("Sending Has been set to 0", 1));
                }
            }
            catch (Exception ex)                                                                                        //catch errors
            {
                Form1.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));                                                             //log them
            }
        }

        public void Deafen(string message)
        {
            try
            {
                var OldSetting = Form1.Listening;                                                                  //grab old setting
                string jsonString = message.Substring(7, message.Length - 7);                                           //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;                          //convert to jobject
                Form1.Listening = Convert.ToInt32(receivedPacket["ListenBool"]) == 1;                              //set setting
                Form1.DiagnosticItems.Enqueue(new DiagnosticItem("Listening Has been set to" + receivedPacket["ListenBool"].ToString(), 1));     //logit
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)                                      //if inttime inseconds is set and not 0
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);//a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    Form1.Listening = OldSetting;                                                                  //sleep for that long then set the setting back
                    if (OldSetting)
                        Form1.DiagnosticItems.Enqueue(new DiagnosticItem("Listening Has been set to 1", 1));
                    else
                        Form1.DiagnosticItems.Enqueue(new DiagnosticItem("Listening Has been set to 0", 1));
                }
            }
            catch (Exception ex)                                                                                        //catch the errors
            {
                Form1.DiagnosticItems.Enqueue(new DiagnosticItem(ex.ToString(), 1));                                                             //let it log it
            }
        }
    }
}