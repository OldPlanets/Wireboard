using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_SetDisplayLock : BbTcpPacket
    {
        private bool m_bSetLock;
        private bool m_bScreenLockBright;

        public BbTcpPacket_SetDisplayLock(byte byProtolVersion, bool bSet, bool bScreenLockBright) 
            : base(BBProtocol.OP_SETDISPLAYLOCK, byProtolVersion)
        {
            m_bSetLock = bSet;
            m_bScreenLockBright = bScreenLockBright;
        }

        public override MemoryStream[] WritePacket()
        {
            // [SetLock 1][DisplayLockBright 1]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(512), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
                    res.Write((byte)(m_bSetLock ? 1 : 0));
                    res.Write((byte)(m_bScreenLockBright ? 1 : 0));
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
