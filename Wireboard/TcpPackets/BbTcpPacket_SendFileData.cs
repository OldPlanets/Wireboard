using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_SendFileData : BbTcpPacket
    {
        public int FileID { get; private set; }
        public ulong StartPosition { get; private set; }
        public MemoryStream FileData { get; private set; }
        private const int PacketHeaderSize = 12;
        // since we do split file data into chunks anyway, it makes sense to make them small enough to not
        // create fragment packets to save (mainly) memory and cpu overhead of creating those
        public int MaxDataSize => (MaxFragmentSizeWithoutHeader - (BBProtocol.TCP_HEADERSIZE + PacketHeaderSize));


        internal BbTcpPacket_SendFileData(BbTcpPacket src) : base(src)
        {
        }

        public BbTcpPacket_SendFileData(byte byProtolVersion, int nFileID, ulong lStartPosition) : base(BBProtocol.OP_SENDFILEDATA, byProtolVersion)
        {
            FileID = nFileID;
            StartPosition = lStartPosition;
        }

        internal void ProcessData(BinaryReader data)
        {
            // [FileID 4][StartPos 8][Data]
            FileID = data.ReadInt32();
            StartPosition = data.ReadUInt64();
            FileData = (MemoryStream)data.BaseStream;
            IsValid = true;
        }

        public async Task<int> WriteFileData(FileStream stream, int nWrite, CancellationToken cancel)
        {
            int nSize = BBProtocol.TCP_HEADERSIZE + PacketHeaderSize + nWrite;
            FileData = new MemoryStream(new byte[nSize], 0, nSize, true, true);
            using (BinaryWriter res = new BinaryWriter(FileData, Encoding.UTF8, true))
            {
                res.BaseStream.Position = BBProtocol.TCP_HEADERSIZE;
                res.Write(FileID);
                res.Write(StartPosition);
            }
            int nRead = await stream.ReadAsync(FileData.GetBuffer(), (int)FileData.Position, nWrite, cancel);
            if (nRead > 0)
                FileData.Position = FileData.Position + nRead;
            return nRead;
        }

        public override MemoryStream[] WritePacket()
        {
            if (FileData == null || FileData.Position <= BBProtocol.TCP_HEADERSIZE + PacketHeaderSize)
            {
                ParseError = "No file data in buffer";
                Log.e(BbTcpPacket.TAG, ParseError);
                return null;
            }
            using (BinaryWriter res = new BinaryWriter(FileData, Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = ((MemoryStream)res.BaseStream).Capacity;
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
