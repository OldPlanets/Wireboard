using Wireboard.BbEventArgs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Wireboard
{
    public class AdbWrapper : INotifyPropertyChanged
    {
        protected static String TAG = typeof(AdbWrapper).Name;

        public BitmapImage CurrentScreenCap { get; private set; }
        private CancellationTokenSource m_screenCapCancelToken;
        private bool m_bIsScreenCapPaused = false;
        public bool IsScreenCapPaused
        {
            get { return m_bIsScreenCapPaused && Properties.Settings.Default.ScreenCapPauseOnInactive; }
            set
            {
                m_bIsScreenCapPaused = value;
            }
        }
        private Task m_taskScreenCap;
        private bool m_bIsScreenCapOutOfDate;
        public bool IsScreenCapOutOfDate
        {
            get { return m_bIsScreenCapOutOfDate; }
            private set
            {
                if (IsScreenCapOutOfDate != value)
                {
                    m_bIsScreenCapOutOfDate = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsScreenCapOutOfDate"));
                }
            }
        }
        private bool m_bIsScreenCapActive = false;
        public bool IsScreenCapActive
        {
            get { return m_bIsScreenCapActive; }
            private set
            {
                m_bIsScreenCapActive = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsScreenCapActive"));
            }
        }
        private LowResStopWatch m_swLastScreenCapUpdate = new LowResStopWatch(true);
        private bool m_bScreenCapFallbackMethod;
        public event PropertyChangedEventHandler PropertyChanged;

        public async Task<bool> StartScreenCappingAsync()
        {
            if (m_taskScreenCap != null && m_taskScreenCap.Status == TaskStatus.Running && !m_screenCapCancelToken.IsCancellationRequested)
            {
                return true;
            }
            if (m_screenCapCancelToken != null)
                m_screenCapCancelToken.Dispose();
            m_screenCapCancelToken = new CancellationTokenSource();
            if (!await GetIsADBReadyAsync(GetAdbLocation(), m_screenCapCancelToken.Token))
            {
                Log.w(TAG, "Unable to capture screens from ADB. See the Debug Log for details", true);
                return false;
            }
            // get one screen to test if it works out and so that the layout can be adjusted once this function returns
            BitmapImage first = await Task.Run(async () => await GetADBScreenCapAsync(GetAdbLocation(), m_screenCapCancelToken.Token, false));
            if (first == null)
            {
                first = await Task.Run(async () => await GetADBScreenCapAsync(GetAdbLocation(), m_screenCapCancelToken.Token, true));
                if (first == null)
                {
                    Log.w(TAG, "Unable to capture screens from ADB. See the Debug Log for details", true);
                    return false;
                }
                m_bScreenCapFallbackMethod = true;
                Log.w(TAG, "Preferred ADB screen capture method not supported (either local or remote ADB too old), using fallback", true);
            }
            else
                m_bScreenCapFallbackMethod = false;
            CurrentScreenCap = first;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentScreenCap"));
            m_swLastScreenCapUpdate.Start();
            IsScreenCapOutOfDate = false;
            IsScreenCapActive = true;

            m_taskScreenCap = Task.Run(async () =>
            {
                while (!m_screenCapCancelToken.IsCancellationRequested)
                {
                    BitmapImage newCap = await GetADBScreenCapAsync(GetAdbLocation(), m_screenCapCancelToken.Token, m_bScreenCapFallbackMethod);
                    if (newCap != null)
                    {
                        CurrentScreenCap = newCap;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentScreenCap"));
                        m_swLastScreenCapUpdate.Start();
                        IsScreenCapOutOfDate = false;
                        await Task.Delay(Properties.Settings.Default.ScreenCapRefresh, m_screenCapCancelToken.Token);
                    }
                    else
                    {
                        m_screenCapCancelToken.Token.ThrowIfCancellationRequested();
                        IsScreenCapOutOfDate = true;
                        while(!await GetIsADBReadyAsync(GetAdbLocation(), m_screenCapCancelToken.Token) && !m_screenCapCancelToken.IsCancellationRequested)
                        {
                            await Task.Delay(5000, m_screenCapCancelToken.Token);
                        }
                    }
                        
                    while (IsScreenCapPaused && !m_screenCapCancelToken.IsCancellationRequested)
                    {
                        if (m_swLastScreenCapUpdate.ElapsedMilliseconds > Properties.Settings.Default.ScreenCapRefresh * 2)
                            IsScreenCapOutOfDate = true;
                        await Task.Delay(250, m_screenCapCancelToken.Token);
                    }                        
                }
            });
            return true;
        }

        public async Task StopScreenCappingAsync()
        {
            if (m_screenCapCancelToken != null && !m_screenCapCancelToken.IsCancellationRequested)
            {
                try
                {
                    m_screenCapCancelToken.Cancel();
                }
                catch (Exception e)
                {
                    if (!(e is OperationCanceledException))
                        Log.w(TAG, "Cancel Exception: " + e.Message);
                }
                if (m_taskScreenCap != null)
                {
                    try
                    {
                        await m_taskScreenCap;
                    }
                    catch (OperationCanceledException) { };
                }
                m_taskScreenCap = null;
                CurrentScreenCap = null;
                Log.d(TAG, "Screencapping stopped");
            }
            IsScreenCapActive = false;
        }

        public static String GetAdbLocation(bool bDirectoryOnly = false)
        {
            string strLocation = Properties.Settings.Default.ADBPath;
            if (!String.IsNullOrWhiteSpace(strLocation))
            {
                if (File.Exists(strLocation + Path.DirectorySeparatorChar + "adb.exe"))
                    return bDirectoryOnly ? strLocation : (strLocation + Path.DirectorySeparatorChar + "adb.exe");
                else
                    return null;
            }
            // Install default
            strLocation = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + "Android" 
                + Path.DirectorySeparatorChar + "sdk" +Path.DirectorySeparatorChar + "platform-tools";
            if (File.Exists(strLocation + Path.DirectorySeparatorChar + "adb.exe"))
                return bDirectoryOnly ? strLocation : (strLocation + Path.DirectorySeparatorChar + "adb.exe");

            // programm directory
            strLocation = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + Path.DirectorySeparatorChar + "Android"
                + Path.DirectorySeparatorChar + "sdk" + Path.DirectorySeparatorChar + "platform-tools";
            if (File.Exists(strLocation + Path.DirectorySeparatorChar + "adb.exe"))
                return bDirectoryOnly ? strLocation : (strLocation + Path.DirectorySeparatorChar + "adb.exe");

            // root
            strLocation = "C:" + Path.DirectorySeparatorChar + "Android"
                + Path.DirectorySeparatorChar + "sdk" + Path.DirectorySeparatorChar + "platform-tools";
            if (File.Exists(strLocation + Path.DirectorySeparatorChar + "adb.exe"))
                return bDirectoryOnly ? strLocation : (strLocation + Path.DirectorySeparatorChar + "adb.exe");

            if (File.Exists(strLocation))
                return strLocation;

            return null;
        }

        public static async Task<bool> GetIsADBAvailableAsync(String strPath = null, CancellationToken token = default(CancellationToken))
        {
            return (await GetAdbVersionAsync(strPath, token)) != null;
        }

        public static async Task<Version> GetAdbVersionAsync(String strPath = null, CancellationToken token = default(CancellationToken))
        {
            String strAdbPath = strPath ?? GetAdbLocation();
            if (strAdbPath == null)
                return null;
            String output = await GetADBTextOutputAsync(strAdbPath, "version", token);
            if (output == null)
                return null;

            int nStart = output.IndexOf("version") + "version".Length;
            if (nStart < 1)
            {
                Log.w(TAG, "ADB version marker not found");
                return null;
            }
            int nEnd = output.IndexOf('\n', nStart);
            if (nEnd < 1)
            {
                Log.w(TAG, "ADB version end marker not found");
                return null;
            }
            String strVersion = output.Substring(nStart, nEnd - nStart).Trim();
            if (Version.TryParse(strVersion, out Version version))
            {
                //Log.d(TAG, "ADB found, Version: " + version);
                return version;
            }
            else
            {
                Log.w(TAG, "ADB version number format not recognized");
                return null;
            }
        }

        public static async Task<bool> GetIsADBReadyAsync(String strPath = null, CancellationToken token = default(CancellationToken))
        {
            return (await GetADBConnectedDevicesAsync(strPath, token)) == 1;
        }

        public static async Task<int> GetADBConnectedDevicesAsync(String strPath = null, CancellationToken token = default(CancellationToken))
        {
            String strAdbPath = strPath ?? GetAdbLocation();
            if (!await GetIsADBAvailableAsync(strAdbPath))
            {
                return -1;
            }
            String output = await GetADBTextOutputAsync(strAdbPath, "devices -l", token);
            if (output == null)
                return -1;

            int count = output.Split(new String[] {"device product" }, StringSplitOptions.None).Length - 1;
            if (count == 1)
            {
                Log.d(TAG, "ADB: Ready and one device connected");
                return 1;
            }
            else if (count < 1)
                Log.w(TAG, "ADB: No devices connected");
            else if (count > 1)
                Log.w(TAG, "ADB: More than one device connected - please connect one only");
            return count;
        }

        private static async Task<String> GetADBTextOutputAsync(String strPath, String strArgument, CancellationToken token)
        {
            Process process = new Process();
            process.StartInfo.FileName = strPath;
            process.StartInfo.Arguments = strArgument;
            process.StartInfo.ErrorDialog = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;

            String strOutput = null;
            try
            {
                process.Start();
                Task<String> outputTask = process.StandardOutput.ReadToEndAsync();
                if (!await process.WaitForExitAsync(1000 * 10,token))
                {
                    Log.w(TAG, "ADB process timeout or cancelled");
                    return null;
                }
                strOutput = await outputTask;
            }
            catch (Exception e)
            {
                Log.e(TAG, "Error while executing ADB processs: " + e.Message);
                return null;
            }
            
            return strOutput;
        }

        public static async Task<BitmapImage> GetADBScreenCapAsync(String strPath, CancellationToken token, bool bFallBackMethod)
        {
            String strAdbPath = strPath ?? GetAdbLocation();
            Process process = new Process();
            process.StartInfo.FileName = strAdbPath;
            process.StartInfo.Arguments = bFallBackMethod ? "shell screencap -p" : "exec-out screencap -p";
            process.StartInfo.ErrorDialog = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;

            try
            {
                process.Start();
            }
            catch (Exception e)
            {
                Log.e(TAG, "Error while executing ADB processs: " + e.Message);
                return null;
            }

            MemoryStream streamBuffer = new MemoryStream(100 * 1024);
            Task copyStreamTask = bFallBackMethod ? CopyAndRepairStreamAsync(process.StandardOutput.BaseStream, streamBuffer, token)
                : process.StandardOutput.BaseStream.CopyToAsync(streamBuffer, 81920, token);
            Task<bool> waitForExitTask = process.WaitForExitAsync(1000 * 15, token);

            Task finished = await Task.WhenAny(copyStreamTask, waitForExitTask);
            try
            {
                if (finished == waitForExitTask)
                {
                    if (!await waitForExitTask)
                    {
                        Log.w(TAG, "ADB process timeout or cancelled");
                        return null;
                    }
                }
                await copyStreamTask;
            }
            catch (Exception e)
            {
                if (e is OperationCanceledException)
                    Log.d(TAG, "ADB: Operation cancelled");
                else
                    Log.e(TAG, "ADB: Execute process exception: " + e.Message);
                return null;
            }

            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.DecodeFailed += (x, e) => { throw e.ErrorException; };
                image.StreamSource = streamBuffer;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception e)
            {
                Log.w(TAG, "Failed to recive/decode screencap image from ADB - " + e.Message);
                return null;
            }
        }

        private static async Task CopyAndRepairStreamAsync(Stream sourceStream, MemoryStream destStream, CancellationToken token)
        {
            // if we have to use adb shell to transfer the screen it will do an unwanted end of line character conversion from \n to \r\n
            // or \r\r\n (why two?!) corrupting the binary data in the process. Can be fixed, but it takes longer and is kinda messy compared
            // to the exec-out supported in newer versions. 
            byte[] buffer = new byte[81920];
            int nRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, token);
            if (nRead > 0)
            {
                if (buffer[5] == 0x0D && buffer[6] == 0x0D) // one or two \r added? Test the known header for it
                    await CopyAndRepairDoubleCRStreamAsync(nRead, buffer, sourceStream, destStream, token);
                else
                    await CopyAndRepairSingleCRStreamAsync(nRead, buffer, sourceStream, destStream, token);
            }
        }

        private static async Task CopyAndRepairSingleCRStreamAsync(int nRead, byte[] buffer, Stream sourceStream, MemoryStream destStream, CancellationToken token)
        {
            bool bCarryOverCR = false;
            do
            {
                if (bCarryOverCR)
                {
                    bCarryOverCR = false;
                    if (buffer[0] != 0x0A)
                        destStream.WriteByte(0x0D);
                }

                int nNextBlockStart = 0;
                for (int i = 0; i < nRead; i++)
                {
                    if (buffer[i] == 0x0D)
                    {
                        if (i + 1 >= nRead)
                        {
                            bCarryOverCR = true;
                            destStream.Write(buffer, nNextBlockStart, i - nNextBlockStart);
                            nNextBlockStart = i + 1;
                        }
                        else if (buffer[i + 1] == 0x0A)
                        {
                            destStream.Write(buffer, nNextBlockStart, i - nNextBlockStart);
                            nNextBlockStart = i + 1;
                        }
                    }
                }
                if (nRead - nNextBlockStart > 0)
                {
                    destStream.Write(buffer, nNextBlockStart, nRead - nNextBlockStart);
                }
            }
            while ((nRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0);

            if (bCarryOverCR)
                destStream.WriteByte(0x0D);
        }

        private static async Task CopyAndRepairDoubleCRStreamAsync(int nRead, byte[] buffer, Stream sourceStream, MemoryStream destStream, CancellationToken token)
        {
            bool bCarryOverCR = false;
            bool bCarryOverDoubleCR = false;
            do
            {
                int i = 0;
                if (bCarryOverCR)
                {
                    bCarryOverCR = false;
                    if (buffer[0] == 0x0D)
                    {
                        bCarryOverDoubleCR = true;
                        i++;
                    }
                    else
                        destStream.WriteByte(0x0D);
                }

                while (bCarryOverDoubleCR && i < nRead)
                {
                    if (buffer[i] == 0x0D)
                    {
                        destStream.WriteByte(0x0D);
                        i++;
                    }
                    else
                    {
                        bCarryOverDoubleCR = false;
                        if (buffer[i] != 0x0A)
                        {
                            destStream.WriteByte(0x0D);
                            destStream.WriteByte(0x0D);
                        }
                    }
                }

                int nNextBlockStart = i;
                for (; i < nRead; i++)
                {
                    if (buffer[i] == 0x0D)
                    {
                        if (i + 1 >= nRead)
                        {
                            bCarryOverCR = true;
                            destStream.Write(buffer, nNextBlockStart, i - nNextBlockStart);
                            nNextBlockStart = i + 1;
                        }
                        else if (buffer[i + 1] == 0x0D)
                        {
                            if (i + 2 >= nRead)
                            {
                                bCarryOverDoubleCR = true;
                                destStream.Write(buffer, nNextBlockStart, i - nNextBlockStart);
                                nNextBlockStart = i + 2;
                                break;
                            }
                            else if (buffer[i + 2] == 0x0A)
                            {
                                destStream.Write(buffer, nNextBlockStart, i - nNextBlockStart);
                                nNextBlockStart = i + 2;
                                i++;
                            }
                        }
                    }
                }
                if (nRead - nNextBlockStart > 0)
                {
                    destStream.Write(buffer, nNextBlockStart, nRead - nNextBlockStart);
                }
            }
            while ((nRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0);

            if (bCarryOverCR)
                destStream.WriteByte(0x0D);
            if (bCarryOverDoubleCR)
            {
                destStream.WriteByte(0x0D);
                destStream.WriteByte(0x0D);
            }
        }

        public async Task DoTapAsync(Point point)
        {
            Log.d(TAG, "Executing Tap to target points: " + ((int)Math.Round(point.X)).ToString() + " " + ((int)Math.Round(point.Y)).ToString());
            String strArgument = "shell input tap " + ((int)Math.Round(point.X)).ToString() + " " + ((int)Math.Round(point.Y)).ToString();
            await GetADBTextOutputAsync(GetAdbLocation(), strArgument
                , m_screenCapCancelToken != null ? m_screenCapCancelToken.Token : default(CancellationToken));
        }

        public async Task SaveScreenCapAsync(String strPath = null, String strFileName = null)
        {
            if (CurrentScreenCap == null)
                return;
            String strFullPath;
            if (strPath == null)
            {
                strPath = Properties.Settings.Default.PrefDownloadDir;
                if (String.IsNullOrWhiteSpace(strPath))
                    strPath = ReceiveFilesManager.GetDefaultDownloadDirectory();
            }

            if (strFileName == null)
            {
                String strName = "wireboard-" + DateTime.Now.Year + "-" + DateTime.Now.Month + "-" + DateTime.Now.Day + "-"
                    + DateTime.Now.Hour + "h" + DateTime.Now.Minute + "m";
                strFullPath = strPath + Path.DirectorySeparatorChar + strName + ".png";
                for (int i = 2; File.Exists(strFullPath); i++)
                {
                    strFullPath = strPath + Path.DirectorySeparatorChar + strName + "-" + i + ".png";
                }
            }
            else
                strFullPath = strPath + Path.DirectorySeparatorChar + strFileName;

            try
            {
                await Task.Run(() =>
                   {
                       BitmapEncoder encoder = new PngBitmapEncoder();
                       encoder.Frames.Add(BitmapFrame.Create(CurrentScreenCap));

                       using (var fileStream = new FileStream(strFullPath, FileMode.Create))
                       {
                           encoder.Save(fileStream);
                       }
                   });
                Log.i(TAG, "Saved screen capture as " + strFullPath, true);
            }
            catch (Exception e)
            {
                Log.e(TAG, "Failed to save screen capture. Error: " + e.Message, true);
            }
        }
    }
}
