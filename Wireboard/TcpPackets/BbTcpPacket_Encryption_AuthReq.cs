using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_Encryption_AuthReq : BbTcpPacket
    {
        private byte[] ChallengeResult { get; set; }

        public BbTcpPacket_Encryption_AuthReq(byte byProtolVersion, byte[] abyChallengeResult)
            : base(BBProtocol.OP_ENCRYPTIONAUTHREQ, byProtolVersion)
        {
            ChallengeResult = abyChallengeResult;
        }

        public override MemoryStream[] WritePacket()
        {
            m_answerTimer.StartCountDown(4000);
            // [ChallengeResult 32]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(64), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
                    BBProtocol.WriteByteArray(ChallengeResult, res);
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
        public override byte GetExpectedAnswer() { return BBProtocol.OP_ENCRYPTIONAUTHANS; }
    }
}