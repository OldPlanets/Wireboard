using System;

namespace SimpleRtspPlayer.RawFramesDecoding.DecodedFrames
{
    class DecodedVideoFrame : IDecodedVideoFrame
    {
        private readonly Action<IntPtr, int, TransformParameters> _transformAction;
        public DecodedVideoFrameParameters Parameters { get;  }

        public DecodedVideoFrame(Action<IntPtr, int, TransformParameters> transformAction, DecodedVideoFrameParameters parameters)
        {
            Parameters = parameters;
            _transformAction = transformAction;
        }

        public void TransformTo(IntPtr buffer, int bufferStride, TransformParameters transformParameters)
        {
            _transformAction(buffer, bufferStride, transformParameters);
        }
    }
}