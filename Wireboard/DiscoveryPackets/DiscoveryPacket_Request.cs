using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.DiscoveryPackets
{
    class DiscoveryPacket_Request : DiscoveryPacket
    {
        public static BinaryWriter WritePacket()
        {
            // [Header 5] [RequestedProtVer 1]
            BinaryWriter res = new BinaryWriter(new MemoryStream(256));
            res.Seek(BBProtocol.DISCOVERY_HEADERSIZE, SeekOrigin.Begin);
            res.Write((byte)BBProtocol.CURRENT_DISCOVERY_PROTOCOL_VERSION);
            WriteHeader(res, BBProtocol.OP_DISCOVERY_REQUEST);
            return res;
        }
    }
}
