using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Wireboard.TcpPackets.BbTcpPacket_StartEncryption;

namespace Wireboard.TcpPackets
{
    public class BbTcpPacket_EncryptionACK : BbTcpPacket
    {
        public byte ErrorCode { get; private set; }
        public byte[] RemotePublicKey { get; private set; }
        public byte[] EncryptedMagicValue { get; private set; }
        public byte[] PasswordSalt { get; private set; }
        public EEncryptionMethod EncryptionMethod { get; set; }

        internal BbTcpPacket_EncryptionACK(BbTcpPacket src) : base(src)
        {
        }

        internal void ProcessData(BinaryReader data)
        {
            // [ErrorCode 1](ErrorCode == 0: [EncryptionMethod 1][EncryptionMagicValue 4] [RemotePublicKey 256]( EncryptionMethod == AUTH: [PasswordSalt 16]))
            ErrorCode = data.ReadByte();
            if (ErrorCode == 0)
            {
                EncryptionMethod = (EEncryptionMethod)data.ReadByte();
                EncryptedMagicValue = BBProtocol.ReadByteArray(data);
                if (EncryptedMagicValue.Length != 4)
                {
                    IsValid = false;
                    return;
                }
                RemotePublicKey = BBProtocol.ReadByteArray(data);
                if (RemotePublicKey.Length != 256)
                {
                    IsValid = false;
                    return;
                }
                if (EncryptionMethod == EEncryptionMethod.DH_PLUS_AUTH)
                {
                    PasswordSalt = BBProtocol.ReadByteArray(data);
                    if (PasswordSalt.Length != 16)
                    {
                        IsValid = false;
                        return;
                    }
                }
            }
            IsValid = true;
        }
    }
}