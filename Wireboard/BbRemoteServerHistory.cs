using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard
{
    public class BbRemoteServerHistory
    {
        public IPAddress ServerIP { get; protected set; }
        public ushort Port { get; protected set; }
        protected LowResStopWatch ConnectedTime { get; set; } = new LowResStopWatch(false);
        protected LowResStopWatch DisconnectedTime { get; set; } = new LowResStopWatch(false);
        protected bool ProtocolIncompatible { get; set; } = false;
        protected byte ServerMinProtocolVersion { get; set; } = 0;
        public bool SupportsScreenCapture { get; protected set; } = false;
        public bool IsProVersion { get; protected set; } = false;

        private int m_nSessionID = 0;
        public int SessionID
        {
            get
            {
                while (m_nSessionID == 0)
                    m_nSessionID = new Random().Next(int.MinValue, int.MaxValue);
                return m_nSessionID;
            }
        }

        public String ServerName { get; protected set; }
        public int ServerGUID { get; protected set; }

        public static int OwnGUID
        {
            get
            {
                int nGUID = Properties.Settings.Default.OwnGUID;
                while (nGUID == 0)
                {
                    nGUID = new Random().Next(int.MinValue, int.MaxValue);
                    Properties.Settings.Default.OwnGUID = nGUID;
                    Properties.Settings.Default.Save();
                }
                return nGUID;
            }
        }

        public BbRemoteServerHistory() { }

        public BbRemoteServerHistory(BbRemoteServerHistory src)
        {
            ServerIP = src.ServerIP;
            Port = src.Port;
            ConnectedTime = src.ConnectedTime;
            DisconnectedTime = src.DisconnectedTime;
            ProtocolIncompatible = src.ProtocolIncompatible;
            m_nSessionID = src.m_nSessionID;
            ServerName = src.ServerName;
            ServerGUID = src.ServerGUID;
            ServerMinProtocolVersion = src.ServerMinProtocolVersion;
            SupportsScreenCapture = src.SupportsScreenCapture;
            IsProVersion = src.IsProVersion;
        }

    }
}
