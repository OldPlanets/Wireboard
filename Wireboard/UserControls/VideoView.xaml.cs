/*MIT License

Copyright(c) 2018 Bogdanov Kirill

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.*/

using SimpleRtspPlayer.GUI;
using SimpleRtspPlayer.RawFramesDecoding;
using SimpleRtspPlayer.RawFramesDecoding.DecodedFrames;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using PixelFormat = SimpleRtspPlayer.RawFramesDecoding.PixelFormat;
using Size = System.Drawing.Size;

namespace Wireboard.UserControls
{
    public partial class VideoView
    {
        private static readonly System.Windows.Media.Color DefaultFillColor = Colors.Black;
        private static readonly TimeSpan ResizeHandleTimeout = TimeSpan.FromMilliseconds(500);

        private System.Windows.Media.Color _fillColor = DefaultFillColor;
        private WriteableBitmap _writeableBitmap;

        private Size m_size;
        private System.Windows.Size m_constraints;
        private Int32Rect _dirtyRect;
        private TransformParameters _transformParameters;
        private IDecodedVideoFrame _lastFrame;
        private readonly Action<IDecodedVideoFrame> _invalidateAction;

        private Task _handleSizeChangedTask = Task.CompletedTask;
        private CancellationTokenSource _resizeCancellationTokenSource = new CancellationTokenSource();

        public static readonly DependencyProperty VideoSourceProperty = DependencyProperty.Register(nameof(VideoSource),
            typeof(IVideoSource),
            typeof(VideoView),
            new FrameworkPropertyMetadata(OnVideoSourceChanged));

        public IVideoSource VideoSource
        {
            get => (IVideoSource)GetValue(VideoSourceProperty);
            set => SetValue(VideoSourceProperty, value);
        }

        public VideoView()
        {
            InitializeComponent();
            _invalidateAction = Invalidate;
        }

        protected override System.Windows.Size MeasureOverride(System.Windows.Size constraint)
        {
            m_constraints = constraint;
            Size newSize = GetPreferredSize((int)constraint.Width, (int)constraint.Height);           
            if (newSize.Height > 0 && newSize.Width > 0 && m_size != newSize)
            {
                _resizeCancellationTokenSource.Cancel();
                _resizeCancellationTokenSource = new CancellationTokenSource();

                _handleSizeChangedTask = _handleSizeChangedTask.ContinueWith(prev =>
                    HandleSizeChangedAsync(newSize, _resizeCancellationTokenSource.Token));
            }

            return base.MeasureOverride(constraint);
        }

        private async Task HandleSizeChangedAsync(Size size, CancellationToken token)
        {
            try
            {
                await Task.Delay(ResizeHandleTimeout, token).ConfigureAwait(false);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ReinitializeBitmap(size, true);
                }, DispatcherPriority.Send, token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private Size GetPreferredSize(int width, int height)
        {
            if (_lastFrame != null)
            {
                float srcAspectRatio = (float)_lastFrame.Parameters.Width / _lastFrame.Parameters.Height;
                float destAspectRatio = (float)width / height;
                if (destAspectRatio < srcAspectRatio)
                    return new Size(width, _lastFrame.Parameters.Height * width / _lastFrame.Parameters.Width);
                else
                    return new Size(_lastFrame.Parameters.Width * height / _lastFrame.Parameters.Height, height);
            }
            return new Size(width, height);
        }

        private void ReinitializeBitmap(Size size, bool bInvalidate)
        {

             m_size = size;
            _dirtyRect = new Int32Rect(0, 0, m_size.Width, m_size.Height);

            _transformParameters = new TransformParameters(RectangleF.Empty,
                    new System.Drawing.Size(m_size.Width, m_size.Height),
                    ScalingPolicy.RespectAspectRatio, PixelFormat.Bgra32, ScalingQuality.Bicubic);

            _writeableBitmap = new WriteableBitmap(
                m_size.Width,
                m_size.Height,
                ScreenInfo.DpiX,
                ScreenInfo.DpiY,
                PixelFormats.Pbgra32,
                null);

            RenderOptions.SetBitmapScalingMode(_writeableBitmap, BitmapScalingMode.NearestNeighbor);
            if (bInvalidate)
                Invalidate(_lastFrame);
        }

        private static void OnVideoSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (VideoView)d;

            if (e.OldValue is IVideoSource oldVideoSource)
                oldVideoSource.FrameReceived -= view.OnFrameReceived;

            if (e.NewValue is IVideoSource newVideoSource)
                newVideoSource.FrameReceived += view.OnFrameReceived;
        }

        private void OnFrameReceived(object sender, IDecodedVideoFrame decodedFrame)
        {
            if (_lastFrame == null || _lastFrame.Parameters != decodedFrame.Parameters)
            {
                _lastFrame = decodedFrame;
                if (m_constraints != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ReinitializeBitmap(GetPreferredSize((int)m_constraints.Width, (int)m_constraints.Height), false);
                    }, DispatcherPriority.Send);
                }
            }
            _lastFrame = decodedFrame;
            Application.Current.Dispatcher.Invoke(_invalidateAction, DispatcherPriority.Send, decodedFrame);
        }

        private void Invalidate(IDecodedVideoFrame decodedVideoFrame)
        {
            if (VideoImage.Source != _writeableBitmap)
                VideoImage.Source = _writeableBitmap;

            if (m_size.Width == 0 || m_size.Height == 0)
                return;

            _writeableBitmap.Lock();

            try
            {
                decodedVideoFrame.TransformTo(_writeableBitmap.BackBuffer, _writeableBitmap.BackBufferStride, _transformParameters);

                _writeableBitmap.AddDirtyRect(_dirtyRect);
            }
            finally
            {
                _writeableBitmap.Unlock();
            }
        }
    }
}
