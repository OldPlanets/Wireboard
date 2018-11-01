using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
namespace Wireboard
{
    public class Log
    {
        public enum ESeverity { DEBUG, WARNING, ERROR, INFO, STATUS }
        public class LogEventArgs : EventArgs
        {
            public LogEventArgs(ESeverity severity, string tag, string message, bool showInStatusbar)
            {
                Severity = severity;
                Tag = tag;
                Message = message;
                ShowInStatusbar = showInStatusbar;
            }

            public ESeverity Severity { get; private set; }
            public String Tag { get; private set; }
            public String Message { get; private set; }
            public bool ShowInStatusbar { get; private set; }
        }

        private const int MAX_LINES = 200;
        private static BlockingCollection<string> logList = new BlockingCollection<string>();
        internal static event EventHandler<LogEventArgs> LogAdded;
        internal static event EventHandler<LogEventArgs> StatusChanged;

        public static void e(String tag, String msg, bool bShowinStatusbar = false)
        {
            Trace.WriteLine(tag + ": " + msg);
            addLineToLog(tag, msg, ESeverity.ERROR, bShowinStatusbar);
        }

        public static void d(String tag, String msg, bool bShowinStatusbar = false)
        {
            Trace.WriteLine(tag + ": " + msg);
            addLineToLog(tag, msg, ESeverity.DEBUG, bShowinStatusbar);
        }

        public static void i(String tag, String msg, bool bShowinStatusbar = false)
        {
            Trace.WriteLine(tag + ": " + msg);
            addLineToLog(tag, msg, ESeverity.INFO, bShowinStatusbar);
        }

        public static void w(String tag, String msg, bool bShowinStatusbar = false)
        {
            Trace.WriteLine(tag + ": " + msg);
            addLineToLog(tag, msg, ESeverity.WARNING, bShowinStatusbar);
        }

        public static void s(String tag, String msg)
        {
            addLineToLog(tag, msg, ESeverity.STATUS, true);
        }

        private static void addLineToLog(String tag, string sLine, ESeverity severity, bool bShowinStatusbar = false)
        {
            logList.Add(DateTime.Now.ToLongTimeString() + " - " + sLine);
            if (logList.Count > MAX_LINES)
                logList.Take();

            if (LogAdded != null || (StatusChanged != null && bShowinStatusbar))
            {
                LogEventArgs args = new LogEventArgs(severity, tag, sLine, bShowinStatusbar);
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.Invoke(new Action(() => 
                        {
                            LogAdded?.Invoke(null, args);
                            if (bShowinStatusbar)
                                StatusChanged?.Invoke(null, args);
                        }));
                }
                else
                {
                    LogAdded?.Invoke(null, args);
                    if (bShowinStatusbar)
                        StatusChanged?.Invoke(null, args);
                }
            }
        }

        public static string getLog()
        {
            return String.Join("\n", logList.ToArray());
        }
    }
}
