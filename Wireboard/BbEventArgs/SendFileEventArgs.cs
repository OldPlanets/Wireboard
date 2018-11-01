using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.BbEventArgs
{
    public class SendFileEventArgs : EventArgs
    {
        public SendFileEventArgs(EFileEvent eFileEvent, int nFileID, String strErrorMessage)
        {
            FileEvent = eFileEvent;
            FileID = nFileID;
            ErrorMessage = strErrorMessage;
        }

        public enum EFileEvent { CANCELFILE, ACCEPT }

        public EFileEvent FileEvent { get; }
        public int FileID { get; }
        public String ErrorMessage { get; }
    }
}
