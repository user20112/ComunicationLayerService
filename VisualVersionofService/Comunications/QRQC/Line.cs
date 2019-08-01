/// <summary>
/// If anything in the line class is change, make sure the changes are reflected serverside.
/// </summary>

namespace SNPService.Comunications.QRQC
{
    public class Line
    {
        public string Name { get; set; }            //Resource ID
        public string DisplayName { get; set; }     //Name of the Resource
        public bool Automatic;                      //if line is automated else line requires manual data per hour
        public int GoodProductPerEntry;             //product for database index
        public string ProductInputSQLVar;
        public string GoodProductSQLVar;            //column name for good product bit
        public string TableName;                    //table name for line
        public int AStart;                          //shift times
        public int AEnd;
        public int BStart;
        public int BEnd;
        public int CStart;
        public int CEnd;
        public bool isFinalMachine;
        public double FUDGE;                        //goal modifier
    }
}