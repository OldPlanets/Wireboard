using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.BbEventArgs
{
    public class ShareTextEventArgs : EventArgs
    {
        public String Text { get; }
        public String Type { get; }
        public String TextHtml { get; }

        public bool IsFromClipboard { get; }

        public ShareTextEventArgs(String strText, String strType, String strTextHtml, bool bIsFromClipboard)
        {
            Text = strText;
            Type = strType;
            TextHtml = strTextHtml;
            IsFromClipboard = bIsFromClipboard;
        }
    }
}
