using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// This must be an exact copy of the 'Instructions' struct on the QRQC service
/// or nothing will work.
/// 
/// If you change anything about the Line class, than make sure the changes are updated server side as well.
/// 
/// </summary>

namespace SNPService.Comunications.QRQC
{
    [Serializable]
    public struct Instructions
    {
        public bool Recalculate; //ask server to recalculate output also
        public DateTime time;
        public Line L;
        public string DateTimeStr;
        public string customMsg;

        public Instructions(bool recalculate, DateTime datetime, Line line)
        {
            this.Recalculate = recalculate;
            this.time = datetime;
            this.L = line;
            this.DateTimeStr = datetime.ToString();
            this.customMsg = "NORMAL";
        }
    }
}
