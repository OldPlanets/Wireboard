using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_DoImeAction : BbTcpPacket
    {
        public BbTcpPacket_DoImeAction(byte byProtolVersion) : base(BBProtocol.OP_DOIMEACTION, byProtolVersion)
        {
        }

        public override MemoryStream[] WritePacket()
        {
            // [Empty]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(32), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
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
