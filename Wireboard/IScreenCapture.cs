using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard
{
    public interface IScreenCapture : INotifyPropertyChanged
    {
        bool IsScreenCapActive { get; }
        bool IsShowing { get; }
        bool IsWaitingForPermission { get; }
        bool IsScreenCapPaused { get; }
        bool IsScreenCapOutOfDate { get; }
        bool CanCapture { get; }
        bool CanTap { get; }

        Task<bool> StartScreenCappingAsync();
        Task StopScreenCappingAsync();
        Task SaveScreenCapAsync(String strPath = null, String strFileName = null);
    }
}
