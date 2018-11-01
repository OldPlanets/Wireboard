using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_CommandSendFile : BbTcpPacket
    {
        public enum EFileCommand { ACCEPT = 1, CANCEL = 2}
        public int FileID { get; private set; }
        public EFileCommand Command { get; private set; }
        public bool IsSender { get; private set; }
        public String ErrorMessage { get; private set; }


        public BbTcpPacket_CommandSendFile(byte byProtolVersion, int nFileID, EFileCommand eCommand, bool bIsSender, String strErrorMessage) 
            : base(BBProtocol.OP_SENDFILECOMMAND, byProtolVersion)
        {
            FileID = nFileID;
            Command = eCommand;
            IsSender = bIsSender;
            ErrorMessage = strErrorMessage;
        }

        internal BbTcpPacket_CommandSendFile(BbTcpPacket src) : base(src)
        {
        }

        public override MemoryStream[] WritePacket()
        {
            // [FileID 4][Command 1][IsSender 1][ErrorMsg String]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(32), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
                    res.Write(FileID);
                    res.Write((byte)Command);
                    res.Write((byte)(IsSender ? 1 : 0));
                    BBProtocol.WriteString(ErrorMessage, res);
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

        internal void ProcessData(BinaryReader data)
        {
            // [FileID 4][Command 1][IsSender 1][ErrorMsg String]
            try
            {
                FileID = data.ReadInt32();
                Command = (EFileCommand)data.ReadByte();
                IsSender = data.ReadByte() != 0;
                ErrorMessage = BBProtocol.ReadString(data);
            }
            catch(Exception)
            {
                IsValid = false;
                return;
            }
            IsValid = true;
        }
    }
}
