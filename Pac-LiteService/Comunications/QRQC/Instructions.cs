﻿using System;

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
        private bool Recalculate; //ask server to recalculate output also
        private DateTime time;
        private Line L;           //the line you are requesting to be recalculated
        private string DateTimeStr;//string for the Date Time
        private string customMsg;

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