using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_SendKey : BbTcpPacket
    {
        private int m_nInputID;
        private int m_nKeyCode;
        private int m_nModifierCoder;
        private int m_nOffset;

        public BbTcpPacket_SendKey(byte byProtolVersion,int nInputID, int nKeyCode, int nModifierCode, int nOffset)
            : base(BBProtocol.OP_SENDSPECIALKEY, byProtolVersion)
        {
            m_nInputID = nInputID;
            m_nKeyCode = nKeyCode;
            m_nModifierCoder = nModifierCode;
            m_nOffset = nOffset;
        }

        public override MemoryStream[] WritePacket()
        {
            // [InputID 4] [Keycode 4] [ModifierCode 4] [Offset 4]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(32), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
                    res.Write(m_nInputID);
                    res.Write(m_nKeyCode);
                    res.Write(m_nModifierCoder);
                    res.Write(m_nOffset);
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
