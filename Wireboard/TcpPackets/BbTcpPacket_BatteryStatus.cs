using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_BatteryStatus : BbTcpPacket
    {
        internal BbTcpPacket_BatteryStatus(BbTcpPacket src) : base(src)
        {
        }

        public bool IsPluggedIn { get; private set; }
        public bool IsCharging { get; private set; }
        public int BatteryLevel { get; private set; }

        internal void ProcessData(BinaryReader data)
        {
            // [Level 1][PluggedIn 1][Charging 1]
            BatteryLevel = Math.Min(100, Math.Max(0, (int)data.ReadByte()));
            IsPluggedIn = data.ReadByte() > 0;
            IsCharging = data.ReadByte() > 0;
            IsValid = true;
        }

    }
}
