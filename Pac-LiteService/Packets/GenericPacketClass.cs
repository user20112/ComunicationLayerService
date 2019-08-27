using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNPService.Packets
{
    class GenericPacketClass
    {
        public GenericPacketClass()
        {
            Dictionary<int, Action<string>> GenericPacketClassDictionary = new Dictionary<int, Action<string>>();//name this after your application
            GenericPacketClassDictionary.Add(1, (Action<string>)Packet1);//this should be This Functions ID in the application
            //add the above line for each packet you create.
            SNPService.Packets.Add(1, GenericPacketClassDictionary);//this should be your ApplicationID and name
        }
        public void Packet1(string message)
        {
            //do whatever you want with the packet.
        }
    }
}
