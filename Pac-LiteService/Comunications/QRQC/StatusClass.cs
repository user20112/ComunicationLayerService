namespace SNPService.Comunications.QRQC
{
    public class StatusClass            //Used for reporting to QRQC
    {
        public int StatusID;            //Current Status ID
        public string Product;          //which product the line is running
        public string Desc { get; set; }//must match server side
        public bool OEE;
        public int Goal;

        public StatusClass(int stID, string description, bool oee)
        {
            StatusID = stID;
            Desc = description;
            OEE = oee;
        }

        public StatusClass()            //defualt values
        {
            Desc = "error";
            OEE = false;
            Product = "none";
        }
    }
}