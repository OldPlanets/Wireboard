using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Web;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Wireboard.Native.NativeMethods;

namespace Wireboard
{
    public class ReceiveFile
    {
        protected static String TAG = typeof(ReceiveFile).Name;

        public int FileID { get; }
        public String FileName { get; }
        public String FileType { get; }
        public ulong FileSize { get; }

        public String TempFilePath { get; private set; }
        public String FinalFilePath { get; private set; }
        public String BasePath { get; private set; }

        public bool Complete { get; private set; }

        private FileStream m_fileStream;

        private CancellationTokenSource m_CancelToken;
        private ActionBlock<MemoryStream> m_writeAction;
        private bool m_bError = false;
        public ulong DataReceived { get; private set; } = 0;
        private LowResStopWatch m_swDownloadStartTime;

        public ImageSource Thumbnail { get; private set; }  

        public ReceiveFile(String strFileName, String strFileType, int nFileID, ulong lFileSize)
        {
            FileName = strFileName;
            FileType = strFileType;
            FileID = nFileID;
            FileSize = lFileSize;
        }

        public bool Open()
        {
            if (m_fileStream == null)
            {

                BasePath = Properties.Settings.Default.PrefDownloadDir;
                if (String.IsNullOrWhiteSpace(BasePath))
                    BasePath = ReceiveFilesManager.GetDefaultDownloadDirectory();

                TempFilePath = BasePath + Path.DirectorySeparatorChar + FileName + ".part";
                for (int i = 1; File.Exists(TempFilePath); i++)
                {
                    TempFilePath = BasePath + Path.DirectorySeparatorChar + FileName + i.ToString() + ".part";
                }

                try
                {
                    Directory.CreateDirectory(BasePath);
                    m_fileStream = new FileStream(TempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    Log.d(TAG, "Created new file for receiving: " + TempFilePath);
                    return true;
                }
                catch (Exception e)
                {
                    Log.w(TAG, "Error opening file (creating directory or file) " + TempFilePath + " - " + e.Message);
                    return false;
                }
            }
            else
                return false;
        }

        public void Cancel()
        {
            if (m_swDownloadStartTime != null)
                m_swDownloadStartTime.Stop();

            if (m_writeAction != null)
            {
                m_CancelToken.Cancel();
                m_writeAction.Complete();
                try
                {
                    m_writeAction.Completion.Wait();
                }
                catch (Exception e)
                {
                    Log.w(TAG, "Cancel excpetion for file " + FileName + " - " + e.Message);
                }
                m_writeAction = null;
                m_CancelToken.Dispose();
            }

            if (m_fileStream != null)
            {
                m_fileStream.Close();
                try
                {
                    File.Delete(TempFilePath);
                    Log.d(TAG, "Cancelled receiving file " + FileName);
                }
                catch (Exception e)
                {
                    Log.w(TAG, "Error while deleting file " + TempFilePath + " - " + e.Message);
                }
            }
        }

        public String GetTransferSpeedString()
        {
            if (m_swDownloadStartTime != null && m_swDownloadStartTime.ElapsedMilliseconds > 0 && DataReceived > 0)
            {
                return ((DataReceived / (ulong)m_swDownloadStartTime.ElapsedMilliseconds) * 1000).ToXByteSize() + "/s";
            }
            else
            {
                return "0B/s";
            }
        }

        public async Task Write(MemoryStream stream, ulong lDataStartPos)
        {
            // Calling WriteAsync on the same stream multiple times parallelly might (unknown) lead to unexpected write race conditions
            // To be safe we use ActionBlocks to write Non-Blocking but sequential
            if (m_writeAction == null)
            {
                m_swDownloadStartTime = new LowResStopWatch(true);
                m_CancelToken = new CancellationTokenSource();
                m_writeAction = new ActionBlock<MemoryStream>(ms =>
                {
                    try
                    {
                        m_fileStream.Write(ms.GetBuffer(), (int)ms.Position, (int)ms.Remaining());
                    }
                    catch (Exception e)
                    {
                        Log.e(TAG, "Error Writing file " + FileName + " - " + e.Message);
                        m_bError = true;
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = m_CancelToken.Token
                });
            }
            if (m_bError)
                throw new IOException("Write Error in Actionblock");

            if (lDataStartPos != DataReceived)
                throw new IOException("Wrong start position for data chunk");

            DataReceived += (ulong)stream.Remaining();

            if (DataReceived > FileSize)
                throw new IOException("More data received than expected file size");

            m_writeAction.Post(stream);

            if (DataReceived == FileSize)
            {
                m_writeAction.Complete();
                try
                {
                    await m_writeAction.Completion;
                }
                catch (Exception e)
                {
                    Log.e(TAG, "Error while waiting for Completition of file " + FileName + " - " + e.Message);
                    throw e;
                }
                m_writeAction = null;
                m_CancelToken.Dispose();

                if (m_fileStream.Position != (long)FileSize)
                    throw new Exception("Unexpected position in file stream while completing");
                m_fileStream.Close();
                m_fileStream = null;

                FinalFilePath = BasePath + Path.DirectorySeparatorChar + FileName;
                for (int i = 1; File.Exists(FinalFilePath); i++)
                {
                    String fileBase = Path.GetFileNameWithoutExtension(FileName);
                    String ext = Path.GetExtension(FileName);
                    FinalFilePath = BasePath + Path.DirectorySeparatorChar + fileBase + "_" + i.ToString() + ext;
                }
                File.Move(TempFilePath, FinalFilePath);
                TempFilePath = "";
                if (m_swDownloadStartTime != null)
                    m_swDownloadStartTime.Stop();

                await Task.Run(() => FetchThumbnail());
                Complete = true;
            }
        }

        private void FetchThumbnail()
        {
            if (MimeMapping.GetMimeMapping(FileName).StartsWith("image/"))
            {
                try
                {
                    BitmapImage Image = new BitmapImage();
                    Image.BeginInit();
                    Image.DecodeFailed += (x, e) => { throw e.ErrorException; };
                    Image.DecodePixelWidth = 80;
                    Image.UriSource = new Uri(FinalFilePath, UriKind.Absolute);
                    Image.CacheOption = BitmapCacheOption.OnLoad;
                    Image.EndInit();
                    Image.Freeze();
                    Thumbnail = Image;
                    return;
                }
                catch (Exception e)
                {
                    Log.w(TAG, "Failed to load/decode thumbnail image for file " + FileName + " - " + e.Message);
                }
            }

            SHFILEINFO info = new SHFILEINFO(true);
            int cbFileInfo = Marshal.SizeOf(info);
            SHGFI flags;
            flags = SHGFI.Icon | SHGFI.LargeIcon | SHGFI.UseFileAttributes;
            try
            {
                SHGetFileInfo(FinalFilePath, 256, out info, (uint)cbFileInfo, flags);
                IntPtr iconHandle = info.hIcon;

                Thumbnail = Imaging.CreateBitmapSourceFromHIcon(
                            iconHandle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                Thumbnail.Freeze();
                DestroyIcon(iconHandle);
            }
            catch (Exception e)
            {
                Log.w(TAG, "Failed to fetch icon for file " + FileName + " - " + e.Message);
            }
        }

    }
}
