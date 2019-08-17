using RtspClientSharp.MediaParsers;
using RtspClientSharp.RawFrames.Video;
using SimpleRtspPlayer.GUI;
using SimpleRtspPlayer.RawFramesDecoding;
using SimpleRtspPlayer.RawFramesDecoding.DecodedFrames;
using SimpleRtspPlayer.RawFramesDecoding.FFmpeg;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wireboard.BbEventArgs;
using Wireboard.TcpPackets;
using PixelFormat = SimpleRtspPlayer.RawFramesDecoding.PixelFormat;

namespace Wireboard
{
    public class AndroidScreenCapture : IScreenCapture, IVideoSource, INotifyPropertyChanged
    {
        private static String TAG = typeof(AndroidScreenCapture).Name;

        public enum EState
        {
            NONE,
            REQUESTED,
            WAITING_FOR_PERMISSION,
            ACTIVE,
        }

        private class Payload
        {
            public MemoryStream Data { get; set; }
        }

        private BbRemoteServer m_captureServer;
        private CancellationTokenSource m_CancelToken;
        private readonly Buffer​Block<Payload> m_payloadQueue = new BufferBlock<Payload>();
        private TaskCompletionSource<bool> m_startScreenCaptureTCS;
        private Task m_decodeTask;
        private IDecodedVideoFrame m_lastDecodedFrame;
        private readonly object m_decoderLock = new object();
        private Queue<Tuple<int, int>> m_transferRateList = new Queue<Tuple<int, int>>();
        private int m_nTransferRate = 0;
        public int TransferRate
        {
            get
            {
                while (m_transferRateList.Count > 0 && Environment.TickCount - m_transferRateList.Peek().Item1 > 5000)
                    m_nTransferRate -= m_transferRateList.Dequeue().Item2;
                return m_nTransferRate / 5;
            }
        }
        private bool m_bDidCaptureDuringConnection;
    

        public event EventHandler<IDecodedVideoFrame> FrameReceived;
        public event EventHandler<EventArgs> ScreenCaptureCancelled;
        public event PropertyChangedEventHandler PropertyChanged;

        private EState m_state;
        private EState State
        {
            get { return m_state; }
            set
            {
                if (m_state != value)
                {
                    m_state = value;
                    String[] aStrChanged = new String[] { "IsScreenCapActive", "IsShowing", "IsWaitingForPermission", "CanCapture" };
                    foreach (String s in aStrChanged)
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(s));
                }
            }
        }

        public bool IsScreenCapActive => State == EState.ACTIVE || State == EState.WAITING_FOR_PERMISSION || State == EState.REQUESTED;
        public bool IsShowing => State == EState.ACTIVE;
        public bool IsWaitingForPermission => State == EState.WAITING_FOR_PERMISSION;
        public bool IsScreenCapPaused => false;
        public bool IsScreenCapOutOfDate => false;
        public bool CanTap => false;
        public bool CanCapture => m_captureServer != null && m_captureServer.Attached && m_captureServer.SupportsScreenCapture && (m_captureServer.IsProVersion || !m_bDidCaptureDuringConnection || State != EState.NONE);

        private void DecodeTask()
        {
            H264VideoPayloadParser parser = new H264VideoPayloadParser(new RtspClientSharp.Codecs.Video.H264CodecInfo());
            FFmpegVideoDecoder decoder = FFmpegVideoDecoder.CreateDecoder(FFmpegVideoCodecId.H264);
            parser.FrameGenerated += (frame) =>
            {
                lock (m_decoderLock)
                {
                    IDecodedVideoFrame decodedFrame = decoder.TryDecode((RawVideoFrame)frame);
                    if (decodedFrame != null)
                    {
                        m_lastDecodedFrame = decodedFrame;
                        FrameReceived?.Invoke(this, decodedFrame);
                    }
                    else
                    {
                        Log.e(TAG, "Failed to decode frame");
                    }
                }
            };

            while (!m_CancelToken.Token.IsCancellationRequested)
            {                
                try
                {
                    Payload nextFramePayload = m_payloadQueue.Receive(m_CancelToken.Token);
                    parser.Parse(new TimeSpan(1), new ArraySegment<byte>(nextFramePayload.Data.GetBuffer(), (int)nextFramePayload.Data.Position, (int)nextFramePayload.Data.Remaining()), true);
                }
                catch (OperationCanceledException)
                {
                    break;
                }                
            }            
        }

        public async Task<bool> StartScreenCappingAsync()
        {
            if (State != EState.NONE || !CanCapture)
            {
                return false;
            }
            m_CancelToken?.Dispose();
            
            m_CancelToken = new CancellationTokenSource();
            m_startScreenCaptureTCS = new TaskCompletionSource<bool>();

            m_captureServer.ReceivedScreenCapture += OnScreenCaptureReceivedEvent;
            m_captureServer.SendStartCapture((byte)Properties.Settings.Default.AndroidScreenCapQuality);
            State = EState.REQUESTED;

            return await m_startScreenCaptureTCS.Task;
        }

        private async Task StartDecoder()
        {
            Task decodeTask = Task.Run(() => DecodeTask(), m_CancelToken.Token);
            try
            {
                await decodeTask;
            }
            catch (OperationCanceledException)
            {
                Log.d(TAG, "Decoding Task stopped");
            }
            catch (Exception e)
            {
                Log.e(TAG, "Error while decoding ScreenCapture data - " + e.Message);
                Log.i(TAG, "Screen capture has been cancelled due to an error. Check the debug log for details.", true);
                StopCapture(true, true);
            }
        }

        public async Task StopScreenCappingAsync()
        {
            if (State == EState.NONE)
                return;
            StopCapture(true, false);
            if (m_decodeTask != null)
            {
                await m_decodeTask;
            }
            m_decodeTask = null;
        }

        private void StopCapture(bool bNotifyRemote, bool bNotifyGUI)
        {
            if (State == EState.NONE)
                return;

            if (bNotifyRemote)
                m_captureServer?.SendStopCapture();

            State = EState.NONE;
            m_startScreenCaptureTCS?.TrySetResult(false);
            m_startScreenCaptureTCS = null;

            m_CancelToken?.Cancel();
            if (bNotifyGUI)
                ScreenCaptureCancelled?.Invoke(this, new EventArgs());

            if (m_captureServer != null)
                m_captureServer.ReceivedScreenCapture -= OnScreenCaptureReceivedEvent;

            m_lastDecodedFrame = null;
            m_transferRateList.Clear();
            m_nTransferRate = 0;
        }

        public void OnConnectionEvent(object sender, ConnectionEventArgs eventArgs)
        {
            if (eventArgs.NewState == ConnectionEventArgs.EState.DISCONNECTED)
            {
                StopCapture(false, true);
                m_captureServer = null;
                m_bDidCaptureDuringConnection = false;
            }
            else if (eventArgs.NewState == ConnectionEventArgs.EState.CONNECTED)
            {
                m_captureServer = sender as BbRemoteServer;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CanCapture"));
        }

        public void OnScreenCaptureReceivedEvent(object sender, ScreenCaptureStateEventArgs eventArgs)
        {
            if (eventArgs.HasNewData)
            {
                //Log.d(TAG, "Received screencapture data, size: " + eventArgs.Data.Length + " Remaining: " + eventArgs.Data.Remaining());
                if (State == EState.ACTIVE && !m_CancelToken.Token.IsCancellationRequested)
                {
                    m_transferRateList.Enqueue(new Tuple<int, int>(Environment.TickCount, (int)eventArgs.Data.Length));
                    m_nTransferRate += (int)eventArgs.Data.Length;
                    while (m_transferRateList.Count > 0 && Environment.TickCount - m_transferRateList.Peek().Item1 > 5000)
                        m_nTransferRate -= m_transferRateList.Dequeue().Item2;                  
                    m_payloadQueue.Post(new Payload() { Data = eventArgs.Data });
                }
            }
            else if (eventArgs.HasNewState)
            {
                if ((State == EState.WAITING_FOR_PERMISSION || State == EState.REQUESTED) && eventArgs.CaptureState == ECaptureState.STARTING)
                {
                    Log.i(TAG, "Screen capture successfully started", true);
                    m_bDidCaptureDuringConnection = true;
                    State = EState.ACTIVE;
                    //DbgShowTransferRateAsync();
                    m_decodeTask = StartDecoder();
                }
                else if (State == EState.REQUESTED && eventArgs.CaptureState == ECaptureState.WAITINGFORPERMISSION)
                {
                    Log.i(TAG, "Waiting for permission on the Android device");
                    State = EState.WAITING_FOR_PERMISSION;
                }
                else if (State != EState.NONE && eventArgs.CaptureState == ECaptureState.ERROR)
                {
                    if (eventArgs.ErrorCode == BBProtocol.CAPTURE_ERRORCDOE_TRIALOVER)
                        Log.i(TAG, "Screen capture ended due to time limit of the free version", true);
                    else if (eventArgs.ErrorCode == BBProtocol.CAPTURE_ERRORCDOE_TRIALNOTALLOWED)
                        Log.i(TAG, "The free version of Wireboard allows only one screen capture per connection", true);
                    else
                        Log.i(TAG, "Screen capture failed due to an error on the Android device", true);

                    StopCapture(false, true);
                }
                else if (State != EState.NONE && eventArgs.CaptureState == ECaptureState.PERMISSIONDENIED)
                {
                    Log.i(TAG, "Permission to capture the screen was denied on the Android device", true);
                    StopCapture(false, true);
                }

                m_startScreenCaptureTCS?.TrySetResult(State == EState.ACTIVE || State == EState.WAITING_FOR_PERMISSION);
                m_startScreenCaptureTCS = null;
            }
        }

        public async Task SaveScreenCapAsync(String strPath = null, String strFileName = null)
        {
            WriteableBitmap writeableBitmap;
            lock (m_decoderLock)
            {
                if (m_lastDecodedFrame == null || State != EState.ACTIVE)
                    return;
                TransformParameters transformParameters = new TransformParameters(RectangleF.Empty,
                 new System.Drawing.Size(m_lastDecodedFrame.Parameters.Width, m_lastDecodedFrame.Parameters.Height),
                 ScalingPolicy.RespectAspectRatio, PixelFormat.Bgra32, ScalingQuality.Bicubic);

                writeableBitmap = new WriteableBitmap(
                    m_lastDecodedFrame.Parameters.Width,
                    m_lastDecodedFrame.Parameters.Height,
                    ScreenInfo.DpiX,
                    ScreenInfo.DpiY,
                    PixelFormats.Pbgra32,
                    null);
                m_lastDecodedFrame.TransformTo(writeableBitmap.BackBuffer, writeableBitmap.BackBufferStride, transformParameters);
                writeableBitmap.Freeze();

            }
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
                    encoder.Frames.Add(BitmapFrame.Create(writeableBitmap));

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

        private async Task DbgShowTransferRateAsync()
        {
            while (State == EState.ACTIVE)
            {
                Log.s(TAG, "ScreenCap TransferRate: " + TransferRate.ToXByteSize(2) + "(" + TransferRate.ToMbitSpeed() + ")" );
                await Task.Delay(500);
            }
        }

    }
}
