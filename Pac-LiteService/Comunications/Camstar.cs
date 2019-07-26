using Camstar.XMLClient.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNPService.Comunications
{
    class Camstar
    {
        string Host;
        int Port;
        string UserName;
        string Password;
        string ContainerName;
        Guid SessionID;

        csiClient Client;
        csiConnection Connection;
        csiSession Session;
        csiDocument Document;
        csiService Service;
        csiObject InputData;
        csiSubentity Details;
        csiDocument ResponseDocument;
        csiExceptionData ExceptionData;
        csiDataField CompletionMsg;
        csiService csiService;
        csiSubentity CurrentStatusDetails;
        string ErrorMsg;
        Camstar(string host, int port, string username, string password)
        {
            Host = host;
            Port = port;
            UserName = username;
            Password = password;
        }
        bool InitializeSession()
        {
            Client = new csiClient();
            Connection = Client.createConnection(Host, Port);
            Session = Connection.createSession(UserName, Password, SessionID.ToString());
            return true;
        }
        void ResourceThroughput()
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
