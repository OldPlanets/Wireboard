using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_SendIcon : BbTcpPacket
    {
        public String PackageName { get; private set; }
        public BitmapImage Image { get; private set; }

        internal BbTcpPacket_SendIcon(BbTcpPacket src) : base(src)
        {
        }

        internal void ProcessData(BinaryReader data)
        {
            PackageName = BBProtocol.ReadString(data);
            MemoryStream bs = (MemoryStream)data.BaseStream;
            try
            {               
                Image = new BitmapImage();
                Image.BeginInit();
                Image.DecodeFailed += (x, e) => { throw e.ErrorException; };
                Image.StreamSource = new MemoryStream(bs.GetBuffer(), (int)bs.Position, (int)bs.Remaining());
                Image.EndInit();
                Image.Freeze();
                Log.d(TAG, "Loaded received image for package " + PackageName);
            }
            catch (Exception e)
            {
                // don't disconnect just because we fail to decode an image
                Image = null;
                Log.w(TAG, "Failed to load/decode received image for package " + PackageName + " - " + e.Message);
            }
            IsValid = true;

        }
    }
}
