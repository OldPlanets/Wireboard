using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_StopCapture : BbTcpPacket
    {
        public int CaptureId { get; private set; }

        public BbTcpPacket_StopCapture(byte byProtolVersion) : base(BBProtocol.OP_STOPCAPTURE, byProtolVersion)
        {
            //Debug.Assert(byProtolVersion >= 2);
        }

        public override MemoryStream[] WritePacket()
        {
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(BBProtocol.TCP_HEADERSIZE + 0), Encoding.UTF8, true))
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
