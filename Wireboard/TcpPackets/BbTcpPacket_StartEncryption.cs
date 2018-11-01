using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// DHAGREEMENT: Straight forward DH Key Agreement and initalizing the Ciphers with the result entropy
// DH_PLUS_AUTH: Do a DH Agreement as before. Hash the result with our secret to get the cipher keys. 
//               Verify and Auth with the remote side we have the right password (so we can have multiple tries) by hashing the cipher key. If successful initalize the ciphers.
//               Both public keys are different with each connection, which means so is the agreement, which also is a proper challenge.
//               Offers additional a security against eavesdroppers (no brute-force against original plain text password possible due to DH)
//                  , no listening even if the password has been compromised (without an active man-in-the-middle attack)

namespace Wireboard.TcpPackets
{
    public class BbTcpPacket_StartEncryption : BbTcpPacket
    {
        public enum EEncryptionMethod { NONE = 0, DHAGREEMENT = 1, DH_PLUS_AUTH = 2 }

        private EEncryptionMethod Method { get; set; }
        private byte[] LocalPublicKey { get; set; }
        private byte[] LocalIV { get; set; }
        private byte[] RemoteIV { get; set; }

        public BbTcpPacket_StartEncryption(byte byProtolVersion, EEncryptionMethod method, byte[] abyLocalPublicKey, byte[] abyLocalIV, byte[] abyRemoteIV)
            : base(BBProtocol.OP_STARTENCRYPTION, byProtolVersion)
        {
            if (abyLocalPublicKey.Length != 256 || abyLocalIV.Length != 16 || abyRemoteIV.Length != 16)
                throw new ArgumentException("Invalid Encryption data for remote client");
            Method = method;
            LocalPublicKey = abyLocalPublicKey;
            LocalIV = abyLocalIV;
            RemoteIV = abyRemoteIV;
        }

        public override MemoryStream[] WritePacket()
        {
            m_answerTimer.StartCountDown(4000);
            // [KeySource 1] [LocalIV 16][RemoteIV 16] [LocalPublicKey 256]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(512), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
                    res.Write((byte)Method);
                    BBProtocol.WriteByteArray(LocalIV, res);
                    BBProtocol.WriteByteArray(RemoteIV, res);
                    BBProtocol.WriteByteArray(LocalPublicKey, res);

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
        public override byte GetExpectedAnswer() { return BBProtocol.OP_ENCRYPTIONACK; }
    }
}