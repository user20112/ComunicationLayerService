namespace SNPService.Packets
{
    internal class PacketCollection
    {
        public EMPPackets EMPPackets;                                                                                  //collection of all EMP Packets and functions
        public SNPPackets SNPPackets;                                                                                  //collection of all snp packets and functions
        public ControlPackets ControlPackets;                                                                          //collection of all Control packets and functions
        public ChainStretchPackets ChainStretchPackets;                                                                //collection of all Chain Stretch packets and functions
        public GenericPackets GenericPackets;                                                                          //collection of all Generic Packets and functions

        public PacketCollection()
        {
            EMPPackets = new EMPPackets();                                                                          //generate the packet classes
            SNPPackets = new SNPPackets();
            ControlPackets = new ControlPackets();
            ChainStretchPackets = new ChainStretchPackets();
            GenericPackets = new GenericPackets();
        }
    }
}