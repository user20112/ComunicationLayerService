namespace SNPService
{
    public class DiagnosticItem
    {
        public int logginglevel = 5;
        public string message;

        public DiagnosticItem(string Message, int LoggingLevel)
        {
            message = Message;
            logginglevel = LoggingLevel;
        }
    }
}