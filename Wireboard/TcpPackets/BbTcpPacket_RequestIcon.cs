using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_RequestIcon : BbTcpPacket
    {
        private String m_strPackageName;

        public BbTcpPacket_RequestIcon(byte byProtolVersion, String strPackageName) : base(BBProtocol.OP_REQUESTICON, byProtolVersion)
        {
            m_strPackageName = strPackageName;
        }

        public override MemoryStream[] WritePacket()
        {
            // [PackageName String]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(512), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
                    BBProtocol.WriteString(m_strPackageName, res);
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