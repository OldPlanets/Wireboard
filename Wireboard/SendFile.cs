using Wireboard.TcpPackets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Wireboard
{
    public class SendFile
    {
        private static String TAG = typeof(SendFile).Name;
        private static int s_nFileIDCounter = 0;
        public enum EStatus { WAITING, REQUESTED, ACCEPTED, TRANSFERRING, FINISHED, CANCELLED }

        public String FullPath { get; private set; }
        public String FileName { get; private set; }
        public String FileType { get; private set; }
        public long FileSize { get; private set; }
        public int FileID { get; private set; }
        public EStatus Status { get; set; }

        private CancellationTokenSource m_CancelToken;
        private FileStream m_stream;
        private LowResStopWatch m_swUploadStartTime;

        private SendFile() { }

        public static SendFile CreateFromString(String strPath)
        {
            if (!File.Exists(strPath))
                return null;

            SendFile res = new SendFile()
            {
                FullPath = strPath,
                FileName = Path.GetFileName(strPath),
                FileType = MimeMapping.GetMimeMapping(Path.GetFileName(strPath)),
                FileID = --s_nFileIDCounter,
                Status = EStatus.WAITING
            };

            try
            {
                using (File.OpenRead(strPath)) { }
                res.FileSize = new FileInfo(strPath).Length;
            }
            catch(Exception e)
            {
                Log.w(TAG, "Unable to open file " + res.FileName + " - " + e.Message, true);
                return null;
            }
            if (res.FileSize <= 0)
            {
                Log.w(TAG, res.FileName + " is empty, skipping upload", true);
                return null;
            }
            return res;
        }

        public void Cancel()
        {
            if (Status == EStatus.TRANSFERRING)
            {
                try
                {
                    m_CancelToken.Cancel();
                }
                catch (Exception) { }
            }
            if (Status != EStatus.FINISHED)
                Status = EStatus.CANCELLED;
        }

        public async Task UploadFile(BbRemoteServer server)
        {
            m_CancelToken = new CancellationTokenSource();
            Status = EStatus.TRANSFERRING;
            m_swUploadStartTime = new LowResStopWatch(true);
            Log.i(TAG, "Sending " + FileName + " (" + FileSize.ToXByteSize() + ")", true);
            try
            {
                using (m_stream = File.OpenRead(FullPath))
                {
                    long lPosition = 0;
                    while (lPosition < FileSize && server.Attached)
                    {
                        m_CancelToken.Token.ThrowIfCancellationRequested();
                        BbTcpPacket_SendFileData packet = new BbTcpPacket_SendFileData(server.UsedProtocolVersion, FileID, (ulong)lPosition);
                        int nToRead = (int)Math.Min(packet.MaxDataSize, FileSize - lPosition);
                        int nRead = await packet.WriteFileData(m_stream, nToRead, m_CancelToken.Token);
                        if (nRead != nToRead)
                        {
                            throw new IOException("Unexpected read error (read less than expected)");
                        }
                        lPosition += nRead;
                        await server.SendLowPriorityDataPacketAsync(packet, m_CancelToken.Token);
                    }
                }
            }
            finally
            {
                m_CancelToken.Dispose();
                m_CancelToken = null;
            }
            Status = EStatus.FINISHED;
            m_swUploadStartTime.Stop();
            Log.i(TAG, "Finished sending file " + FileName, true);
        }
    }
}
