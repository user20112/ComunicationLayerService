using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace VisualVersionofService
{
    internal class ControlPackets
    {        /// <summary>
             /// Packet Sent every index for the EMP system. Simply insert into SQL for recording ( and grab a time stamp if missing)
             /// </summary>
        private Form1 MainForm;

        public ControlPackets(Form1 mainform)
        {
            MainForm = mainform;
        }

        public void LoggingLevel(string message)
        {
            try
            {
                var OldSetting = MainForm.LogggingLevel;
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                MainForm.LogggingLevel = Convert.ToInt32(receivedPacket["LoggingLevel"]);
                MainForm.DiagnosticOut("Logging Level Has been set to" + receivedPacket["LoggingLevel"].ToString(), 2);
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);//a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    MainForm.LogggingLevel = OldSetting;
                    MainForm.DiagnosticOut("Logging Level Has been set to " + OldSetting.ToString(), 2);
                }

            }
            catch (Exception ex)
            {
                MainForm.DiagnosticOut(ex.ToString(), 1);
            }
        }

        public void Silence(string message)
        {
            try
            {
                var OldSetting = MainForm.Sending;
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                MainForm.Sending = Convert.ToInt32(receivedPacket["Sendbool"]) == 1;
                MainForm.DiagnosticOut("Sending Has been set to" + receivedPacket["Sendbool"].ToString(), 2);
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);//a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    MainForm.Sending = OldSetting;
                    if (OldSetting)
                        MainForm.DiagnosticOut("Sending Has been set to 1", 2);
                    else
                        MainForm.DiagnosticOut("Sending Has been set to 0", 2);
                }
            }
            catch (Exception ex)
            {
                MainForm.DiagnosticOut(ex.ToString(), 1);
            }
        }

        public void Deafen(string message)
        {
            try
            {
                var OldSetting = MainForm.Listening;
                string jsonString = message.Substring(7, message.Length - 7);//grab json data from the end.
                JObject receivedPacket = JsonConvert.DeserializeObject(jsonString) as JObject;
                MainForm.Listening = Convert.ToInt32(receivedPacket["ListenBool"]) == 1;
                MainForm.DiagnosticOut("Listening Has been set to" + receivedPacket["ListenBool"].ToString(), 2);
                if (Convert.ToInt32(receivedPacket["IntTimeInSeconds"] ?? 0) != 0)
                {
                    Thread.Sleep(Convert.ToInt32(receivedPacket["IntTimeInSeconds"]) * 1000);//a bit worried about exhuasting the number of threads in the threadpool. However there shouldnt be many threads consumed by control Messages so it should be ok.
                    MainForm.Listening = OldSetting;
                    if (OldSetting)
                        MainForm.DiagnosticOut("Listening Has been set to 1", 2);
                    else
                        MainForm.DiagnosticOut("Listening Has been set to 0", 2);
                }
            }
            catch (Exception ex)
            {
                MainForm.DiagnosticOut(ex.ToString(), 1);
            }
        }
    }
}