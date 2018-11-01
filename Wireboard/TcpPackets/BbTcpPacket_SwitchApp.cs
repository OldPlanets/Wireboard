using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_SwitchApp : BbTcpPacket
    {
        private String m_strPackageName;
        private int m_nFieldID;

        public BbTcpPacket_SwitchApp(byte byProtolVersion, String strPackageName, int nFieldID) : base(BBProtocol.OP_SWITCHAPP, byProtolVersion)
        {
            m_strPackageName = strPackageName;
            m_nFieldID = nFieldID;
        }

        public override MemoryStream[] WritePacket()
        {
            // [PackageName String] [FieldID 4]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(512), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
                    BBProtocol.WriteString(m_strPackageName, res);
                    res.Write(m_nFieldID);
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
