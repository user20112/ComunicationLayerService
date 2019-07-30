using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Web.Script.Serialization;

namespace SNPService.Comunications.QRQC
{
    public class SynchronousSocketClient
    {
        private static bool testConnection = false;
        public SynchronousSocketClient(Instructions i)
        {
			testConnection = false;
            string serializedObject;
            JavaScriptSerializer jss = new JavaScriptSerializer();
            serializedObject = jss.Serialize(i);
            StartClient(serializedObject);
        }

        public SynchronousSocketClient(Instructions i, bool test) //only for testing the connection (crude)
        {
			if (test) testConnection = true;
			else testConnection = false;
            string serializedObject;
            JavaScriptSerializer jss = new JavaScriptSerializer();
            serializedObject = jss.Serialize(i);
            StartClient(serializedObject);
        }
		
        public static void StartClient(string serialized)
        {
            // Data buffer for incoming data.
            byte[] bytes = new byte[1024];

            // Connect to a remote device.  
            try
            {
                Stopwatch stopWatch = new Stopwatch();
                string SERVERIP = System.Configuration.ConfigurationManager.AppSettings["QRQC_Service_SERVERIP"];
                // Establish the remote endpoint for the socket.
                // This example uses port 11000 on the local computer.
                IPAddress ipAddress = IPAddress.Parse(SERVERIP);
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, 61752);

                // Create a TCP/IP  socket.  
                Socket sender = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                // Connect the socket to the remote endpoint. Catch any errors.
                try
                {
                    sender.Connect(remoteEP);

                    Console.WriteLine("Socket connected to {0}",
                        sender.RemoteEndPoint.ToString());

                    // Encode the data string into a byte array.  
                    byte[] msg = Encoding.ASCII.GetBytes(serialized + "<EOF>");

                    // Send the data through the socket.  
                    if(testConnection)
                    {
                        stopWatch.Start();
                    }
                    int bytesSent = sender.Send(msg);

                    // Receive the response from the remote device.  
                    int bytesRec = sender.Receive(bytes);
                    string recv = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                    
                    // Release the socket.  
                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();

                    if (testConnection)
                    {
                        stopWatch.Stop();
                    }
                }
                catch (ArgumentNullException ane)
                {
				}
                catch (SocketException se)
                {
				}
                catch (Exception e)
                {
				}

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
			}
        }
    }
}
