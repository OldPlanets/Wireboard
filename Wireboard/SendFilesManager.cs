using Wireboard.BbEventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard
{
    public class SendFilesManager
    {
        private static String TAG = typeof(SendFilesManager).Name;

        private List<SendFile> m_liSendFiles = new List<SendFile>();

        public void SendFiles(String[] aFiles, BbRemoteServer server)
        {
            int nActiveFiles = m_liSendFiles.Count(x => x.Status != SendFile.EStatus.WAITING);
            foreach (String file in aFiles)
            {
                if (!String.IsNullOrEmpty(file))
                {
                    SendFile newFile = SendFile.CreateFromString(file);
                    if (newFile != null)
                        m_liSendFiles.Add(newFile);
                }
            }
            if (nActiveFiles == 0)
                StartNextFile(server);
        }

        private void StartNextFile(BbRemoteServer server)
        {
            SendFile next = m_liSendFiles.Find(x => x.Status == SendFile.EStatus.WAITING);
            if (next != null)
            {
                next.Status = SendFile.EStatus.REQUESTED;
                server.SendFileStart(next.FileName, next.FileType, next.FileID, (ulong)next.FileSize);
                Log.s(TAG, $"Waiting for remote client to accept file: {next.FileName}");
            }
        }

        public void onConnectionEvent(object sender, ConnectionEventArgs eventArgs)
        {
            if (eventArgs.NewState == ConnectionEventArgs.EState.DISCONNECTED)
            {
                foreach (SendFile f in m_liSendFiles)
                {
                    Log.i(TAG, "Cancelling upload due to disconnect for file " + f.FileName, true);
                    f.Cancel();
                }
                m_liSendFiles.Clear();
                //m_statusTimer.IsEnabled = false;
            }
        }

        public async void onSendFileEvent(object sender, SendFileEventArgs eventArgs)
        {
            SendFile file = m_liSendFiles.Find(x => x.FileID == eventArgs.FileID);
            if (file != null)
            {
                if (eventArgs.FileEvent == SendFileEventArgs.EFileEvent.CANCELFILE)
                {
                    if (String.IsNullOrWhiteSpace(eventArgs.ErrorMessage))
                        Log.w(TAG, "Remote device requested to cancel upload of file " + file.FileName, true);
                    else
                        Log.w(TAG, "Remote device cancelled upload of file " + file.FileName + " because of an error: " + eventArgs.ErrorMessage, true);

                    file.Cancel();
                    m_liSendFiles.Remove(file);
                }
                else if (eventArgs.FileEvent == SendFileEventArgs.EFileEvent.ACCEPT && file.Status == SendFile.EStatus.REQUESTED)
                {
                    file.Status = SendFile.EStatus.ACCEPTED;
                    try
                    {
                        await file.UploadFile((BbRemoteServer)sender);
                    }
                    catch (OperationCanceledException) { } // thats fine, nothing else to do
                    catch (Exception e)
                    {
                        if (file.Status == SendFile.EStatus.TRANSFERRING)
                        {
                            Log.e(TAG, "Error while sending file " + file.FileName + " - " + e.Message, true);
                            ((BbRemoteServer)sender).SendFileCancel(file.FileID, true);
                            file.Cancel();
                        }
                    }
                    m_liSendFiles.Remove(file);
                    StartNextFile((BbRemoteServer)sender);
                    return;
                }
            }
            else
            {
                Log.w(TAG, "Received file command for unknown FileID " + eventArgs.FileID);
            }
        }

    }
}
