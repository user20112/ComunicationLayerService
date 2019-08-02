using System.Configuration;

namespace SNPService
{
    public class Encryptor
    {
        /// <summary>
        /// Updates the password if encrypt is true  it also encrypts it first
        /// </summary>
        public static void UpdateCamstarPassword(string Password, bool encrypt = false)
        {
            string password = Password;
            if (encrypt)
                password = (EncryptOrDecrypt(Password));
            ChangeConfig("CamstarPassword", password);
            ChangeConfig("ResetCamstarPassword", "");
        }
        /// <summary>
        /// Updates the password if encrypt is true  it also encrypts it first
        /// </summary>
        public static void UpdateEngDBPassword(string Password, bool encrypt = false)
        {
            string password = Password;
            if (encrypt)
                password = (EncryptOrDecrypt(Password));
            ChangeConfig("ENGDBPassword", password);
            ChangeConfig("ResetENGDBPassword", "");
        }
        /// <summary>
        /// Changes the app.config key to the entered value.
        /// </summary>
        public static void ChangeConfig(string key, string value)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None); //get current 
            config.AppSettings.Settings[key].Value = value;                                                 //change 
            config.Save(ConfigurationSaveMode.Modified);                                                    //Save
            ConfigurationManager.RefreshSection("appSettings");                                            //reload 
        }
        /// <summary>
        /// super simple encryption just flips the bits.
        /// </summary>
        public static string EncryptOrDecrypt(string Input)
        {
            string result = "";
            for (int x = 0; x < Input.Length; x++)
            {
                result += (char)((byte)(~((byte)Input[x])));
            }
            SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(result, 1));
            return result;
        }

    }
}
