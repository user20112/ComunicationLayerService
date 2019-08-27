namespace SNPService.Resources
{
    public class DiagnosticItem
    {
        public int logginglevel = 5;                            //required logging level to see it
        public string message;                                  //message to be logged

        public DiagnosticItem(string Message, int LoggingLevel) // constructor just casts everything in.
        {
            message = Message;
            logginglevel = LoggingLevel;
        }
    }
}