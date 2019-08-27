using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SNPService.Comunications
{
    class UDPListener
    {
        UdpClient receivingUdpClient;                                                               //udp client for recieving messages
        int Port;                                                                                   //port to listen on
        public event MessageReceivedDelegate OnMessageReceived;                                     //callback delegate for whenever we receive a message
        IPEndPoint RemoteIpEndPoint;                                                                //accept from any ip and port.

        public UDPListener(int port)
        {
            receivingUdpClient = new UdpClient(port);                                               //make the receiver , copy the port and start the listening loop
            RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, port);                                 //accept from any ip and port.
            Port = port;

            Task.Run(() => ListenLoop());
        }
        private void ListenLoop()
        {
            while (SNPService.running)                                                              //while we are running
            {
                byte[] data = receivingUdpClient.Receive(ref RemoteIpEndPoint);                     //block until we can receive data
                string Message = "";                                                                //convert the byte array to a string
                for (int x = 0; x < data.Length; x++)
                {
                    Message += (char)data[x];
                }
                if (this.OnMessageReceived != null)                                                 //if its not null
                {
                    Task.Run(() => this.OnMessageReceived(Message));                                //fire the message to the MessageDelegate
                }
            }
        }
    }
}
