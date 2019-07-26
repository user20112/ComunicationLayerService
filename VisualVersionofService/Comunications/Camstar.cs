using Camstar.XMLClient.API;
using System;

namespace VisualVersionofService.Comunications
{
    internal class Camstar
    {
        private string Host;
        private int Port;
        private string UserName;
        private string Password;
        private string ContainerName;
        private Guid SessionID;

        private csiClient Client;
        private csiConnection Connection;
        private csiSession Session;
        private csiDocument Document;
        private csiService Service;
        private csiObject InputData;
        private csiSubentity Details;
        private csiDocument ResponseDocument;
        private csiExceptionData ExceptionData;
        private csiDataField CompletionMsg;
        private csiService csiService;
        private csiSubentity CurrentStatusDetails;
        private string ErrorMsg;

        private Camstar(string host, int port, string username, string password)
        {
            Host = host;
            Port = port;
            UserName = username;
            Password = password;
        }

        private bool InitializeSession()
        {
            Client = new csiClient();
            Connection = Client.createConnection(Host, Port);
            Session = Connection.createSession(UserName, Password, SessionID.ToString());
            return true;
        }

        private void ResourceThroughput()
        {
            Document = Session.createDocument("ThroughputDocument");
            Service = Document.createService("Start");
            InputData = Service.inputData();
            Details = InputData.subentityField("Details");

            Details.dataField("ContainerName").setValue(ContainerName);
            Details.namedObjectField("Owner").setRef("Owner1");
            Details.namedObjectField("StartReason").setRef("Start1");
            Details.namedObjectField("Level").setRef("Batch");

            CurrentStatusDetails.namedObjectField("Factory").setRef("HQ");
            Service.setExecute();

            Service.requestData().requestField("CompletionMsg");
            ResponseDocument = Document.submit();

            if (ResponseDocument.checkErrors())
            {
            }
        }
    }
}