using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_StartSendFile : BbTcpPacket
    {
        public String FileName { get; private set; }
        public String FileType { get; private set; }
        public int FileID { get; private set; }
        public ulong FileSize { get; private set; }

        internal BbTcpPacket_StartSendFile(BbTcpPacket src) : base(src)
        {
        }

        public BbTcpPacket_StartSendFile(byte byProtolVersion, String strFileName, String strFileType, int nFileID, ulong lFileSize) : base(BBProtocol.OP_STARTSENDFILE, byProtolVersion)
        {
            FileName = strFileName;
            FileType = strFileType;
            FileID = nFileID;
            FileSize = lFileSize;
        }

        public override MemoryStream[] WritePacket()
        {
            // [FileID 4][Filesize 8][Name String][Type String]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(512), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
                    res.Write(FileID);
                    res.Write(FileSize);
                    BBProtocol.WriteString(FileName, res);
                    BBProtocol.WriteString(FileType, res);
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
            // [FileID 4][Filesize 8][Name String][Type String]
            FileID = data.ReadInt32();
            FileSize = data.ReadUInt64();
            FileName = BBProtocol.ReadString(data);
            FileType = BBProtocol.ReadString(data);
            IsValid = true;
        }
    }
}
