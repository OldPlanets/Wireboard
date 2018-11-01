using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.BbEventArgs
{
    public class DiscoveryFinishedEventArgs : EventArgs
    {
        public bool RemoteMinProtocolTooHigh { get; private set; }
        public bool LocalMinProtocolTooHigh { get; private set; }

        public DiscoveryFinishedEventArgs(bool bRemoteMinProtocolTooHigh, bool bLocalMinProtocolTooHigh)
        {
            RemoteMinProtocolTooHigh = bRemoteMinProtocolTooHigh;
            LocalMinProtocolTooHigh = bLocalMinProtocolTooHigh;
        }
    }
}
