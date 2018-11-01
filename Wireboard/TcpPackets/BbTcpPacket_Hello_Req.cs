using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_Hello_Req : BbTcpPacket
    {
        private byte MaxSupportedVersion { get; set; }
        private byte MinSupportedVersion { get; set; }
        private bool RequiresPassword { get; set; }
        private String Desc { get; set; } = "";
        private int OwnGUID { get; set; }
        private Version ClientVersion { get; set; }

        public BbTcpPacket_Hello_Req(byte byMaxSupportedVersion, byte byMinSupportedVersion, bool bRequiresPassword, String strDesc, int nOwnGUID, Version vClientVersion)
            : base(BBProtocol.OP_HELLO_REQUEST, 0)
        {
            MaxSupportedVersion = byMaxSupportedVersion;
            MinSupportedVersion = byMinSupportedVersion;
            RequiresPassword = bRequiresPassword;
            Desc = strDesc;
            OwnGUID = nOwnGUID;
            ClientVersion = vClientVersion;
        }

        public override MemoryStream[] WritePacket()
        {
            m_answerTimer.StartCountDown(4000);
            // [MaxVer 1][MinVer 1][ReqPassword 1][Desc String][OwnGUID 4][ClientVersionMaj 4][ClientVersionMinor 4][ClientVersionBuild 4]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(512), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
                    res.Write(MaxSupportedVersion);
                    res.Write(MinSupportedVersion);
                    res.Write((byte)(RequiresPassword ? 1 : 0));
                    BBProtocol.WriteString(Desc, res);
                    res.Write(OwnGUID);
                    res.Write(ClientVersion.Major);
                    res.Write(ClientVersion.Minor);
                    res.Write(ClientVersion.Build);
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
        public override byte GetExpectedAnswer() { return BBProtocol.OP_HELLO_ANSWER; }
    }
}
