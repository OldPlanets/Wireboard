using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.BbEventArgs
{
    public class ClipboardChangedEventArgs
    {
        public String TextPlain { get; }
        public String TextHtml { get; }
        public bool ModeChange { get; }

        public ClipboardChangedEventArgs(String strNewTextPlain, String strNewTextHtml)
        {
            TextPlain = strNewTextPlain;
            TextHtml = strNewTextHtml;
            ModeChange = false;
        }

        public ClipboardChangedEventArgs(bool bModeChange)
        {
            TextPlain = null;
            TextHtml = null;
            ModeChange = bModeChange;
        }

    }
}
