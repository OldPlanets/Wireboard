using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_RemoveText : BbTcpPacket
    {
        private int m_nInputID;
        private uint m_nOffset;
        private uint m_nLength;

        public BbTcpPacket_RemoveText(byte byProtolVersion, int nInputID, uint nOffset, uint nLength) 
            : base(BBProtocol.OP_REMOVETEXT, byProtolVersion)
        {
            m_nInputID = nInputID;
            m_nOffset = nOffset;
            m_nLength = nLength;
        }

        public override MemoryStream[] WritePacket()
        {
            // [InputID 4] [Offset 4] [Length 4]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(64), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
                    res.Write(m_nInputID);
                    res.Write(m_nOffset);
                    res.Write(m_nLength);
                    return CreateFragments(res, Opcode);
                }
                catch (Exception e) when (e is IOException || e is IOException)
                {
                    ParseError = "Error while writing packet - " + e.Message;
                    Log.e(BbTcpPacket.TAG, ParseError);
                    return null;
                }
            }
        }
    }
}
