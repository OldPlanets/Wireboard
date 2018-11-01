using Wireboard.BbEventArgs;
using Wireboard.Crypting;
using Wireboard.Properties;
using Wireboard.TcpPackets;
using Org.BouncyCastle.Crypto.Digests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Input;
using static Wireboard.TcpPackets.BbTcpPacket_StartEncryption;

namespace Wireboard
{
    public class BbRemoteServer : BbRemoteServerHistory
    {
        protected static String TAG = typeof(BbRemoteServer).Name;

        public event EventHandler<ConnectionEventArgs> ConnectionChanged;
        public event EventHandler<InputFeedbackEventArgs> ReceivedInputFeedback;
        public event EventHandler<ReceivedIconEventArgs> ReceivedIcon;
        public event EventHandler<BatteryStatusEventArgs> BatteryStatusChanged;
        public event EventHandler<ReceiveFileEventArgs> ReceivedFile;
        public event EventHandler<ShareTextEventArgs> ReceivedSharedText;
        public event EventHandler<SendFileEventArgs> ReceivedSendFileCommand;

        private TcpClient m_tcpClient;
        private CancellationTokenSource m_CancelToken;
        private readonly LowResStopWatch m_swLastAliveSign = new LowResStopWatch();
        private readonly Buffer​Block<BbTcpPacket> m_receivedPackets = new BufferBlock<BbTcpPacket>();
        private readonly List<BbTcpPacket> m_liExpectedAnswers = new List<BbTcpPacket>();
        private readonly Buffer​Block<MemoryStream> m_sendQueue= new BufferBlock<MemoryStream>(new DataflowBlockOptions() { BoundedCapacity = 20 });
        private readonly Buffer​Block<MemoryStream> m_prioritySendQueue = new BufferBlock<MemoryStream>();
        private Task m_startUpTask;
        private bool m_bShutDown = false;
        public byte UsedProtocolVersion { get; private set; } = 0;

        private DHKeyExchange m_dhKeyExchange;
        private readonly BbStreamCipher m_sendStreamCipher = new BbStreamCipher();
        private readonly BbStreamCipher m_recvStreamCipher = new BbStreamCipher();
        private EEncryptionMethod m_requestedEncryptionMethod = EEncryptionMethod.NONE;
        private byte[] m_abyServerPasswordSalt;
        private byte[] m_abyTestedSharedSecret;

        private bool m_bRequiresPassword;
        public bool Attached { get; private set; } = false;
        public bool WasAttached { get; private set; } = false;
        public bool Connecting { get; private set; } = false;
        private bool m_bAuthentificated = false;

        private bool m_bRequestedHello = false;
        private bool m_bRequestedAttach = false;

        public BbRemoteServer(IPAddress ip, ushort uPort, int nServerGUID)
        {
            ServerIP = ip;
            Port = uPort;
            ServerGUID = nServerGUID;
        }

        public BbRemoteServer(BbRemoteServerHistory src): base(src)
        {
        }


        ///<summary>
        ///thread-safe, instantly returns
        ///</summary>
        public bool SendPacket(BbTcpPacket packet)
        {
            MemoryStream[] aData = packet.WritePacket();
            if (aData != null)
            {
                foreach (MemoryStream data in aData)
                {
                    m_prioritySendQueue.Post(data);
                }
                if (packet.IsExpectingAnswer())
                {
                    lock (m_liExpectedAnswers)
                    {
                        m_liExpectedAnswers.Add(packet);
                    }
                }
                    
                return true;
            }
            else
                return false;
        }

        ///<summary>
        ///thread-safe,  Will await if the low priority queue is too full
        ///</summary>
        public async Task<bool> SendLowPriorityDataPacketAsync(BbTcpPacket packet, CancellationToken cancel)
        {
            MemoryStream[] aData = packet.WritePacket();
            if (aData != null)
            {
                foreach (MemoryStream data in aData)
                {
                    await m_sendQueue.SendAsync(data, cancel);
                }
                if (packet.IsExpectingAnswer())
                {
                    lock (m_liExpectedAnswers)
                    {
                        m_liExpectedAnswers.Add(packet);
                    }
                }
                return true;
            }
            else
                return false;
        }

        private async Task SendTask()
        {
            NetworkStream stream;
            try
            {
                stream = m_tcpClient.GetStream();
            }
            catch (InvalidOperationException e)
            {
                throw new IOException("Send: Unable to get stream (not connected?) - " + e.Message, e);
            }
            while (true)
            {
                try
                {
                    MemoryStream data = null;
                    while (!m_prioritySendQueue.TryReceive(out data) && !m_sendQueue.TryReceive(out data))
                    {
                        await Task.WhenAny(m_prioritySendQueue.OutputAvailableAsync(m_CancelToken.Token), m_sendQueue.OutputAvailableAsync(m_CancelToken.Token));
                        m_CancelToken.Token.ThrowIfCancellationRequested();
                    }
                    if (data == null)
                        throw new IOException("No data)");

                    if (m_sendStreamCipher.IsReady)
                        m_sendStreamCipher.Crypt(data.GetBuffer(), (int)data.Position, (int)data.Remaining());

                    await stream.WriteAsync(data.GetBuffer(), (int)data.Position, (int)data.Remaining(), m_CancelToken.Token);
                    //Log.d(TAG, "Sent packet len " + (int)data.Remaining());
                }
                catch (Exception e) when (e is IOException || e is NotSupportedException || e is ObjectDisposedException)
                {
                    Log.e(TAG, "Error while writing: " + e.Message);
                    throw new IOException("Error while writing", e);
                }
                m_CancelToken.Token.ThrowIfCancellationRequested();
            }
        }

        private async Task ReceiveTask()
        {
            NetworkStream stream;
            try
            {
                stream = m_tcpClient.GetStream();
            }
            catch (InvalidOperationException e)
            {
                throw new IOException("Receive: Unable to get stream (not connected?) - " + e.Message, e);
            }
            List<BbTcpPacket_Fragment> liFragments = new List<BbTcpPacket_Fragment>();
            while (true)
            {
                m_CancelToken.Token.ThrowIfCancellationRequested();
                try
                {
                    byte[] byHeader = new byte[BBProtocol.TCP_HEADERSIZE];
                    int read = 0;
                    while (read != byHeader.Length)
                    {
                        int len = await stream.ReadAsync(byHeader, read, byHeader.Length - read, m_CancelToken.Token);
                        if (len <= 0)
                            throw new IOException("Read < 0");
                       read += len;
                        m_CancelToken.Token.ThrowIfCancellationRequested();
                    }

                    if (m_recvStreamCipher.IsReady)
                        m_recvStreamCipher.Crypt(byHeader);

                    BbTcpPacket newPacket = new BbTcpPacket(byHeader);
                    if (newPacket.IsValid)
                    {
                        if (newPacket.IsComplete)
                        {
                            newPacket = newPacket.ReadPacket(new byte[0], UsedProtocolVersion);
                        }
                        else
                        {
                            byte[] byBody = new byte[newPacket.BodyLength];
                            read = 0;
                            while (read != byBody.Length)
                            {
                                int len = await stream.ReadAsync(byBody, read, byBody.Length - read, m_CancelToken.Token);
                                if (len <= 0)
                                    throw new IOException("Read < 0");
                                read += len;
                                m_CancelToken.Token.ThrowIfCancellationRequested();
                            }

                            if (m_recvStreamCipher.IsReady)
                                m_recvStreamCipher.Crypt(byBody);

                            newPacket = newPacket.ReadPacket(byBody, UsedProtocolVersion);
                        }

                        if (newPacket.IsValid && newPacket.IsComplete)
                        {
                            if (newPacket is BbTcpPacket_Fragment)
                            {
                                Log.d(TAG, "Fragment received");
                                BbTcpPacket_Fragment found = liFragments.Find(x => x.FragmentID == ((BbTcpPacket_Fragment)newPacket).FragmentID);
                                if (found == null)
                                {
                                    liFragments.Add((BbTcpPacket_Fragment)newPacket);
                                }
                                else if (found.addFragment((BbTcpPacket_Fragment)newPacket)) // add. complete?
                                {
                                    newPacket = found.assemblePacket();
                                    liFragments.Remove(found);
                                    if (newPacket != null)
                                    {
                                        Log.d(TAG, "Fragment completed from Opcode: " + newPacket.DbgGetOpcode());
                                        m_receivedPackets.Post(newPacket);
                                    }
                                    else
                                    {
                                        Log.d(TAG, "Error while completing Fragment: " + found.ParseError);
                                        throw new IOException("Error while completing Fragment: " + found.ParseError);
                                    }
                                }
                            }
                            else
                            {
                                //Log.d(TAG, "New Packet received");
                                m_receivedPackets.Post(newPacket);
                            }
                        }
                        else
                        {
                            Log.e(TAG, "Invalid Packet (body) received:" + newPacket.ParseError);
                            throw new IOException("Invalid Packet (body) received:" + newPacket.ParseError);
                        }
                    }
                    else
                    {
                        Log.e(TAG, "Invalid Packet (header) received:" + newPacket.ParseError);
                        throw new IOException("Invalid Packet (header) received:" + newPacket.ParseError);
                    }
                }
                catch (Exception e) when (e is NotSupportedException || e is ObjectDisposedException)
                {
                    Log.e(TAG, "Error while reading: " + e.Message + " stack: " + e.StackTrace);
                    throw new IOException("Error while reading", e);
                }

            }
        }

        public async Task<bool> ConnectAsync()
        {
            if (m_bShutDown)
                return false;
            if (m_tcpClient == null)
            {
                Connecting = true;
                try
                {
                    m_tcpClient = new TcpClient(new IPEndPoint((ServerIP.AddressFamily == AddressFamily.InterNetworkV6) ? IPAddress.IPv6Any : IPAddress.Any, 0));
                }
                catch(SocketException e)
                {
                    Log.d(TAG, "Couldn't create local socket - " + e.Message);
                    return false;
                }

                Log.d(TAG, "Trying to connect to " + ServerIP + ":" + Port);
                try
                {
                    await m_tcpClient.ConnectAsync(ServerIP, Port);
                    m_tcpClient.NoDelay = true;
                    m_tcpClient.LingerState = new LingerOption(true, 0);
                }
                catch (SocketException e)
                {
                    Log.d(TAG, "Unable to connect to " + ServerIP + ":" + Port + " - " + e.Message);
                    m_tcpClient.Dispose();
                    m_tcpClient = null;
                    Connecting = false;
                    return false;
                }
                if (m_bShutDown)
                    return false;
                m_CancelToken = new CancellationTokenSource();
                m_startUpTask = StartUp();

                BbTcpPacket_Hello_Req helloPacket = new BbTcpPacket_Hello_Req(BBProtocol.CURRENT_PROTOCOL_VERSION, BBProtocol.MIN_PROTOCOL_VERSION, false, Environment.MachineName
                    , OwnGUID, System.Reflection.Assembly.GetEntryAssembly().GetName().Version);
                m_bRequestedHello = true;
                SendPacket(helloPacket);

                Log.d(TAG, "TCP Connected to " + ServerIP + ":" + Port + " - " + m_tcpClient.Connected);
            }

            return m_tcpClient.Connected;
        }

        private async Task HandlePackets()
        {
            while (!m_bShutDown)
            {
                BbTcpPacket packet;
                try
                {
                    packet = await m_receivedPackets.ReceiveAsync(m_CancelToken.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                lock (m_liExpectedAnswers)
                {
                    m_liExpectedAnswers.RemoveAll(x => x.GetExpectedAnswer() == packet.Opcode);
                }
                m_swLastAliveSign.Start();
                await HandlePacket(packet);
            }
        }

        private async Task CheckForTimeouts()
        {
            while (!m_bShutDown)
            {
                try
                {
                    await Task.Delay(500, m_CancelToken.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                BbTcpPacket timedout = null;
                lock (m_liExpectedAnswers)
                {
                    timedout = m_liExpectedAnswers.Find(x => x.IsExpectedAnswerTimedOut());
                }
                if (timedout != null)
                {
                    Log.w(TAG, "Didn't receive expected answer in time from " + ServerName + " Expected Answer Opcode: " + timedout.Opcode);
                    Disconnect(false, false);
                }
            }
        }

        private async Task HandlePacket(BbTcpPacket packet)
        {
            if (packet is BbTcpPacket_Hello_Ans && m_bRequestedHello)
            {
                m_bRequestedHello = false;
                BbTcpPacket_Hello_Ans hello_ans = (BbTcpPacket_Hello_Ans)packet;
                ServerName = hello_ans.Desc;
                ServerGUID = hello_ans.ServerGUID;
                ServerMinProtocolVersion = hello_ans.MinSupportedVersion;
                m_bRequiresPassword = hello_ans.RequiresPassword;
                UsedProtocolVersion = (byte)Math.Min(hello_ans.MaxSupportedVersion, BBProtocol.CURRENT_PROTOCOL_VERSION);
                if (UsedProtocolVersion < hello_ans.MinSupportedVersion || UsedProtocolVersion < BBProtocol.MIN_PROTOCOL_VERSION)
                {
                    ProtocolIncompatible = true;
                    Log.w(TAG, "Server supported protocol versions incompatible - " + ServerName);
                    Disconnect(false, true);
                }
                Log.d(TAG, "Received Hello Answer from " + ServerName + " Protocol Version: " + UsedProtocolVersion + " Needs Password: " + m_bRequiresPassword);
                Attach();
            }
            else if (packet is BbTcpPacket_Pink_ACK)
            {
                Log.d(TAG, "Got Pong");
            }
            else if (packet is BbTcpPacket_EncryptionACK crypt)
            {
                await ContinueEncryptionNegotiation(crypt);
            }
            else if (packet is BbTcpPacket_Encryption_AuthAns auth)
            {
                if (m_abyTestedSharedSecret != null)
                {
                    if (auth.ErrorCode == 0)
                    {
                        Log.d(TAG, "DH+Auth negotiated encryption key for session is: " + BitConverter.ToString(m_abyTestedSharedSecret));
                        m_sendStreamCipher.Init(m_abyTestedSharedSecret);
                        m_recvStreamCipher.Init(m_abyTestedSharedSecret);
                        m_bAuthentificated = true;
                        m_abyTestedSharedSecret = null;
                        m_dhKeyExchange = null;
                        m_requestedEncryptionMethod = EEncryptionMethod.NONE;
                        Log.d(TAG, "Password accepted, DH+Auth Encryption negotiation finished, transmissions will now be encrypted with " + m_sendStreamCipher.Algorithm);
                        Attach();
                    }
                    else
                    {
                        Log.w(TAG, "Challenge failed - Password rejected. Trying again.");
                        m_abyTestedSharedSecret = null;
                        await Authenticate(true);
                    }
                }
                else
                {
                    Log.e(TAG, "Unexpected/Unrequested Encryption_AuthAns packet");
                    Disconnect(false, false);
                }
            }
            else if (packet is BbTcpPacket_Attach_Ans attach_ans && m_bRequestedAttach)
            {
                m_bRequestedAttach = false;
                if (attach_ans.AttachResult == EAttachResult.ACCEPTED)
                {
                    Attached = true;
                    WasAttached = true;
                    ConnectedTime.Start();
                    Connecting = false;
                    ConnectionChanged?.Invoke(this, new ConnectionEventArgs(ConnectionEventArgs.EState.CONNECTED, SessionID));
                    Log.d(TAG, "Attached to " + ServerName);
                }
                else
                {
                    Log.d(TAG, "Cannot Attach to " + ServerName + " Error: " + attach_ans.AttachResult);
                    Disconnect(false, false);
                }
            }
            else if (packet is BbTcpPacket_InputFocusChange input && Attached)
            {
                ReceivedInputFeedback?.Invoke(this, new InputFeedbackEventArgs(InputFeedbackEventArgs.EInputEvent.FOCUSCHANGE, input.ImeOptions, input.InputType, input.Hint
                    , input.PackageName, input.Text, input.CursorPos, input.FieldID));
            }
            else if (packet is BbTcpPacket_TextSyncUpdate textsync && Attached)
            {
                ReceivedInputFeedback?.Invoke(this, new InputFeedbackEventArgs(InputFeedbackEventArgs.EInputEvent.TEXTUPDATE
                    , textsync.Text, textsync.CursorPos, textsync.LastProcessedInputID));
            }
            else if (packet is BbTcpPacket_SendIcon icon && Attached)
            {
                Log.d(TAG, "Received Icon packet");
                ReceivedIcon?.Invoke(this, new ReceivedIconEventArgs(icon.PackageName, icon.Image));
            }
            else if (packet is BbTcpPacket_BatteryStatus battery && Attached)
            {
                Log.d(TAG, "Received BatteryStatus packet");
                BatteryStatusChanged?.Invoke(this, new BatteryStatusEventArgs(battery.BatteryLevel, battery.IsPluggedIn, battery.IsCharging));

            }
            else if (packet is BbTcpPacket_StartSendFile sendfile && Attached)
            {
                Log.d(TAG, "Received New File packet");
                ReceiveFileEventArgs fileEventArgs = new ReceiveFileEventArgs(sendfile.FileName, sendfile.FileType, sendfile.FileID, sendfile.FileSize);
                ReceivedFile?.Invoke(this, fileEventArgs);
                if (ReceivedFile == null || fileEventArgs.CancelFile)
                {
                    SendPacket(new BbTcpPacket_CommandSendFile(UsedProtocolVersion, sendfile.FileID, BbTcpPacket_CommandSendFile.EFileCommand.CANCEL, false, null));
                }
                else
                {
                    SendPacket(new BbTcpPacket_CommandSendFile(UsedProtocolVersion, sendfile.FileID, BbTcpPacket_CommandSendFile.EFileCommand.ACCEPT, false, null));
                }
            }
            else if (packet is BbTcpPacket_CommandSendFile filecommand && Attached)
            {
                Log.d(TAG, "Received File Command Packet for FileID " + filecommand.FileID);
                if (filecommand.IsSender)
                {
                    if (filecommand.Command == BbTcpPacket_CommandSendFile.EFileCommand.CANCEL)
                    {
                        ReceiveFileEventArgs fileEventArgs = new ReceiveFileEventArgs(filecommand.FileID, ReceiveFileEventArgs.EFileEvent.CANCELFILE);
                        ReceivedFile?.Invoke(this, fileEventArgs);
                    }
                }
                else
                {
                    if (filecommand.Command == BbTcpPacket_CommandSendFile.EFileCommand.ACCEPT)
                        ReceivedSendFileCommand?.Invoke(this, new SendFileEventArgs(SendFileEventArgs.EFileEvent.ACCEPT, filecommand.FileID, filecommand.ErrorMessage));
                    else if (filecommand.Command == BbTcpPacket_CommandSendFile.EFileCommand.CANCEL)
                        ReceivedSendFileCommand?.Invoke(this, new SendFileEventArgs(SendFileEventArgs.EFileEvent.CANCELFILE, filecommand.FileID, filecommand.ErrorMessage));
                }
            }
            else if (packet is BbTcpPacket_SendFileData filedata && Attached)
            {
                //Log.d(TAG, "Received File data Packet, Size " + packet.BodyLength);
                ReceiveFileEventArgs fileEventArgs = new ReceiveFileEventArgs(filedata.FileID, filedata.StartPosition, filedata.FileData);
                ReceivedFile?.Invoke(this, fileEventArgs);
                if (fileEventArgs.CancelFile)
                {
                    SendPacket(new BbTcpPacket_CommandSendFile(UsedProtocolVersion, filedata.FileID, BbTcpPacket_CommandSendFile.EFileCommand.CANCEL, false, null));
                }
            }
            else if (packet is BbTcpPacket_ShareText sharetext && Attached)
            {
                //Log.d(TAG, "Received ShareText Packet, Text: " + sharetext.Text + " Type: " + sharetext.Type);
                ReceivedSharedText?.Invoke(this, new ShareTextEventArgs(sharetext.Text, sharetext.Type, null, false));
            }
            else if (packet is BbTcpPacket_ClipboardContent clipboard && Attached)
            {
                //Log.d(TAG, "Received Clipboard Packet, Text: " + clipboard.ContentPlainText);
                ReceivedSharedText?.Invoke(this, new ShareTextEventArgs(clipboard.ContentPlainText, "", clipboard.ContentHtmlText, true));
            }
            else
                Log.w(TAG, "Received unhandled package, Opcode: " + packet.Opcode);
        }

        private void Attach()
        {
            if (!m_sendStreamCipher.IsReady)
            {
                InitiateEncryption(m_bRequiresPassword);
                return;
            }
            BbTcpPacket_Attach_Req packet = new BbTcpPacket_Attach_Req(UsedProtocolVersion, SessionID, Settings.Default.PrefInitialDisplayLock
                , Settings.Default.ScreenlockBright, BbSharedClipboard.IsSharedRemote);
            m_bRequestedAttach = true;
            SendPacket(packet);
            Log.d(TAG, "Sent Attach Request to " + ServerName);
        }

        private void InitiateEncryption(bool bAuthenticate)
        {
            if (!m_bAuthentificated && !m_sendStreamCipher.IsReady)
            {
                m_dhKeyExchange = new DHKeyExchange();
                m_requestedEncryptionMethod = bAuthenticate ? EEncryptionMethod.DH_PLUS_AUTH : EEncryptionMethod.DHAGREEMENT;
                BbTcpPacket_StartEncryption packet = new BbTcpPacket_StartEncryption(UsedProtocolVersion, m_requestedEncryptionMethod
                    , m_dhKeyExchange.GetPublicKey(), m_sendStreamCipher.GenerateIV(), m_recvStreamCipher.GenerateIV());
                SendPacket(packet);
            }
        }

        private async Task ContinueEncryptionNegotiation(BbTcpPacket_EncryptionACK crypt)
        {
            try
            {
                if (m_sendStreamCipher.IsReady)
                    throw new Exception("Encryption already active, unexpected packet");

                if (crypt.ErrorCode != 0)
                    throw new Exception("Remote Errorcode: " + crypt.ErrorCode);

                byte[] magicValue = crypt.EncryptedMagicValue;
                if (crypt.EncryptionMethod == EEncryptionMethod.DHAGREEMENT)
                {
                    if (m_dhKeyExchange == null || m_requestedEncryptionMethod != EEncryptionMethod.DHAGREEMENT)
                        throw new Exception("Wrong encryption method in remote answer");

                    byte[] sharedSecret = m_dhKeyExchange.CalculateSharedSecret(crypt.RemotePublicKey);
                    Log.d(TAG, "DH negotiated encryption key for session is: " + BitConverter.ToString(sharedSecret));
                    m_sendStreamCipher.Init(sharedSecret);
                    m_recvStreamCipher.Init(sharedSecret);

                    m_recvStreamCipher.Crypt(magicValue);
                    if (!magicValue.SequenceEqual(BBProtocol.ENCRYPTION_MAGICVALUE))
                        throw new Exception("MagicValue Mismatch");

                    Log.d(TAG, "DH Encryption negotiation finished, transmissions will now be encrypted with " + m_sendStreamCipher.Algorithm);
                    m_dhKeyExchange = null;
                    m_requestedEncryptionMethod = EEncryptionMethod.NONE;
                    Attach();

                }
                else if (crypt.EncryptionMethod == EEncryptionMethod.DH_PLUS_AUTH)
                {
                    if (m_dhKeyExchange == null || m_requestedEncryptionMethod != EEncryptionMethod.DH_PLUS_AUTH)
                        throw new Exception("Wrong encryption method in remote answer");

                    // check if the DH Part succeeded
                    (new BbStreamCipher()).Init(m_dhKeyExchange.CalculateSharedSecret(crypt.RemotePublicKey), m_recvStreamCipher.IV).Crypt(magicValue);
                    if (!magicValue.SequenceEqual(BBProtocol.ENCRYPTION_MAGICVALUE))
                        throw new Exception("MagicValue Mismatch");
                    m_abyServerPasswordSalt = crypt.PasswordSalt;
                    Log.d(TAG, "DH Encryption step for Auth finished");
                    await Authenticate(false);
                }
            }
            catch (Exception e)
            {
                Log.e(TAG, "Error while negotiating encrpytion: " + e.Message);
                Disconnect(false, false);
                return;
            }
        }

        private async Task Authenticate(bool bFailedBefore)
        {
            if (m_dhKeyExchange == null || m_dhKeyExchange.SharedSecret == null || m_abyServerPasswordSalt == null || m_requestedEncryptionMethod != EEncryptionMethod.DH_PLUS_AUTH)
            {
                Log.e(TAG, "State mismatch during authenication, disconnecting from " + ServerName);
                Disconnect(false, false);
                return;
            }
            // get the password
            byte[] abyServerSecret = await BbPasswordManager.GetKeyForServer(ServerGUID, ServerName, m_abyServerPasswordSalt, bFailedBefore, m_CancelToken.Token);
            if (abyServerSecret == null)
            {
                // user hit cancel on the login dialog or the connection was dropped and cancellation requested
                Log.d(TAG, "No password available, disconnecting from server " + ServerName);
                Disconnect(false, true);
                return;
            }
            // calculate the suspected shared secret, so our future cipher keys
            Sha256Digest digest = new Sha256Digest();
            m_abyTestedSharedSecret = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(m_dhKeyExchange.SharedSecret, 0, m_dhKeyExchange.SharedSecret.Length);
            digest.BlockUpdate(abyServerSecret, 0, abyServerSecret.Length);
            digest.DoFinal(m_abyTestedSharedSecret, 0);

            // response to the challenge to find out if the password was right
            digest.Reset();
            byte[] abyChallangeResponse = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(m_abyTestedSharedSecret, 0, m_abyTestedSharedSecret.Length);
            digest.BlockUpdate(BBProtocol.ENCRYPTION_MAGICVALUE, 0, BBProtocol.ENCRYPTION_MAGICVALUE.Length); // not really needed, doesn't do any harm neither
            digest.DoFinal(abyChallangeResponse, 0);

            SendPacket(new BbTcpPacket_Encryption_AuthReq(UsedProtocolVersion, abyChallangeResponse));
        }

        private async Task StartUp()
        {
            Task recvT = Task.Run(() => ReceiveTask(), m_CancelToken.Token);
            Task sendT = Task.Run(() => SendTask(), m_CancelToken.Token);
            Task handleT = HandlePackets();
            Task timeoutT = CheckForTimeouts();
            try
            {
                await await Task.WhenAny(recvT, sendT, handleT, timeoutT);
            }
            catch (IOException e)
            {
                Log.w(TAG, "Socket or Protocol error, disconnecting from " + ServerIP + " - " + e.Message);
                Disconnect(false, false);
            }
            catch (OperationCanceledException)
            {
                Log.d(TAG, "Socket read/write Tasks stopped");
            }
            catch (Exception e)
            {
                Log.e(TAG, "Unexpected / unhandled exception in server code, disconnecting - " + e.Message);
                Disconnect(false, false);
            }
            await Task.WhenAll(recvT, sendT, handleT, timeoutT);
        }

        private void Disconnect(bool bUserRequested, bool bPersistentError)
        {          
            if (!m_bShutDown && m_tcpClient != null && m_CancelToken?.IsCancellationRequested != true)
            {
                m_bShutDown = true;
                if (m_CancelToken == null)
                {
                    // connecting, before a tcp connection has been established
                    try
                    {
                        m_tcpClient.Client.Disconnect(false);
                    }
                    catch (SocketException) { }
                    Connecting = false;
                }
                else
                {
                    // connected stage
                    try
                    {
                        m_CancelToken.Cancel();
                    }
                    catch (AggregateException ae)
                    {
                        foreach (Exception e in ae.InnerExceptions)
                        {
                            Log.w(TAG, "Cancel Exception: " + e.GetType().Name);
                        }
                    }
                    //m_tcpClient.Close();
                    try
                    {
                        m_tcpClient.Client.Disconnect(false);
                    }
                    catch (SocketException) { }
                    ConnectedTime.Stop();
                    DisconnectedTime.Start();
                    Attached = false;
                    Connecting = false;
                    Log.i(TAG, "Disconnected from " + ServerName + " (" + ServerIP + ")", true);
                    ConnectionChanged?.Invoke(this, new ConnectionEventArgs(ConnectionEventArgs.EState.DISCONNECTED, SessionID, bUserRequested
                        , bPersistentError, ProtocolIncompatible && ServerMinProtocolVersion > BBProtocol.CURRENT_PROTOCOL_VERSION
                        , ProtocolIncompatible && UsedProtocolVersion < BBProtocol.MIN_PROTOCOL_VERSION));
                }
            }
        }

        public async Task CloseAsync()
        {
            if (m_tcpClient == null)
                return;
            if (!m_bShutDown || m_CancelToken?.IsCancellationRequested != true)
            {
                Disconnect(true, false);
            }
            try
            {
                if (m_startUpTask != null)
                    await m_startUpTask;
            }
            catch (IOException e)
            {
                Log.w(TAG, "Expected Cancel Exception: " + e.Message);
            }
            catch (Exception e)
            {
                Log.w(TAG, "Unexpected Cancel Exception: " + e.Message);
            }
            m_CancelToken?.Dispose();
        }

        public void SendSpecialKey(Key key, int nModifier, int nCursorPos, int nInputEventID)
        {
            if (!Attached)
                return;

            BbTcpPacket_SendKey packetKey = new BbTcpPacket_SendKey(UsedProtocolVersion, nInputEventID, (int)key, nModifier, nCursorPos);
            SendPacket(packetKey);
            //Log.d(TAG, "Sent Key Input: " + key);
        }

        public void SendTextInput(String strText, uint nCursorPos, int nTextAfterCursor, int nInputEventID)
        {
            if (Attached)
                SendPacket(new BbTcpPacket_SendText(UsedProtocolVersion, nInputEventID, strText, nCursorPos, nTextAfterCursor));
            //Log.d(TAG, "Sent Text Input: " + strText);
        }

        public void SendTextRemove(uint nCursorPos, uint nLength, int nInputEventID)
        {
            if (Attached)
                SendPacket(new BbTcpPacket_RemoveText(UsedProtocolVersion, nInputEventID, nCursorPos, nLength));
            //Log.d(TAG, "Sent Text Remove: " + nLength);
        }

        public void SendDoImeAction()
        {
            if (Attached)
                SendPacket(new BbTcpPacket_DoImeAction(UsedProtocolVersion));
        }

        public void SendSetDisplayLock(bool bSet, bool bBright)
        {
            if (Attached)
                SendPacket(new BbTcpPacket_SetDisplayLock(UsedProtocolVersion, bSet, bBright));
        }

        public void SendIconRequest(String strPackageName)
        {
            if (Attached)
                SendPacket(new BbTcpPacket_RequestIcon(UsedProtocolVersion, strPackageName));
        }

        public void SendSwitchApp(String strPackageName, int nFieldID)
        {
            if (Attached)
                SendPacket(new BbTcpPacket_SwitchApp(UsedProtocolVersion, strPackageName, nFieldID));
        }

        public void SendFileStart(String strFileName, String strFileType, int nFileID, ulong lFileSize)
        {
            if (Attached)
                SendPacket(new BbTcpPacket_StartSendFile(UsedProtocolVersion, strFileName, strFileType, nFileID, lFileSize));
        }

        public void SendFileCancel(int nFileID, bool bSender)
        {
            if (Attached)
                SendPacket(new BbTcpPacket_CommandSendFile(UsedProtocolVersion, nFileID, BbTcpPacket_CommandSendFile.EFileCommand.CANCEL, bSender, null));
        }

        public void CheckAlive()
        {
            if (m_swLastAliveSign.ElapsedMilliseconds > 10000)
            {
                Log.d(TAG, "Sending Ping");
                SendPacket(new BbTcpPacket_Ping(UsedProtocolVersion));
                m_swLastAliveSign.Start();
            }
        }

        public void SendClipboardContent(String strContentPlainText, String strContentHtmlText)
        {
            if (Attached)
                SendPacket(new BbTcpPacket_ClipboardContent(UsedProtocolVersion, strContentPlainText, strContentHtmlText));
        }

        public void SendShareRemoteClipboard(bool bNewMode)
        {
            if (Attached)
                SendPacket(new BbTcpPacket_OptionsChange(UsedProtocolVersion, bNewMode));
        }
    }
}
