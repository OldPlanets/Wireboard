using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Wireboard.BbEventArgs
{
    public class ReceivedIconEventArgs : EventArgs
    {
        public ReceivedIconEventArgs(string packageName, BitmapImage image)
        {
            PackageName = packageName;
            Image = image;
        }

        public String PackageName { get; private set; }
        public BitmapImage Image { get; private set; }
    }
}
