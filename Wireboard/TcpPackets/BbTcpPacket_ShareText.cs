using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_ShareText : BbTcpPacket
    {
        public String Text { get; private set; }
        public String Type { get; private set; }

        internal BbTcpPacket_ShareText(BbTcpPacket src) : base(src)
        {
        }

        internal void ProcessData(BinaryReader data)
        {
            // [Text String][Type String]
            Text = BBProtocol.ReadString(data);
            Type = BBProtocol.ReadString(data);
            IsValid = true;
        }
    }
}
