using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wireboard.TcpPackets;

namespace Wireboard.BbEventArgs
{
    public class ScreenCaptureStateEventArgs
    {
        public ECaptureState CaptureState { get; }
        public bool HasNewData { get; } = false;
        public bool HasNewState { get; } = false;
        public int ErrorCode { get; } = 0;
        public MemoryStream Data { get; }

        public ScreenCaptureStateEventArgs(ECaptureState eCaptureState, int nErrorCode)
        {
            CaptureState = eCaptureState;
            ErrorCode = nErrorCode;
            HasNewState = true;
        }

        public ScreenCaptureStateEventArgs(MemoryStream captureData)
        {
            Data = captureData;
            HasNewData = true;
        }
    }
}
