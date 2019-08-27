using System.Configuration;

namespace SNPService.Resources
{
    public class Encryptor
    {
        /// <summary>
        /// Updates the password if encrypt is true  it also encrypts it first
        /// </summary>
        public static void UpdateCamstarPassword(string Password, bool encrypt = false)                     //updates the Password in the app.config
        {
            string password = Password;                                                                     //copy password over
            if (encrypt)                                                                                    //if its not already encrypted encrypt it
                password = (EncryptOrDecrypt(Password));                                                    //just bit flips every character
            ChangeConfig("CamstarPassword", password);                                                      //Change the config for the password
            ChangeConfig("ResetCamstarPassword", "");                                                       //and clear the reset password feild to prevent it being reset again.
        }

        /// <summary>
        /// Updates the password if encrypt is true  it also encrypts it first
        /// </summary>
        public static void UpdateEngDBPassword(string Password, bool encrypt = false)
        {
            string password = Password;                                                                     //copy the password over
            if (encrypt)                                                                                    //if not already encrypted
                password = (EncryptOrDecrypt(Password));                                                    //encrypt it ( just bit flipped every char
            ChangeConfig("ENGDBPassword", password);                                                        //change the password
            ChangeConfig("ResetENGDBPassword", "");                                                         //reset the value of reset password to prevent reseting it next boot.
            //SNPService.DiagnosticItems.Enqueue(new DiagnosticItem(password, 1));                          //uncomment to log password changes.
        }

        /// <summary>
        /// Changes the app.config key to the entered value.
        /// </summary>
        public static void ChangeConfig(string key, string value)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);  //get current
            config.AppSettings.Settings[key].Value = value;                                                 //change
            config.Save(ConfigurationSaveMode.Modified);                                                    //Save
            ConfigurationManager.RefreshSection("appSettings");                                             //reload
        }

        /// <summary>
        /// super simple encryption just flips the bits.
        /// </summary>
        public static string EncryptOrDecrypt(string Input)                                                 //bit flip every character in the input string
        {
            string result = "";                                                                             //stores result
            for (int x = 0; x < Input.Length; x++)                                                          //foreach character
            {
                result += (char)((byte)(~((byte)Input[x])));                                                //cast to byte then bitwise negate it then cast back to byte ( comes back as int) then cast to character. ( the second byte cast is important otherwise '0' will return int 65437
            }
            return result;                                                                                  //return the result
        }
    }
}