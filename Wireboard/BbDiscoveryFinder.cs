using Wireboard.DiscoveryPackets;
using Org.BouncyCastle.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Linq;
using Wireboard.BbEventArgs;

namespace Wireboard
{
    public class BbDiscoveryFinder
    {
        private static String TAG = typeof(BbDiscoveryFinder).Name;
        private const int TIMEOUT = 3000;

        public ObservableCollection<DiscoveredServerInfo> FoundServer { get; private set; } = new ObservableCollection<DiscoveredServerInfo>();
        private WriteOnceBlock<DiscoveredServerInfo> m_wbFirstResult;
        public bool IsDiscovering { get; private set; }
        public event EventHandler<DiscoveryFinishedEventArgs> DisoveryFinished;
        private bool m_bRemoteMinVersionError = false;
        private bool m_bLocalMinVersionError = false;

        public async Task DiscoverAllAsync()
        {
            if (IsDiscovering) // only called from the Mainthread -> not racy
                return;

            IsDiscovering = true;
            m_bRemoteMinVersionError = false;
            m_bLocalMinVersionError = false;

            m_wbFirstResult = new WriteOnceBlock<DiscoveredServerInfo>((x) => { return x; }); // no cloning needed
            BinaryWriter reqPacket = DiscoveryPacket_Request.WritePacket();
            List<DiscoveredServerInfo> liResults = new List<DiscoveredServerInfo>();

            await Task.WhenAll(DiscoverAllIPv4Async(reqPacket, liResults)
                , DiscoverAllIPv6Async(reqPacket, liResults));

            Log.d(TAG, "Done waiting, got " + liResults.Count + " valid replies");
            if (liResults.Count > 0)
                m_wbFirstResult.Post(liResults[0]); // In case we didn't post any first result because the defaultserver wasn't found


            // Remove Servers which were found on an earlier run but not anymore
            for (int i = FoundServer.Count - 1; i >= 0; i--)
            {
                if (!liResults.Contains(FoundServer[i]))
                    FoundServer.RemoveAt(i);
            }

            IsDiscovering = false;
            DisoveryFinished.Invoke(this, new DiscoveryFinishedEventArgs(m_bRemoteMinVersionError, m_bLocalMinVersionError));
        }

        private async Task DiscoverAllIPv4Async(BinaryWriter reqPacket, List<DiscoveredServerInfo> liResults)
        {
            try
            {
                using (UdpClient udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0)))
                {
                    udpClient.Client.SendTimeout = 1000;
                    udpClient.EnableBroadcast = true;

                    await Ipv4BroadcastDiscoveryPacketsAsync(udpClient, reqPacket);
                    LowResStopWatch sw = new LowResStopWatch(true);
                    while (sw.ElapsedMilliseconds < TIMEOUT)
                    {
                        Task timeout = Task.Delay(TIMEOUT);
                        Task<UdpReceiveResult> recv = udpClient.ReceiveAsync();
                        if (await Task.WhenAny(timeout, recv) == recv)
                        {
                            HandleAnswer(await recv, liResults, "IPv4 Broadcast");
                        }
                        else
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.d(TAG, "IPv4 UDP Socket Error: " + e.Message);
            }
        }

        private async Task DiscoverAllIPv6Async(BinaryWriter reqPacket, List<DiscoveredServerInfo> liResults)
        {
            // Give IPv4 Broadcast a bit of a headstart, because IPv4 results tend to be a bit more robust and easier to read for the user
            await Task.Delay(700);
            try
            {
                using (UdpClient udpV6Client = new UdpClient(new IPEndPoint(IPAddress.IPv6Any, 0)))
                {
                    udpV6Client.Client.SendTimeout = 1000;

                    await Ipv6MulticastDiscoveryPacketsAsync(udpV6Client, reqPacket);
                    LowResStopWatch sw = new LowResStopWatch(true);
                    while (sw.ElapsedMilliseconds < TIMEOUT - 700)
                    {
                        Task timeout = Task.Delay(TIMEOUT - 700);
                        Task<UdpReceiveResult> recv = udpV6Client.ReceiveAsync();
                        if (await Task.WhenAny(timeout, recv) == recv)
                        {
                            HandleAnswer(await recv, liResults, "IPv6 Multicast");
                        }
                        else
                            break;
                    }

                }
            }
            catch (Exception e)
            {
                Log.d(TAG, "IPv6 UDP Socket Error: " + e.Message);
            }
        }

        private void HandleAnswer(UdpReceiveResult answer, List<DiscoveredServerInfo> liResults, String strSource)
        {
            DiscoveryPacket recvPacket = DiscoveryPacket.ReadPacket(answer.Buffer);
            if (recvPacket.IsValid)
            {
                if (recvPacket.IsAnswer() && recvPacket is DiscoveryPacket_Answer recvAnswer)
                {
                    m_bRemoteMinVersionError = recvAnswer.MinProtocolVer > BBProtocol.CURRENT_PROTOCOL_VERSION;
                    m_bLocalMinVersionError = recvAnswer.MaxProtocolVer < BBProtocol.MIN_PROTOCOL_VERSION;
                    if (!m_bLocalMinVersionError && !m_bRemoteMinVersionError)
                    {
                        DiscoveredServerInfo info = new DiscoveredServerInfo(recvAnswer, answer.RemoteEndPoint.Address);
                        if (!liResults.Contains(info))
                        {
                            liResults.Add(info);
                            // Only return the server as First Result (to be connected to) if it's the Default Server or no Default has been set
                            if (Properties.Settings.Default.DefaultServer == 0 || info.ServerGUID == Properties.Settings.Default.DefaultServer)
                                m_wbFirstResult.Post(info);
                            AddFoundServer(info);
                            Log.d(TAG, "Recveid answer from " + strSource + " - " + info.getDebugString());
                        }
                        else
                        {
                            Log.d(TAG, "Ignored duplicate answer from " + strSource + " - " + info.getDebugString());
                        }
                    }
                    else
                    {
                        
                        Log.d(TAG, "Protocolversion not supported in answer from " + strSource);
                    }
                }
                else
                {
                    Log.e(TAG, "Unknown/unexpected packet received from " + answer.RemoteEndPoint);
                }
            }
            else
            {
                Log.e(TAG, "Invalid packet received from " + answer.RemoteEndPoint + " - " + recvPacket.ParseError);
            }
        }

        public async Task<DiscoveredServerInfo> DiscoverReturnFirstAsync()
        {
            Task discoverAllTask = DiscoverAllAsync();
            Task<DiscoveredServerInfo> firstTask = m_wbFirstResult.ReceiveAsync();
            Task res = await Task.WhenAny(discoverAllTask, firstTask);

            try
            {
                if (res == firstTask)
                    return await firstTask;
                else
                {
                    await discoverAllTask;
                    if (FoundServer.Count > 0)
                        return FoundServer[0];
                    else
                        return null;
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        private void AddFoundServer(DiscoveredServerInfo server)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(new Action(() => AddFoundServer(server)));
            }
            if (!FoundServer.Contains(server))
                FoundServer.Add(server);
        }

        private async Task Ipv6MulticastDiscoveryPacketsAsync(UdpClient udpClient, BinaryWriter reqPacket)
        {
            // we want a site local multicast, but it somtimes only seems to work with a scope id attached (identifying the NIC)
            // so go through all interfaces and get the scope ID of each which has the sitelocal multicast ip
            IPAddress multicastIP = IPAddress.Parse("ff02::1");

            try
            {
                // Try once without scope anyway
                Log.d(TAG, "Trying Multicast Address: " + multicastIP);
                udpClient.JoinMulticastGroup(multicastIP);
                await udpClient.SendAsync(reqPacket, multicastIP, BBProtocol.DISCOVERYPORT);
                udpClient.DropMulticastGroup(multicastIP);

                NetworkInterface[] Interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface Interface in Interfaces)
                {
                    if (Interface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (Interface.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (MulticastIPAddressInformation mci in Interface.GetIPProperties().MulticastAddresses)
                    {
                        if (mci.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            if (multicastIP.GetAddressBytes().SequenceEqual(mci.Address.GetAddressBytes()))
                            {
                                //Log.d(TAG, "Trying Multicast Address: " + mci.Address);
                                udpClient.JoinMulticastGroup(mci.Address);
                                await udpClient.SendAsync(reqPacket, mci.Address, BBProtocol.DISCOVERYPORT);
                                udpClient.DropMulticastGroup(mci.Address);
                            }
                        }
                    }
                }
            }
            catch (Exception e) when (e is SocketException || e is InvalidOperationException || e is ObjectDisposedException)
            {
                Log.e(TAG, "Error while sending IPv6 Multicast UDP packet - " + e.Message);
            }
        }

        private async Task Ipv4BroadcastDiscoveryPacketsAsync(UdpClient udpClient, BinaryWriter reqPacket)
        {
            NetworkInterface[] Interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface Interface in Interfaces)
            {
                if (Interface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (Interface.OperationalStatus != OperationalStatus.Up) continue;
                UnicastIPAddressInformationCollection UnicastIPInfoCol = Interface.GetIPProperties().UnicastAddresses;
                foreach (UnicastIPAddressInformation UnicatIPInfo in UnicastIPInfoCol)
                {
                    if (UnicatIPInfo.Address.AddressFamily == AddressFamily.InterNetwork && UnicatIPInfo.Address.IsPrivateIP())
                    {
                        try
                        {
                            int len = await udpClient.SendAsync(reqPacket, UnicatIPInfo.GetBroadcastAddress(), BBProtocol.DISCOVERYPORT);
                            Log.d(TAG, "Sent broadcast packet (" + len + ") to " + UnicatIPInfo.GetBroadcastAddress());
                        }
                        catch (Exception e) when (e is SocketException || e is InvalidOperationException || e is ObjectDisposedException)
                        {
                            Log.e(TAG, "Error while sending broadcast packet to " + UnicatIPInfo.GetBroadcastAddress() + " : " + e.Message);
                        }
                    }
                }
            }
        }
    }
}
