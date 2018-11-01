using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.DiscoveryPackets
{
    public class DiscoveryPacket
    {
        public bool IsValid { get; protected set; } = true;
        public String ParseError { get; protected set; } = "";

        private byte m_byOpcode = 0x00;

        protected DiscoveryPacket()
        {

        }

        protected DiscoveryPacket(DiscoveryPacket src)
        {
            IsValid = src.IsValid;
            m_byOpcode = src.m_byOpcode;
            ParseError = src.ParseError;
        }

        public static DiscoveryPacket ReadPacket(byte[] data)
        {
            DiscoveryPacket res = new DiscoveryPacket();
            res.IsValid = false;
            try
            {
                using (BinaryReader buf = new BinaryReader(new MemoryStream(data)))
                {
                    ushort shortHeaderValue = buf.ReadUInt16();
                    if (shortHeaderValue != BBProtocol.PROTOCOL_HEADER)
                    {
                        res.ParseError = "Protocol header mismatch, not a packet we are looking for";
                        return res;
                    }
                    ushort nPacketSize = buf.ReadUInt16();
                    byte byOpcode = buf.ReadByte();

                    if (nPacketSize > buf.BaseStream.Length - buf.BaseStream.Position)
                    {
                        res.ParseError = "Packet size smaller than expected, discard";
                        return res;
                    }
                    else if (nPacketSize > BBProtocol.MAX_DISCPACKETSIZE_WITHOUTHEADER)
                    {
                        res.ParseError = "Packet size above max, discard";
                        return res;
                    }

                    switch (byOpcode)
                    {
                        case BBProtocol.OP_DISCOVERY_ANSWER:
                            {
                                res.m_byOpcode = byOpcode;
                                DiscoveryPacket_Answer dpAnswer = new DiscoveryPacket_Answer(res);
                                dpAnswer.ProcessData(buf);
                                return dpAnswer;
                            }
                        default:
                            {
                                res.ParseError = "Opcode unsupported";
                                return res;
                            }
                    }
                }
            }
            catch (IOException e)
            {
                res.ParseError = "Exception while reading packet: " + e.Message;
                return res;
            }
        }

    protected static void WriteHeader(BinaryWriter buf, byte byOpcode)
    {
        long pos = buf.BaseStream.Position;
        buf.Seek(0, SeekOrigin.Begin);
        buf.Write((ushort)BBProtocol.PROTOCOL_HEADER);
        buf.Write((ushort)(pos - BBProtocol.DISCOVERY_HEADERSIZE));
        buf.Write((byte)byOpcode);
        buf.Seek((int)pos, SeekOrigin.Begin);
    }

    public virtual bool IsRequest() { return false; }
    public virtual bool IsAnswer() { return false; }
}
}
