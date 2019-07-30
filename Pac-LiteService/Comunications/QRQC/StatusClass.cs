using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNPService.Comunications.QRQC
{
    public class StatusClass //currently multipurpose. used for getting status and detail
    {
        public int StatusID;
        public string Product;
        public string Desc { get; set; }
        public bool OEE;
        public int Goal;

        public StatusClass(int stID, string description, bool oee)
        {
            StatusID = stID;
            Desc = description;
            OEE = oee;
        }

        public StatusClass()
        {
            Desc = "error";
            OEE = false;
            Product = "none";
        }
    }
}
