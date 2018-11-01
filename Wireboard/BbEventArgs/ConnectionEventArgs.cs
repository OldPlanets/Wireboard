using System;

namespace Wireboard.BbEventArgs
{
    public class ConnectionEventArgs : EventArgs
    {
        public ConnectionEventArgs(EState newState, int nSessionID, bool bUserRequested = false
            , bool bPesistentError = false, bool bRemoteMinVersionError = false, bool bLocalMinVersionError = false)
        {
            NewState = newState;
            SessionID = nSessionID;
            UserRequested = bUserRequested;
            PesistentError = bPesistentError;
            RemoteMinVersionError = bRemoteMinVersionError;
            LocalMinVersionError = bLocalMinVersionError;
        }

        public enum EState { CONNECTED, DISCONNECTED}
        public EState NewState { get; set; }
        public int SessionID { get; set; }
        public bool PesistentError { get; set; }
        public bool UserRequested { get; set; }
        public bool RemoteMinVersionError { get; set; }
        public bool LocalMinVersionError { get; set; }

    }
}