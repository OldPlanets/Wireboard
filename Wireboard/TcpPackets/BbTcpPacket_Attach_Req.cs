using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_Attach_Req : BbTcpPacket
    {
        public int SessionID { get; private set; }
        public bool InitialDisplayLock { get; private set; }
        public bool ScreenLockBright { get; private set; }
        public bool ShareClipboard { get; private set; }

        public BbTcpPacket_Attach_Req(byte byProtolVersion, int nSessionID, bool bInitialDisplayLock, bool bScreenLockBright, bool bShareClipboard) 
            : base(BBProtocol.OP_ATTACH_REQUEST, byProtolVersion)
        {
            SessionID = nSessionID;
            InitialDisplayLock = bInitialDisplayLock;
            ScreenLockBright = bScreenLockBright;
            ShareClipboard = bShareClipboard;
        }

        public override MemoryStream[] WritePacket()
        {
            m_answerTimer.StartCountDown(4000);
            // [SessionID 4] [InitialDisplayLock 1][DisplayLockBright 1][ShareClipboard 1][Attach Options 0]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(512), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
                    res.Write(SessionID);
                    res.Write((byte)(InitialDisplayLock ? 1 : 0));
                    res.Write((byte)(ScreenLockBright ? 1 : 0));
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

        public override bool IsExpectingAnswer() { return true; }
        public override byte GetExpectedAnswer() { return BBProtocol.OP_ATTACH_ANSWER; }
    }
}
