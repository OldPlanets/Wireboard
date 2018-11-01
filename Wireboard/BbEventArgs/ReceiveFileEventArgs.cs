using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.BbEventArgs
{
    public class ReceiveFileEventArgs : EventArgs
    {
        public enum EFileEvent { NEWFILE, CANCELFILE, DATA }

        public EFileEvent FileEvent { get; }
        public int FileID { get; }

        public String FileName { get; }
        public String FileType { get; }
        public ulong FileSize { get; }
        public bool CancelFile { get; set; }

        public ulong FilePos { get; }
        public MemoryStream Data { get; }

        
        public ReceiveFileEventArgs(String strFileName, String strFileType, int nFileID, ulong lFileSize)
        {
            FileName = strFileName;
            FileType = strFileType;
            FileID = nFileID;
            FileSize = lFileSize;
            CancelFile = false;
            FileEvent = EFileEvent.NEWFILE;
        }

        public ReceiveFileEventArgs(int nFileID, EFileEvent fileEvent)
        {
            if (fileEvent != EFileEvent.CANCELFILE)
                throw new ArgumentException();
            FileID = nFileID;
            FileEvent = EFileEvent.CANCELFILE;
        }

        public ReceiveFileEventArgs(int nFileID, ulong lFilePos, MemoryStream fileData)
        {
            FileID = nFileID;
            FilePos = lFilePos;
            Data = fileData;
            FileEvent = EFileEvent.DATA;
            CancelFile = false;
        }
    }
}
