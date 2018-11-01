using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_OptionsChange : BbTcpPacket
    {
        public bool ShareClipboard { get; private set; }

        public BbTcpPacket_OptionsChange(byte byProtolVersion, bool bShareClipboard)
            : base(BBProtocol.OP_OPTIONSCHANGE, byProtolVersion)
        {
            ShareClipboard = bShareClipboard;
        }

        public override MemoryStream[] WritePacket()
        {
            // [NewShareClipboard 1]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(64), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
                    res.Write((byte)(ShareClipboard ? 1 : 0));
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
