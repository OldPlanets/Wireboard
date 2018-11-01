using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard
{
    public class BbBatteryStatus
    {
        public int BatteryLevel { get; set; } = 0;
        public bool PluggedIn { get; set; } = false;
        public bool Charging { get; set; } = false;

        public void Reset()
        {
            BatteryLevel = 0;
            PluggedIn = false;
            Charging = false;
        }
    }
}
