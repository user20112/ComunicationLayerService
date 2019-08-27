using SNPService.Resources;
using System;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;

namespace SNPService
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        private static void Main()                              //droping point
        {
            ServiceBase[] servicesToRun = new ServiceBase[]     //make a list of the services that need to run
            {
            new SNPService()                                    //with our little SNP Service in there
            };
            if (Environment.UserInteractive)                    //if we are debugging it
            {
                RunInteractive(servicesToRun);                  //we run it differently see below
            }
            else
            {
                ServiceBase.Run(servicesToRun);                 //else we just run it
            }
        }

        /// <summary>
        /// Runs the services in a way that allows debugging on visual studio.
        /// </summary>
        private static void RunInteractive(ServiceBase[] servicesToRun) //run the services in a way that we can debug them
        {
            MethodInfo onStartMethod = typeof(ServiceBase).GetMethod("OnStart",
            BindingFlags.Instance | BindingFlags.NonPublic);            //grab the onstart method from the service
            foreach (ServiceBase service in servicesToRun)              //foreach service
            {
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("Starting {0}..." + service.ServiceName, 2));//llog that we are starting it
                onStartMethod.Invoke(service, new object[] { new string[] { } });//invoke the onstart method
                SNPService.DiagnosticItems.Enqueue(new DiagnosticItem("{0} Started" + service.ServiceName, 2));//log it finishing to
            }
            while (true)                                                //next sleep until the program is stopped to allow debugging the service. otherwise it would drop of the face of the map.
                Thread.Sleep(1000);
        }
    }
}