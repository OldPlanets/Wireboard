using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    public enum ECaptureState : byte
    {
        ERROR = 0,
        WAITINGFORPERMISSION = 1,
        PERMISSIONDENIED = 2,
        NOTSUPPORTED = 3,
        STARTING = 4,
    }

    class BbTcpPacket_CaptureState : BbTcpPacket
    {
        public ECaptureState CaptureState { get; private set; }

        internal BbTcpPacket_CaptureState(BbTcpPacket src) : base(src)
        {
        }

        internal void ProcessData(BinaryReader data)
        {
            // [CaptureState 1]
            CaptureState = (ECaptureState)data.ReadByte();
            IsValid = true;
        }
    }
}
