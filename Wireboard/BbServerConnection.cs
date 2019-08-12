using Wireboard.BbEventArgs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Wireboard
{
    public class BbServerConnection : INotifyPropertyChanged
    {
        enum EConnectMethod { AUTO = 1, MANUAL = 2}
        private static String TAG = typeof(BbServerConnection).Name;

        public ReceiveFilesManager ReceiveFilesManager { get; } = new ReceiveFilesManager();
        public SendFilesManager SendFilesManager { get; } = new SendFilesManager();
        public AndroidScreenCapture ScreenCapture { get; } = new AndroidScreenCapture();
        public BbDiscoveryFinder DiscoveryFinder { get; set; } = new BbDiscoveryFinder();
        public BbRemoteServer CurrentServer;
        public BbRemoteServerHistory LastConnectedServer { get; private set; }
        public bool ConnectedToServer { get { return CurrentServer != null && CurrentServer.Attached; } }
        public bool IsConnecting => CurrentServer != null && CurrentServer.Connecting;
        public bool IsDiscovering => DiscoveryFinder.IsDiscovering;
        public bool IsNeverConnected { get; set; } = true;
        public bool IsConnectingOrDiscovering => !ConnectedToServer && (IsConnecting || IsDiscovering);
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler SoftwareUpdateRequired;

        private readonly MainWindow m_mainWin;
        private readonly DispatcherTimer m_timerReconnect;
        private readonly LowResStopWatch m_timeNextReconnectTry = new LowResStopWatch(false);
        

        public BbServerConnection(MainWindow mainWin)
        {
            m_mainWin = mainWin;
            DiscoveryFinder.DisoveryFinished += OnDiscoveryFinished;
            m_timerReconnect = new DispatcherTimer()
            {
                IsEnabled = false,
                Interval = TimeSpan.FromMilliseconds(1000),                
            };
        }

        public async Task<bool> ConnectAsync()
        {
            await StopReconnectingAsync();
            if (!ConnectedToServer && !IsConnecting)
            {
                EConnectMethod nConnectMethod = (EConnectMethod)Int32.Parse(Properties.Settings.Default.ConnectionMethod);
                if (nConnectMethod == EConnectMethod.MANUAL)
                {
                    
                    if (Properties.Settings.Default.ManualServerIP.IsValidIP())
                    {
                        IPAddress ip = IPAddress.Parse(Properties.Settings.Default.ManualServerIP);
                        if (String.IsNullOrEmpty(Properties.Settings.Default.ManualServerPort) || !UInt16.TryParse(Properties.Settings.Default.ManualServerPort, out ushort nPort))
                        {
                            nPort = BBProtocol.DEFAULT_TCP_PORT;
                        }
                        return await ConnectToServerAsync(ip, nPort);
                    }
                }
                return await DiscoverAndConnectAsync();
            }
            else
                return true;
        }

        public async Task DisconnectAsync()
        {
            if (CurrentServer != null)
            {
                await StopReconnectingAsync();
                await CurrentServer.CloseAsync();
            }   
        }

        private async Task<bool> DiscoverAndConnectAsync()
        {
            Task<DiscoveredServerInfo> task = DiscoveryFinder.DiscoverReturnFirstAsync();
            NotifyChange(new String[] { "IsDiscovering", "IsConnectingOrDiscovering" });
            Log.i(TAG, "Searching for " + (string)Application.Current.FindResource("AppNameCode") + " clients in local network ", true);
            DiscoveredServerInfo found = await task;
            if (found != null)
            {
                return await ConnectToServerAsync(found.ConnectionIP, found.Port, found.ServerGUID);
            }
            Log.i(TAG, "No " + (string)Application.Current.FindResource("AppNameCode") + " clients in local network found", true);
            return false;
        }

        public async Task DiscoverAsync()
        {
            if (IsDiscovering)
                return;
            await StopReconnectingAsync();
            Task task = DiscoveryFinder.DiscoverAllAsync();
            NotifyChange(new String[] { "IsDiscovering", "IsConnectingOrDiscovering" });
            await task;
        }

        private void OnConnectionEvent(object sender, ConnectionEventArgs e)
        {
            if (e.NewState == ConnectionEventArgs.EState.CONNECTED)
            {
                m_timerReconnect.IsEnabled = false;
                if (IsNeverConnected)
                {
                    IsNeverConnected = false;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsNeverConnected"));
                }
                Log.i(TAG, "Connected to " + CurrentServer.ServerName + " (" + CurrentServer.ServerIP.ToString() + ")", true);
            }
            else if (e.NewState == ConnectionEventArgs.EState.DISCONNECTED)
            {
                if (m_timerReconnect.IsEnabled)
                {
                    m_timeNextReconnectTry.StartCountDown(m_timeNextReconnectTry.CountdownInterval);
                    Log.s(TAG, "Failed to reconnect to " + LastConnectedServer.ServerName + " (Next try in "
                        + (int)Math.Ceiling(m_timeNextReconnectTry.RemainingMillisecondsToCountdown / 1000f) + "s)");
                }
                else
                {
                    if (!e.UserRequested && !e.PesistentError && Properties.Settings.Default.Reconnect && CurrentServer.WasAttached)
                        StartReconnecting(CurrentServer);
                }

                if (e.RemoteMinVersionError)
                {
                    SoftwareUpdateRequired?.Invoke(this, new EventArgs());
                }
            }
            NotifyChange(new String[] { "IsConnecting", "IsConnectingOrDiscovering", "ConnectedToServer" });
        }

        private void OnDiscoveryFinished(object sender, DiscoveryFinishedEventArgs e)
        {
            if (e.RemoteMinProtocolTooHigh)
            {
                SoftwareUpdateRequired?.Invoke(this, new EventArgs());
            }
            NotifyChange(new String[] { "IsDiscovering", "IsConnectingOrDiscovering" });
        }

        public async Task<bool> ConnectToServerAsync(IPAddress address, UInt16 nPort, int nServerGUID = 0)
        {
            return await ConnectToServerAsync(address, nPort, 0, null);
        }

        private async Task<bool> ConnectToServerAsync(IPAddress address, UInt16 nPort, int nServerGUID, BbRemoteServerHistory reconnect)
        {
            if (CurrentServer != null)
            {
                if (CurrentServer.Attached && CurrentServer.ServerGUID == nServerGUID && CurrentServer.ServerIP.Equals(address) && CurrentServer.Port == nPort)
                    return true;
                await CurrentServer.CloseAsync();
            }
            if (reconnect == null)
                Log.i(TAG, "Trying to connect to " + address.ToString() + ":" + nPort, true);
            CurrentServer = reconnect != null ? new BbRemoteServer(reconnect): new BbRemoteServer(address, nPort, nServerGUID);
            CurrentServer.ConnectionChanged += OnConnectionEvent;
            CurrentServer.ConnectionChanged += m_mainWin.AppIconManager.onConnectionEvent;
            CurrentServer.ConnectionChanged += ReceiveFilesManager.OnConnectionEvent;
            CurrentServer.ConnectionChanged += SendFilesManager.onConnectionEvent;
            CurrentServer.ConnectionChanged += ScreenCapture.OnConnectionEvent;
            CurrentServer.ConnectionChanged += m_mainWin.OnConnectionEvent;
            CurrentServer.ReceivedInputFeedback += m_mainWin.OnInputFeedbackEvent;
            CurrentServer.ReceivedIcon += m_mainWin.AppIconManager.onReceivedIcon;
            CurrentServer.BatteryStatusChanged += m_mainWin.OnBatteryStatusChanged;
            CurrentServer.ReceivedFile += ReceiveFilesManager.OnReceivedFileEvent;
            CurrentServer.ReceivedSharedText += m_mainWin.OnSharedText;
            CurrentServer.ReceivedSendFileCommand += SendFilesManager.onSendFileEvent;

            Task<bool> connectTask = CurrentServer.ConnectAsync();
            NotifyChange(new String[] { "IsConnecting", "IsConnectingOrDiscovering" });
            bool bResult = await connectTask;
            if (!bResult)
            {
                NotifyChange(new String[] { "IsConnecting" , "IsConnectingOrDiscovering" });
                if (reconnect == null)
                    Log.i(TAG, "Failed to connect to " + address.ToString() + ":" + nPort, true);
            }
            return bResult;
        }

        private void NotifyChange(String[] aStrChanged)
        {
            foreach (String s in aStrChanged)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(s));
        }

        public void CheckAlive()
        {
            if (ConnectedToServer)
                CurrentServer.CheckAlive();
        }

        private async void OnReconnectTimer_Tick(object sender, EventArgs e)
        {
            if (!Properties.Settings.Default.Reconnect)
                await StopReconnectingAsync();
            else if (!IsConnectingOrDiscovering && !ConnectedToServer && LastConnectedServer != null)
            {
                if (m_timeNextReconnectTry.IsCountdownReached)
                {
                    Log.s(TAG, "Trying to reconnect to " + LastConnectedServer.ServerName);
                    if (!await ConnectToServerAsync(null, 0, 0, LastConnectedServer))
                    {
                        m_timeNextReconnectTry.StartCountDown(m_timeNextReconnectTry.CountdownInterval);
                    }
                    else
                        return;
                }
                Log.s(TAG, "Failed to reconnect to " + LastConnectedServer.ServerName + " (Next try in " 
                    + (int)Math.Ceiling(m_timeNextReconnectTry.RemainingMillisecondsToCountdown / 1000f) + "s)");
            }
        }

        private async Task StopReconnectingAsync()
        {
            if (!m_timerReconnect.IsEnabled)
                return;
            Log.s(TAG, "Reconnecting stopped");
            m_timerReconnect.IsEnabled = false;
            m_timerReconnect.Tick -= OnReconnectTimer_Tick;
            if (IsConnecting)
                await DisconnectAsync();
        }

        private void StartReconnecting(BbRemoteServerHistory server)
        {
            if (m_timerReconnect.IsEnabled)
                return;
            Log.i(TAG, "Trying to reconnect to " + server.ServerName, true);
            LastConnectedServer = new BbRemoteServerHistory(server);
            m_timeNextReconnectTry.StartCountDown(20000);
            m_timerReconnect.Tick += OnReconnectTimer_Tick;
            m_timerReconnect.IsEnabled = true;
        }

    }
}
