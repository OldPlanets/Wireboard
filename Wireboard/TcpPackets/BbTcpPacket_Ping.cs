using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_Ping : BbTcpPacket
    {
        public int SessionID { get; private set; }
        public bool InitialDisplayLock { get; private set; }

        public BbTcpPacket_Ping(byte byProtolVersion) : base(BBProtocol.OP_PING, byProtolVersion)
        {
        }

        public override MemoryStream[] WritePacket()
        {
            m_answerTimer.StartCountDown(4000);
            // [0]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(512), Encoding.UTF8, true))
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

        public override bool IsExpectingAnswer() { return true; }
        public override byte GetExpectedAnswer() { return BBProtocol.OP_PING_ACK; }
    }
}