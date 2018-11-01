using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    enum EAttachResult : byte
    {
        ACCEPTED = 0,
        DENIED = 1,
        AUTH_REQUIRED = 2
    }

    class BbTcpPacket_Attach_Ans : BbTcpPacket
    {
        public EAttachResult AttachResult { get; private set; }

        internal BbTcpPacket_Attach_Ans(BbTcpPacket src) : base(src)
        {
        }

        internal void ProcessData(BinaryReader data)
        {
            // [AttachRes 1]
            AttachResult = (EAttachResult)data.ReadByte();
            IsValid = true;
        }
    }
}
