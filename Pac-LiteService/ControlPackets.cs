using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace Pac_LiteService
{
    internal class ControlPackets
    {
        private PacLiteService Controller;

        public ControlPackets(PacLiteService Controller)
        {
            Controller = Controller;
        }

        public void LoggingLevel(string message)
        {
            try
            {
                var OldSetting = Controller.LogggingLevel;
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                Controller.LogggingLevel = Convert.ToInt32(receivedPacket["LoggingLevel"]);
                Controller.DiagnosticOut("Logging Level Has been set to" + receivedPacket["LoggingLevel"].ToString(), 2);
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);//a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    Controller.LogggingLevel = OldSetting;
                    Controller.DiagnosticOut("Logging Level Has been set to " + OldSetting.ToString(), 2);
                }
            }
            catch (Exception ex)
            {
                Controller.DiagnosticOut(ex.ToString(), 1);
            }
        }

        public void Silence(string message)
        {
            try
            {
                var OldSetting = Controller.Sending;
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                Controller.Sending = Convert.ToInt32(receivedPacket["Sendbool"]) == 1;
                Controller.DiagnosticOut("Sending Has been set to" + receivedPacket["Sendbool"].ToString(), 2);
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);//a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    Controller.Sending = OldSetting;
                    if (OldSetting)
                        Controller.DiagnosticOut("Sending Has been set to 1", 2);
                    else
                        Controller.DiagnosticOut("Sending Has been set to 0", 2);
                }
            }
            catch (Exception ex)
            {
                Controller.DiagnosticOut(ex.ToString(), 1);
            }
        }

        public void Deafen(string message)
        {
            try
            {
                var OldSetting = Controller.Listening;
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                Controller.Listening = Convert.ToInt32(receivedPacket["ListenBool"]) == 1;
                Controller.DiagnosticOut("Listening Has been set to" + receivedPacket["ListenBool"].ToString(), 2);
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);//a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    Controller.Listening = OldSetting;
                    if (OldSetting)
                        Controller.DiagnosticOut("Listening Has been set to 1", 2);
                    else
                        Controller.DiagnosticOut("Listening Has been set to 0", 2);
                }
            }
            catch (Exception ex)
            {
                Controller.DiagnosticOut(ex.ToString(), 1);
            }
        }
    }
}