using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_InputFocusChange : BbTcpPacket
    {
        public int ImeOptions { get; private set; }
        public int InputType { get; private set; }
        public String Hint { get; private set; }
        public String PackageName { get; private set; }
        public String Text { get; private set; }
        public int CursorPos { get; private set; }
        public int FieldID { get; private set; }

        internal BbTcpPacket_InputFocusChange(BbTcpPacket src) : base(src)
        {
        }

        internal void ProcessData(BinaryReader data)
        {
            // [ImeOptions 4][InputType 4][Hint String][PackageName String][Text String][CursorPos 4][FieldID 4]
            ImeOptions = data.ReadInt32();
            InputType = data.ReadInt32();
            Hint = BBProtocol.ReadString(data);
            PackageName = BBProtocol.ReadString(data);
            Text = BBProtocol.ReadString(data);
            CursorPos = data.ReadInt32();
            FieldID = data.ReadInt32();
            if (CursorPos < 0 || CursorPos > Text.Length)
            {
                Log.e(TAG, "Invalid Cursor pos on InputFocusChange received" + CursorPos);
                CursorPos = Text.Length;
            }
            IsValid = true;
        }
    }
}
