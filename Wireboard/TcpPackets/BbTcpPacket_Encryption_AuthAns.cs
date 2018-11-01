using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_Encryption_AuthAns : BbTcpPacket
    {
        public byte ErrorCode { get; private set; }

        internal BbTcpPacket_Encryption_AuthAns(BbTcpPacket src) : base(src)
        {
        }

        internal void ProcessData(BinaryReader data)
        {
            // [ErrorCode 1]
            ErrorCode = data.ReadByte();
            IsValid = true;
        }
    }
}