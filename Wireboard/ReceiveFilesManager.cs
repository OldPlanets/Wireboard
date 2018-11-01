using Wireboard.BbEventArgs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Threading;

namespace Wireboard
{
    public class ReceiveFilesManager : INotifyPropertyChanged
    {
        private static String TAG = typeof(ReceiveFilesManager).Name;

        private List<ReceiveFile> m_liReceivingFiles = new List<ReceiveFile>();
        private DispatcherTimer m_statusTimer;

        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<ReceiveFile> FinishedFiles { get; } = new ObservableCollection<ReceiveFile>();
        private int m_nSelected = -1;
        public int Selected
        {
            get { return m_nSelected; }
            set { m_nSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Selected")); }
        }
        private bool m_bShowNotification = false;
        public bool ShowNotification
        {
            get { return m_bShowNotification; }
            set
            {
                m_bShowNotification = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ShowNotification"));
                if (!value)
                    DismissCompletedFiles();
            }
        }

        public ReceiveFilesManager()
        {
            m_statusTimer = new DispatcherTimer
            {
                Interval = new TimeSpan(0, 0, 1),
                IsEnabled = false
            };
            m_statusTimer.Tick += OnStatusTimer;
        }

        public async void OnReceivedFileEvent(object sender, ReceiveFileEventArgs eventArgs)
        {
            if (eventArgs.FileEvent == ReceiveFileEventArgs.EFileEvent.NEWFILE)
            {
                ReceiveFile newFile = new ReceiveFile(eventArgs.FileName, eventArgs.FileType, eventArgs.FileID, eventArgs.FileSize);
                if (newFile.Open())
                {
                    eventArgs.CancelFile = false;
                    m_liReceivingFiles.Add(newFile);
                    m_statusTimer.IsEnabled = true;
                    Log.i(TAG, "Receiving " + newFile.FileName + " (" + newFile.FileSize.ToXByteSize() + ")", true);
                }
                else
                {
                    eventArgs.CancelFile = true;
                }
            }
            else if (eventArgs.FileEvent == ReceiveFileEventArgs.EFileEvent.CANCELFILE)
            {
                ReceiveFile file = m_liReceivingFiles.Find(x => x.FileID == eventArgs.FileID);
                if (file != null)
                {
                    Log.i(TAG, "Aborted file " + file.FileName, true);
                    file.Cancel();
                    m_liReceivingFiles.Remove(file);
                }
                else
                {
                    Log.w(TAG, "Server requested cancelling of unknown file, Id:" + eventArgs.FileID);
                }
            }
            else if (eventArgs.FileEvent == ReceiveFileEventArgs.EFileEvent.DATA)
            {
                ReceiveFile file = m_liReceivingFiles.Find(x => x.FileID == eventArgs.FileID);
                if (file != null)
                {
                    try
                    {
                        await file.Write(eventArgs.Data, eventArgs.FilePos);
                    }
                    catch (Exception e)
                    {
                        Log.e(TAG, "Error while writing received file data for file " + file.FileName + " - " + e.Message);
                        eventArgs.CancelFile = true;
                        file.Cancel();
                        m_liReceivingFiles.Remove(file);
                        return;
                    }
                    if (file.Complete)
                    {
                        Log.i(TAG, "Completed file " + file.FileName, true);
                        FinishedFiles.Add(file);
                        m_liReceivingFiles.Remove(file);
                        Selected = FinishedFiles.Count - 1;
                        if (FinishedFiles.Count == 1)
                            ShowNotification = true;
                    }
                }
            }

        }

        public void OnConnectionEvent(object sender, ConnectionEventArgs eventArgs)
        {
            if (eventArgs.NewState == ConnectionEventArgs.EState.DISCONNECTED)
            {
                foreach (ReceiveFile f in m_liReceivingFiles)
                {
                    f.Cancel();
                }
                m_liReceivingFiles.Clear();
                m_statusTimer.IsEnabled = false;
            }
        }

        private void DismissCompletedFiles()
        {
            if (FinishedFiles.Count > 0)
            {
                m_nSelected = -1;
                FinishedFiles.Clear();
            }
        }

        public void OpenFinished(bool bFolderOnly)
        {
            if (Selected >= 0 && FinishedFiles.Count > Selected)
            {
                ReceiveFile file = FinishedFiles[Selected];
                Process process = new Process();
                process.StartInfo.FileName = bFolderOnly ? file.BasePath : file.FinalFilePath;
                process.StartInfo.UseShellExecute = true;
                try
                {
                    process.Start();
                }
                catch (Exception e)
                {
                    Log.w(TAG, "Failed to open file " + file.FileName + " - " + e.Message);
                }
            }
        }

        private void OnStatusTimer(Object source, EventArgs e)
        {
            if (m_liReceivingFiles.Count == 1)
            {
                ReceiveFile file = m_liReceivingFiles[0];
                int nComplete = (int)((file.DataReceived * 100) / file.FileSize);
                Log.s(TAG, "Receiving file " + file.FileName + " - " + nComplete + "% (" + file.GetTransferSpeedString() + ")");
            }
            else if (m_liReceivingFiles.Count > 1)
            {
                ulong lDataReceived = 0;
                ulong lFilesizes = 0;
                foreach (ReceiveFile file in m_liReceivingFiles)
                {
                    lDataReceived += file.DataReceived;
                    lFilesizes += file.FileSize;
                }
                int nComplete = (int)((lDataReceived * 100) / lFilesizes);
                Log.s(TAG, "Receiving file " + m_liReceivingFiles.Count + " files - " + nComplete + "%");
            }
            else
            {
                m_statusTimer.IsEnabled = false;
            }
        }

        public static String GetDefaultDownloadDirectory(bool bCreate = true)
        {
            String strPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + Path.DirectorySeparatorChar + (string)Application.Current.FindResource("AppNameCode");
            if (bCreate)
            {
                try
                {
                    Directory.CreateDirectory(strPath);
                }
                catch (Exception e)
                {
                    Log.w(TAG, "Failed to create directory " + strPath + " - " + e.Message);
                }
            }
            return strPath;
        }
    }
}
