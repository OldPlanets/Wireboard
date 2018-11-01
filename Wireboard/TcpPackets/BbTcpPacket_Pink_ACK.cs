using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_Pink_ACK : BbTcpPacket
    {
        internal BbTcpPacket_Pink_ACK(BbTcpPacket src) : base(src)
        {
        }

        internal void ProcessData(BinaryReader data)
        {
            // [0]
            IsValid = true;
        }
    }
}
