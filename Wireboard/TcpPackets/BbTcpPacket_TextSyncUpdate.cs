using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_TextSyncUpdate : BbTcpPacket
    {
        public String Text { get; private set; }
        public int CursorPos { get; private set; }
        public int LastProcessedInputID { get; private set; }

        internal BbTcpPacket_TextSyncUpdate(BbTcpPacket src) : base(src)
        {
        }

        internal void ProcessData(BinaryReader data)
        {
            // [Text String][CursorPos 4][LastInputID 4]
            Text = BBProtocol.ReadString(data);
            CursorPos = data.ReadInt32();
            LastProcessedInputID = data.ReadInt32();
            if (CursorPos < 0 || CursorPos > Text.Length)
            {
                Log.e(TAG, "Invalid Cursor pos on InputFocusChange received" + CursorPos);
                CursorPos = Text.Length;
            }
            IsValid = true;
        }
    }
}
