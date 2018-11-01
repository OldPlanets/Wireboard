using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.DiscoveryPackets
{
    public class DiscoveryPacket_Answer : DiscoveryPacket
    {
        public byte ProtocolVersion { get; private set; }
        public IPAddress ServerIP { get; private set; }
        public ushort ServerPort { get; private set; }
        public byte MinProtocolVer { get; private set; }
        public byte MaxProtocolVer { get; private set; }
        public bool PasswordRequired { get; private set; }
        public String ServerName { get; private set; }
        public int ServerGUID { get; private set; }

        internal DiscoveryPacket_Answer(DiscoveryPacket src) : base(src)
        {
        }

        internal void ProcessData(BinaryReader buf)
        {
            // [Header 5] [UsedProtVer 1] [IP InetAddress] [tcpPort 2] [DeviceName String] [FlagPassword 1] [ProtocolMin 1] [ProtocolMax 1][ServerGUID 4]
            ProtocolVersion = buf.ReadByte();
            if (ProtocolVersion < 0x01 || ProtocolVersion > BBProtocol.REQUESTED_DISCOVERY_PROTOCOL_VERSION)
            {
                ParseError = "Protocolversion unsupported";
                return;
            }
            ServerIP = BBProtocol.ReadInetAddress(buf);
            ServerPort = buf.ReadUInt16();
            ServerName = BBProtocol.ReadString(buf);
            PasswordRequired = buf.ReadByte() != 0;
            MinProtocolVer = buf.ReadByte();
            MaxProtocolVer = buf.ReadByte();
            ServerGUID = buf.ReadInt32();
            IsValid = true;
        }

        public String getDebugString()
        {
            return "Desc: " + ServerName + " Reported IP: " + ServerIP + " Port: " + ServerPort 
                + " Password: " + PasswordRequired + " MinVer: " + MinProtocolVer + " MaxVer: " + MinProtocolVer;
        }

        public override bool IsAnswer() { return true; }
    }
}
