using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_SendText : BbTcpPacket
    {
        private int m_nInputID;
        private string m_strText = "";
        private uint m_nOffset;
        private int m_nTextAfterCursor;

        public BbTcpPacket_SendText(byte byProtolVersion, int nInputID, string strText, uint nOffset, int nTextAfterCursor) 
            : base(BBProtocol.OP_SENDTEXT, byProtolVersion)
        {
            m_nInputID = nInputID;
            m_strText = strText;
            m_nOffset = nOffset;
            m_nTextAfterCursor = nTextAfterCursor;
        }

        public override MemoryStream[] WritePacket()
        {
            // [InputID 4] [Text String] [Offset 4] [TextAfterCursor 4]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(64 + BBProtocol.GetMaxEncodedBytes(m_strText)), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
                    res.Write(m_nInputID);
                    BBProtocol.WriteString(m_strText, res);
                    res.Write(m_nOffset);
                    res.Write(m_nTextAfterCursor);
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
