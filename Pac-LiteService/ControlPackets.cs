using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace Pac_LiteService
{
    internal class ControlPackets
    {
        private SNPService Controller;

        public ControlPackets(SNPService controller)
        {
            Controller = controller;
        }

        public void LoggingLevel(string message)
        {
            try
            {
                var OldSetting = Controller.LogggingLevel;                                                              //grab previos setting
                string jsonString = message.Substring(7, message.Length - 7);                                           //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;                          //convert Json to jobject
                Controller.LogggingLevel = Convert.ToInt32(receivedPacket["LoggingLevel"]);                             //set logging level
                Controller.DiagnosticOut("Logging Level Has been set to" + receivedPacket["LoggingLevel"].ToString(), 1);//Log Log Log everyone wants a log
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)                                      //if intTimInSeconds is set and is not 0
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);                           //a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    Controller.LogggingLevel = OldSetting;                                                              //sleep for the time requested in seconds before setting the setting back
                    Controller.DiagnosticOut("Logging Level Has been set to " + OldSetting.ToString(), 1);              //LOOOOOOGGGGG
                }
            }
            catch (Exception ex)                                                                                        //catch all errors
            {
                Controller.DiagnosticOut(ex.ToString(), 1);                                                             //and definitly dont log them that would be terrible why would you do such a thing i mean cmon we worked so hard on this application and we logged every other error in it so why would we log this one ? we wouldnt it just doesnt make sense so cmon get your head out of the gutter man
            }
        }

        public void Silence(string message)
        {
            try
            {
                var OldSetting = Controller.Sending;                                                                    //grab old setting
                string jsonString = message.Substring(7, message.Length - 7);                                           //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;                          //convert to jobject
                Controller.Sending = Convert.ToInt32(receivedPacket["Sendbool"]) == 1;                                  //Set the Sending bool
                Controller.DiagnosticOut("Sending Has been set to" + receivedPacket["Sendbool"].ToString(), 21);         //log it
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)                                      // if int time in seconds is set and not 0
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);                           //a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    Controller.Sending = OldSetting;                                                                    //sleep for that long in seconds before setting the setting back to its previos state
                    if (OldSetting)
                        Controller.DiagnosticOut("Sending Has been set to 1", 1);
                    else
                        Controller.DiagnosticOut("Sending Has been set to 0", 1);
                }
            }
            catch (Exception ex)                                                                                        //catch errors
            {
                Controller.DiagnosticOut(ex.ToString(), 1);                                                             //log them
            }
        }

        public void Deafen(string message)
        {
            try
            {
                var OldSetting = Controller.Listening;                                                                  //grab old setting
                string jsonString = message.Substring(7, message.Length - 7);                                           //grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;                          //convert to jobject
                Controller.Listening = Convert.ToInt32(receivedPacket["ListenBool"]) == 1;                              //set setting
                Controller.DiagnosticOut("Listening Has been set to" + receivedPacket["ListenBool"].ToString(), 1);     //logit
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)                                      //if inttime inseconds is set and not 0
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);//a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    Controller.Listening = OldSetting;                                                                  //sleep for that long then set the setting back
                    if (OldSetting)
                        Controller.DiagnosticOut("Listening Has been set to 1", 1);
                    else
                        Controller.DiagnosticOut("Listening Has been set to 0", 1);
                }
            }
            catch (Exception ex)                                                                                        //catch the errors
            {
                Controller.DiagnosticOut(ex.ToString(), 1);                                                             //let it log it
            }
        }
    }
}