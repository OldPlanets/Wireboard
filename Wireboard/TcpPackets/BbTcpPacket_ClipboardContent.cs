using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_ClipboardContent : BbTcpPacket
    {
        public String ContentPlainText { get; private set; }
        public String ContentHtmlText { get; private set; }

        internal BbTcpPacket_ClipboardContent(BbTcpPacket src) : base(src)
        {
        }

        public BbTcpPacket_ClipboardContent(byte byProtolVersion, String strContentPlainText, String strContentHtmlText)
            : base(BBProtocol.OP_SHAREDCLIPBOARDCONTENT, byProtolVersion)
        {
            ContentPlainText = strContentPlainText;
            ContentHtmlText = strContentHtmlText;
        }

        internal void ProcessData(BinaryReader data)
        {
            // [ContentPlainText String][ContentHTMLText String]
            ContentPlainText = BBProtocol.ReadString(data);
            ContentHtmlText = BBProtocol.ReadString(data);
            IsValid = true;
        }

        public override MemoryStream[] WritePacket()
        {
            // [ContentPlainText String][ContentHTMLText String]
            using (BinaryWriter res = new BinaryWriter(new MemoryStream(64 + BBProtocol.GetMaxEncodedBytes(ContentPlainText) 
                + BBProtocol.GetMaxEncodedBytes(ContentHtmlText)), Encoding.UTF8, true))
            {
                try
                {
                    res.BaseStream.Position = 0 + BBProtocol.TCP_HEADERSIZE;
                    BBProtocol.WriteString(ContentPlainText, res);
                    BBProtocol.WriteString(ContentHtmlText, res);
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
