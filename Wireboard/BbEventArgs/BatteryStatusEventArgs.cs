using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.BbEventArgs
{
    public class BatteryStatusEventArgs : EventArgs
    {
        public BbBatteryStatus BatteryStatus { get; private set; }

        public BatteryStatusEventArgs(int nLevel, bool bPluggedIn, bool bCharging)
        {
            BatteryStatus = new BbBatteryStatus();
            BatteryStatus.BatteryLevel = nLevel;
            BatteryStatus.Charging = bCharging;
            BatteryStatus.PluggedIn = bPluggedIn;
        }
    }
}
