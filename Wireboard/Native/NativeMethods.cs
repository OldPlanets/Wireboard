using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.Native
{
    class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct BROWSEINFO
        {
            /// <summary>
            ///     Handle to the owner window for the dialog box.
            /// </summary>
            public IntPtr HwndOwner;

            /// <summary>
            ///     Pointer to an item identifier list (PIDL) specifying the
            ///     location of the root folder from which to start browsing.
            /// </summary>
            public IntPtr Root;

            /// <summary>
            ///     Address of a buffer to receive the display name of the
            ///     folder selected by the user.
            /// </summary>
            [MarshalAs(UnmanagedType.LPStr)] public string DisplayName;

            /// <summary>
            ///     Address of a null-terminated string that is displayed
            ///     above the tree view control in the dialog box.
            /// </summary>
            [MarshalAs(UnmanagedType.LPStr)] public string Title;

            /// <summary>
            ///     Flags specifying the options for the dialog box.
            /// </summary>
            public uint Flags;

            /// <summary>
            ///     Address of an application-defined function that the
            ///     dialog box calls when an event occurs.
            /// </summary>
            [MarshalAs(UnmanagedType.FunctionPtr)] public WndProc Callback;

            /// <summary>
            ///     Application-defined value that the dialog box passes to
            ///     the callback function
            /// </summary>
            public int LParam;

            /// <summary>
            ///     Variable to receive the image associated with the selected folder.
            /// </summary>
            public int Image;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HResult
        {
            private int _value;

            public int Value
            {
                get { return _value; }
            }

            public Exception Exception
            {
                get { return Marshal.GetExceptionForHR(_value); }
            }

            public bool IsSuccess
            {
                get { return _value >= 0; }
            }

            public bool IsFailure
            {
                get { return _value < 0; }
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public SHFILEINFO(bool x)
            {
                hIcon = IntPtr.Zero;
                iIcon = 0;
                dwAttributes = 0;
                szDisplayName = "";
                szTypeName = "";
            }
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [Flags]
        public enum FolderBrowserOptions
        {
            /// <summary>
            ///     None.
            /// </summary>
            None = 0,

            /// <summary>
            ///     For finding a folder to start document searching
            /// </summary>
            FolderOnly = 0x0001,

            /// <summary>
            ///     For starting the Find Computer
            /// </summary>
            FindComputer = 0x0002,

            /// <summary>
            ///     Top of the dialog has 2 lines of text for BROWSEINFO.lpszTitle and
            ///     one line if this flag is set.  Passing the message
            ///     BFFM_SETSTATUSTEXTA to the hwnd can set the rest of the text.
            ///     This is not used with BIF_USENEWUI and BROWSEINFO.lpszTitle gets
            ///     all three lines of text.
            /// </summary>
            ShowStatusText = 0x0004,
            ReturnAncestors = 0x0008,

            /// <summary>
            ///     Add an editbox to the dialog
            /// </summary>
            ShowEditBox = 0x0010,

            /// <summary>
            ///     insist on valid result (or CANCEL)
            /// </summary>
            ValidateResult = 0x0020,

            /// <summary>
            ///     Use the new dialog layout with the ability to resize
            ///     Caller needs to call OleInitialize() before using this API
            /// </summary>
            UseNewStyle = 0x0040,
            UseNewStyleWithEditBox = (UseNewStyle | ShowEditBox),

            /// <summary>
            ///     Allow URLs to be displayed or entered. (Requires BIF_USENEWUI)
            /// </summary>
            AllowUrls = 0x0080,

            /// <summary>
            ///     Add a UA hint to the dialog, in place of the edit box. May not be
            ///     combined with BIF_EDITBOX.
            /// </summary>
            ShowUsageHint = 0x0100,

            /// <summary>
            ///     Do not add the "New Folder" button to the dialog.  Only applicable
            ///     with BIF_NEWDIALOGSTYLE.
            /// </summary>
            HideNewFolderButton = 0x0200,

            /// <summary>
            ///     don't traverse target as shortcut
            /// </summary>
            GetShortcuts = 0x0400,

            /// <summary>
            ///     Browsing for Computers.
            /// </summary>
            BrowseComputers = 0x1000,

            /// <summary>
            ///     Browsing for Printers.
            /// </summary>
            BrowsePrinters = 0x2000,

            /// <summary>
            ///     Browsing for Everything
            /// </summary>
            BrowseFiles = 0x4000,

            /// <summary>
            ///     sharable resources displayed (remote shares, requires BIF_USENEWUI)
            /// </summary>
            BrowseShares = 0x8000
        }

        /// <summary>Maximal Length of unmanaged Windows-Path-strings</summary>
        private const int MAX_PATH = 260;
        /// <summary>Maximal Length of unmanaged Typename</summary>
        private const int MAX_TYPE = 80;


        [Flags]
        public enum SHGFI : int
        {
            /// <summary>get icon</summary>
            Icon = 0x000000100,
            /// <summary>get display name</summary>
            DisplayName = 0x000000200,
            /// <summary>get type name</summary>
            TypeName = 0x000000400,
            /// <summary>get attributes</summary>
            Attributes = 0x000000800,
            /// <summary>get icon location</summary>
            IconLocation = 0x000001000,
            /// <summary>return exe type</summary>
            ExeType = 0x000002000,
            /// <summary>get system icon index</summary>
            SysIconIndex = 0x000004000,
            /// <summary>put a link overlay on icon</summary>
            LinkOverlay = 0x000008000,
            /// <summary>show icon in selected state</summary>
            Selected = 0x000010000,
            /// <summary>get only specified attributes</summary>
            Attr_Specified = 0x000020000,
            /// <summary>get large icon</summary>
            LargeIcon = 0x000000000,
            /// <summary>get small icon</summary>
            SmallIcon = 0x000000001,
            /// <summary>get open icon</summary>
            OpenIcon = 0x000000002,
            /// <summary>get shell size icon</summary>
            ShellIconSize = 0x000000004,
            /// <summary>pszPath is a pidl</summary>
            PIDL = 0x000000008,
            /// <summary>use passed dwFileAttribute</summary>
            UseFileAttributes = 0x000000010,
            /// <summary>apply the appropriate overlays</summary>
            AddOverlays = 0x000000020,
            /// <summary>Get the index of the overlay in the upper 8 bits of the iIcon</summary>
            OverlayIndex = 0x000000040,
        };

        [ComImport, SuppressUnmanagedCodeSecurity, Guid("00000002-0000-0000-c000-000000000046"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMalloc
        {
            [PreserveSig]
            IntPtr Alloc(int cb);

            [PreserveSig]
            IntPtr Realloc(IntPtr pv, int cb);

            [PreserveSig]
            void Free(IntPtr pv);

            [PreserveSig]
            int GetSize(IntPtr pv);

            [PreserveSig]
            int DidAlloc(IntPtr pv);

            [PreserveSig]
            void HeapMinimize();
        }

        [SecurityCritical]
        public static IMalloc GetSHMalloc()
        {
            IMalloc[] ppMalloc = new IMalloc[1];
            SHGetMalloc(ppMalloc);
            return ppMalloc[0];
        }

        [SecurityCritical, DllImport("shell32")]
        private static extern int SHGetMalloc([Out, MarshalAs(UnmanagedType.LPArray)] IMalloc[] ppMalloc);

        public delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [SecurityCritical, DllImport("shell32")]
        public static extern int SHGetFolderLocation(IntPtr hwndOwner, Int32 nFolder, IntPtr hToken, uint dwReserved,
    out IntPtr ppidl);

        [SecurityCritical, DllImport("shell32")]
        public static extern int SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string pszName, IntPtr pbc,
            out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

        [SecurityCritical, DllImport("shell32")]
        public static extern IntPtr SHBrowseForFolder(ref BROWSEINFO lbpi);

        [SecurityCritical, DllImport("shell32", CharSet = CharSet.Auto)]
        public static extern bool SHGetPathFromIDList(IntPtr pidl, IntPtr pszPath);

        [SecurityCritical, DllImport("shell32")]
        private static extern int SHGetFileInfo(IntPtr pidl, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint flags);

        [SecurityCritical, DllImport("shell32")]
        private static extern void ILFree(IntPtr pidl);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern int SHGetFileInfo(
          string pszPath,
          int dwFileAttributes,
          out SHFILEINFO psfi,
          uint cbfileInfo,
          SHGFI uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }
}
