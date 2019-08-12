using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_SendScreenCaptureData : BbTcpPacket
    {
        public MemoryStream CaptureData { get; private set; }


        internal BbTcpPacket_SendScreenCaptureData(BbTcpPacket src) : base(src)
        {
        }

        internal void ProcessData(BinaryReader data)
        {
            // [Data];
            CaptureData = (MemoryStream)data.BaseStream;
            IsValid = true;
        }
    }
}
