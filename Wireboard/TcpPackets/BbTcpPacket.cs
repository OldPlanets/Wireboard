using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    public class BbTcpPacket
    {
        protected static String TAG = typeof(BbTcpPacket).Name;

        public bool IsValid { get; protected set; } = true;
        public bool IsComplete { get; protected set; }
        public String ParseError { get; protected set; } = "";
        protected byte m_byProtocolVersion = 0;
        public byte Opcode { get; protected set; } = 0x00;
        public int BodyLength { get; protected set; } = 0;
        private static int s_nLastUsedFragmentID;
        protected int MaxFragmentSizeWithoutHeader => BBProtocol.MAX_TCPPACKETSIZE_WITHOUTHEADER;

        private LowResStopWatch m_swCreated = new LowResStopWatch(true);
        protected LowResStopWatch m_answerTimer = new LowResStopWatch(false);

        public BbTcpPacket(byte[] byHeader)
        {
            IsValid = false;
            IsComplete = false;
            try
            {
                using (BinaryReader buf = new BinaryReader(new MemoryStream(byHeader)))
                {

                    ushort shortHeaderValue = buf.ReadUInt16();
                    if (shortHeaderValue != BBProtocol.PROTOCOL_HEADER)
                    {
                        ParseError = "Protocol header mismatch, not a packet we are looking for";
                        return;
                    }
                    BodyLength = buf.ReadUInt16();
                    Opcode = buf.ReadByte();

                    if (BodyLength > BBProtocol.MAX_TCPPACKETSIZE_WITHOUTHEADER)
                    {
                        ParseError = "Invalid Packet size";
                        return;
                    }
                    else if (BodyLength == 0)
                    {
                        IsComplete = true;
                    }
                    IsValid = true;
                }
            }
            catch (IOException e)
            {
                ParseError = "Exception while reading packet: " + e.Message;
                return;
            }
        }

        protected BbTcpPacket(BbTcpPacket src)
        {
            IsValid = src.IsValid;
            Opcode = src.Opcode;
            ParseError = src.ParseError;
            IsComplete = src.IsComplete;
            m_byProtocolVersion = src.m_byProtocolVersion;
            BodyLength = src.BodyLength;
        }

        protected BbTcpPacket(byte byOpcode, byte byProtolVersion)
        {
            IsValid = true;
            IsComplete = true;
            Opcode = byOpcode;
            m_byProtocolVersion = byProtolVersion;
        }

        internal BbTcpPacket(byte byOpcode, int nBodyLength)
        {
            IsValid = true;
            IsComplete = false;
            Opcode = byOpcode;
            BodyLength = nBodyLength;
        }

        protected static void WriteHeader(BinaryWriter buf, byte byOpcode)
        {
            long pos = buf.BaseStream.Position;
            buf.BaseStream.SetLength(pos);
            buf.Seek(0, SeekOrigin.Begin);
            buf.Write(BBProtocol.PROTOCOL_HEADER);
            buf.Write((ushort)(pos - BBProtocol.DISCOVERY_HEADERSIZE));
            buf.Write(byOpcode);
            buf.Seek(0, SeekOrigin.Begin);
        }

        protected static MemoryStream[] CreateFragments(BinaryWriter completePacket, byte byOpcode)
        {
            MemoryStream buf = (MemoryStream)completePacket.BaseStream;
            if (buf.Position - BBProtocol.TCP_HEADERSIZE <= BBProtocol.MAX_TCPPACKETSIZE_WITHOUTHEADER)
            {
                WriteHeader(completePacket, byOpcode);
                MemoryStream[] res = new MemoryStream[1];
                res[0] = (MemoryStream)completePacket.BaseStream;
                return res;
            }
            else
            {
                const int FRAGMENT_PACKETSIZE = 13; // [FragmentID 4] [FragmentCur 4] [FragmentCount 4] [Opcode 1]
                buf.SetLength(buf.Position);
                buf.Position = BBProtocol.TCP_HEADERSIZE;

                int nFragmentID = Interlocked.Increment(ref s_nLastUsedFragmentID);
                int nCount = (int)buf.Remaining() / (BBProtocol.MAX_TCPPACKETSIZE_WITHOUTHEADER - FRAGMENT_PACKETSIZE);
                if (buf.Remaining() % (BBProtocol.MAX_TCPPACKETSIZE_WITHOUTHEADER - FRAGMENT_PACKETSIZE) > 0)
                    nCount++;

                MemoryStream[] res = new MemoryStream[nCount];
                for (int i = 0; i < nCount; i++)
                {
                    int nPacketSize = (int)Math.Min(BBProtocol.MAX_TCPPACKETSIZE_WITHOUTHEADER, buf.Remaining() + FRAGMENT_PACKETSIZE);
                    res[i] = new MemoryStream(nPacketSize + BBProtocol.TCP_HEADERSIZE);
                    res[i].SetLength(res[i].Capacity);
                    using (BinaryWriter fragment = new BinaryWriter(res[i], Encoding.UTF8, true))
                    {
                        // TCP Header
                        fragment.Write(BBProtocol.PROTOCOL_HEADER);
                        fragment.Write((ushort)nPacketSize);
                        fragment.Write(BBProtocol.OP_FRAGMENT);
                        // Fragment Header
                        // [FragmentID 4] [FragmentCur 4] [FragmentLast 4] [Opcode 1]
                        fragment.Write(nFragmentID);
                        fragment.Write(i);
                        fragment.Write(nCount);
                        fragment.Write(byOpcode);
                    }
                    // Payload
                    buf.Read(res[i].GetBuffer(), (int)res[i].Position, (int)res[i].Remaining());
                    res[i].Position = 0;
                }
                return res;
            }
        }

        public BbTcpPacket ReadPacket(byte[] byBody, byte byProtcolVersion)
        {
            IsValid = false;
            if (!IsComplete && (byBody == null || byBody.Length != BodyLength))
            {
                ParseError = "Packet size doesn't match buffer size";
                return this;
            }
            IsComplete = true;

            m_byProtocolVersion = byProtcolVersion;
            if (Opcode != BBProtocol.OP_HELLO_ANSWER && (byProtcolVersion < BBProtocol.MIN_PROTOCOL_VERSION || byProtcolVersion > BBProtocol.CURRENT_PROTOCOL_VERSION))
            {
                ParseError = "Protocolversion unsupported (" + byProtcolVersion + ")";
                return this;
            }
            try
            {
                using (BinaryReader data = new BinaryReader(new MemoryStream(byBody, 0, byBody.Length, false, true), Encoding.UTF8, true))
                {
                    switch (Opcode)
                    {
                        case BBProtocol.OP_FRAGMENT:
                            {
                                BbTcpPacket_Fragment bbFragment = new BbTcpPacket_Fragment(this);
                                bbFragment.ProcessData(data);
                                return bbFragment;
                            }
                        case BBProtocol.OP_HELLO_ANSWER:
                            {
                                BbTcpPacket_Hello_Ans bbHelloAns = new BbTcpPacket_Hello_Ans(this);
                                bbHelloAns.ProcessData(data);
                                return bbHelloAns;
                            }
                        case BBProtocol.OP_ATTACH_ANSWER:
                            {
                                BbTcpPacket_Attach_Ans bbAttachAns = new BbTcpPacket_Attach_Ans(this);
                                bbAttachAns.ProcessData(data);
                                return bbAttachAns;
                            }
                        case BBProtocol.OP_INPUTFOCUSCHANGE:
                            {
                                BbTcpPacket_InputFocusChange bbFocusChange = new BbTcpPacket_InputFocusChange(this);
                                bbFocusChange.ProcessData(data);
                                return bbFocusChange;
                            }
                        case BBProtocol.OP_SENDICON:
                            {
                                BbTcpPacket_SendIcon bbSendIcon = new BbTcpPacket_SendIcon(this);
                                bbSendIcon.ProcessData(data);
                                return bbSendIcon;
                            }
                        case BBProtocol.OP_BATTERYSTATUS:
                            {
                                BbTcpPacket_BatteryStatus bbBattery = new BbTcpPacket_BatteryStatus(this);
                                bbBattery.ProcessData(data);
                                return bbBattery;
                            }
                        case BBProtocol.OP_STARTSENDFILE:
                            {
                                BbTcpPacket_StartSendFile bbFile = new BbTcpPacket_StartSendFile(this);
                                bbFile.ProcessData(data);
                                return bbFile;
                            }
                        case BBProtocol.OP_SENDFILECOMMAND:
                            {
                                BbTcpPacket_CommandSendFile bbFile = new BbTcpPacket_CommandSendFile(this);
                                bbFile.ProcessData(data);
                                return bbFile;
                            }
                        case BBProtocol.OP_SENDFILEDATA:
                            {
                                BbTcpPacket_SendFileData bbFile = new BbTcpPacket_SendFileData(this);
                                bbFile.ProcessData(data);
                                return bbFile;
                            }
                        case BBProtocol.OP_SHARETEXT:
                            {
                                BbTcpPacket_ShareText bbText = new BbTcpPacket_ShareText(this);
                                bbText.ProcessData(data);
                                return bbText;
                            }
                        case BBProtocol.OP_ENCRYPTIONACK:
                            {
                                BbTcpPacket_EncryptionACK bbCrypt = new BbTcpPacket_EncryptionACK(this);
                                bbCrypt.ProcessData(data);
                                return bbCrypt;
                            }
                        case BBProtocol.OP_ENCRYPTIONAUTHANS:
                            {
                                BbTcpPacket_Encryption_AuthAns bbAuth = new BbTcpPacket_Encryption_AuthAns(this);
                                bbAuth.ProcessData(data);
                                return bbAuth;
                            }
                        case BBProtocol.OP_TEXTSYNCUPDATE:
                            {
                                BbTcpPacket_TextSyncUpdate bbTextSync = new BbTcpPacket_TextSyncUpdate(this);
                                bbTextSync.ProcessData(data);
                                return bbTextSync;
                            }
                        case BBProtocol.OP_SHAREDCLIPBOARDCONTENT:
                            {
                                BbTcpPacket_ClipboardContent bbClipboard = new BbTcpPacket_ClipboardContent(this);
                                bbClipboard.ProcessData(data);
                                return bbClipboard;
                            }
                        case BBProtocol.OP_PING_ACK:
                            {
                                BbTcpPacket_Pink_ACK bbPingACK = new BbTcpPacket_Pink_ACK(this);
                                bbPingACK.ProcessData(data);
                                return bbPingACK;
                            }
                        default:
                            {
                                ParseError = "Opcode unsupported - " + Opcode;
                                return this;
                            }
                    }
                }
            }
            catch (IOException e)
            {
                ParseError = "Packet size unexpected short, discard - " + e.Message;
                return this;
            }

        }

        public virtual MemoryStream[] WritePacket()
        {
            return null;
        }

        public int DbgGetOpcode() { return Opcode; }

        public virtual bool IsExpectingAnswer() { return false; }
        public virtual byte GetExpectedAnswer() { return 0; }
        public bool IsExpectedAnswerTimedOut() { return m_answerTimer.IsCountdownReached; }
    }
}
