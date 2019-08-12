using System;
using RtspClientSharp.Codecs;
using RtspClientSharp.Codecs.Video;
using RtspClientSharp.RawFrames;

namespace RtspClientSharp.MediaParsers
{
    abstract class MediaPayloadParser : IMediaPayloadParser
    {
        private DateTime _baseTime = DateTime.MinValue;

        public Action<RawFrame> FrameGenerated { get; set; }

        public abstract void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit);

        public abstract void ResetState();

        protected DateTime GetFrameTimestamp(TimeSpan timeOffset)
        {
            if (timeOffset == TimeSpan.MinValue)
                return DateTime.UtcNow;

            if (_baseTime == DateTime.MinValue)
                _baseTime = DateTime.UtcNow;

            return _baseTime + timeOffset;
        }

        protected virtual void OnFrameGenerated(RawFrame e)
        {
            FrameGenerated?.Invoke(e);
        }

        public static IMediaPayloadParser CreateFrom(CodecInfo codecInfo)
        {
            switch (codecInfo)
            {
                case H264CodecInfo h264CodecInfo:
                    return new H264VideoPayloadParser(h264CodecInfo);
                default:
                    throw new ArgumentOutOfRangeException(nameof(codecInfo),
                        $"Unsupported codec: {codecInfo.GetType().Name}");
            }
        }
    }
}