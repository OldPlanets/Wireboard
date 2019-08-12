using System;

namespace SimpleRtspPlayer.RawFramesDecoding.DecodedFrames
{
    public interface IDecodedVideoFrame
    {
        DecodedVideoFrameParameters Parameters { get; }
        void TransformTo(IntPtr buffer, int bufferStride, TransformParameters transformParameters);
    }
}