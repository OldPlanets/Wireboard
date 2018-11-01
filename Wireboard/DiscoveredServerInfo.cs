using Wireboard.DiscoveryPackets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard
{
    public class DiscoveredServerInfo : IEquatable<DiscoveredServerInfo>
    {
        public DiscoveryPacket_Answer receivedInfo { get; private set; }
        private IPAddress SeenRemoteIP { get; set; }
        public IPAddress ConnectionIP => SeenRemoteIP;

        public String ServerName => receivedInfo.ServerName;
        public UInt16 Port => receivedInfo.ServerPort;
        public IPAddress IP => SeenRemoteIP;
        public int ServerGUID => receivedInfo.ServerGUID;
        public bool PasswordRequired => receivedInfo.PasswordRequired;

        public DiscoveredServerInfo(DiscoveryPacket_Answer packet, IPAddress inSeenRemoteIP)
        {
            receivedInfo = packet;
            SeenRemoteIP = inSeenRemoteIP;
        }

        public String getDebugString()
        {
            return "Seen IP: " + SeenRemoteIP + " " + receivedInfo.getDebugString();
        }

        public bool Equals(DiscoveredServerInfo other)
        {
            // because server will often have an Ipv4 and IPv6 address - so appear different but are actually the same - we will return true
            // if the serverguid and Port are equal and the IPAddress families differ. If they don't differ we check them too, so changed IPs are taken into account
            return  other.Port == Port && other.ServerGUID == ServerGUID 
                && (other.SeenRemoteIP.Equals(SeenRemoteIP) || other.SeenRemoteIP.AddressFamily != SeenRemoteIP.AddressFamily)
                && (other.IP.Equals(IP) || other.IP.AddressFamily != IP.AddressFamily);
        }
    }
}
