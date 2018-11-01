using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Wireboard
{
    public static class Extensions
    {
        public static IPAddress GetBroadcastAddress(this UnicastIPAddressInformation uniInfo)
        {
            if (uniInfo.Address.AddressFamily != AddressFamily.InterNetwork)
                return null;
            byte[] broadcastIPBytes = new byte[4];
            byte[] hostBytes = uniInfo.Address.GetAddressBytes();
            byte[] maskBytes = uniInfo.IPv4Mask.GetAddressBytes();
            for (int i = 0; i < 4; i++)
            {
                broadcastIPBytes[i] = (byte)(hostBytes[i] | (byte)~maskBytes[i]);
            }
            return new IPAddress(broadcastIPBytes);
        }

        public static async Task<int> SendAsync(this UdpClient udp, BinaryWriter packet, IPAddress ip, ushort uPort)
        {
            Debug.Assert(packet.BaseStream is MemoryStream);
            if (packet.BaseStream is MemoryStream)
            {
                IPEndPoint ep = new IPEndPoint(ip, uPort);
                return await udp.SendAsync(((MemoryStream)packet.BaseStream).GetBuffer(), (int)packet.BaseStream.Position, ep);
            }
            else
                return 0;
        }

        public static long Remaining(this MemoryStream stream)
        {
            return stream.Length - stream.Position;
        }

        public static bool IsPrivateIP(this IPAddress ip)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] bytes = ip.GetAddressBytes();
                switch (bytes[0])
                {
                    case 10:
                        return true;
                    case 172:
                        return bytes[1] < 32 && bytes[1] >= 16;
                    case 192:
                        return bytes[1] == 168;
                    default:
                        return false;
                }
            }
            else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return ((ip.GetAddressBytes()[0] == 0xfe) && ((ip.GetAddressBytes()[1] & 0xc0) == (0x80 & 0xc0))) // fe80::/10 link local prefix
                        || (ip.GetAddressBytes()[0] == 0xfd); // ULA prefix
            }
            else
                return false;
        }

        private const long OneKb = 1024;
        private const long OneMb = OneKb * 1024;
        private const long OneGb = OneMb * 1024;
        private const long OneTb = OneGb * 1024;

        public static string ToXByteSize(this int value, int decimalPlaces = 1)
        {
            return ((ulong)value).ToXByteSize(decimalPlaces);
        }

        public static string ToXByteSize(this long value, int decimalPlaces = 1)
        {
            return ((ulong)value).ToXByteSize(decimalPlaces);
        }

        public static string ToXByteSize(this ulong value, int decimalPlaces = 1)
        {
            var asTb = Math.Round((double)value / OneTb, decimalPlaces);
            var asGb = Math.Round((double)value / OneGb, decimalPlaces);
            var asMb = Math.Round((double)value / OneMb, decimalPlaces);
            var asKb = Math.Round((double)value / OneKb, decimalPlaces);
            string chosenValue = asTb > 1 ? string.Format("{0}Tb", asTb)
                : asGb > 1 ? string.Format("{0}Gb", asGb)
                : asMb > 1 ? string.Format("{0}Mb", asMb)
                : asKb > 1 ? string.Format("{0}Kb", asKb)
                : string.Format("{0}B", Math.Round((double)value, decimalPlaces));
            return chosenValue;
        }

        public static void ColorLogText(this TextRange tr, Log.ESeverity severity)
        {
            if (severity == Log.ESeverity.ERROR)
            {
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Red);
            }
            else if (severity == Log.ESeverity.WARNING)
            {
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Orange);
            }
        }

        public static bool RunHyperlink(this Uri uri)
        {
            Process process = new Process();
            process.StartInfo.FileName = uri.ToString();
            process.StartInfo.UseShellExecute = true;
            try
            {
                process.Start();
                return true;
            }
            catch (Exception e)
            {
                Log.w("", "Failed to open link " + uri.ToString() + " - " + e.Message);
            }
            return false;
        }

        public static bool IsValidIP(this String strIP)
        {
            if (IPAddress.TryParse(strIP, out IPAddress ip))
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    var quads = strIP.Split('.');
                    if (!(quads.Length == 4)) return false;
                    foreach (var quad in quads)
                    {
                        if (!Int32.TryParse(quad, out int q)
                            || !q.ToString().Length.Equals(quad.Length)
                            || q < 0
                            || q > 255) { return false; }

                    }
                    return true;
                }
                else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    return true;
            }
            return false;
        }

        public static unsafe byte[] ToByteArray(this SecureString secureString)
        {

            int maxLength = Encoding.UTF8.GetMaxByteCount(secureString.Length);

            IntPtr bytes = IntPtr.Zero;
            IntPtr str = IntPtr.Zero;

            try
            {
                bytes = Marshal.AllocHGlobal(maxLength);
                str = Marshal.SecureStringToBSTR(secureString);

                char* chars = (char*)str.ToPointer();
                byte* bptr = (byte*)bytes.ToPointer();
                int len = Encoding.UTF8.GetBytes(chars, secureString.Length, bptr, maxLength);

                byte[] _bytes = new byte[len];
                for (int i = 0; i < len; ++i)
                {
                    _bytes[i] = *bptr;
                    bptr++;
                }

                return _bytes;
            }
            finally
            {
                if (bytes != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(bytes);
                }
                if (str != IntPtr.Zero)
                {
                    Marshal.ZeroFreeBSTR(str);
                }
            }
        }

        public static async Task<bool> WaitForExitAsync(this Process process, int nTimeOut, CancellationToken cancellationToken)
        {
            Task timeout = Task.Delay(nTimeOut);
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            process.Exited += (sender, args) => tcs.TrySetResult(null);
            process.EnableRaisingEvents = true;
            if (cancellationToken != default(CancellationToken))
            {
                using (cancellationToken.Register(() => { tcs.TrySetCanceled(); }))
                {
                    Task finished = await Task.WhenAny(timeout, tcs.Task);
                    return finished != timeout && !tcs.Task.IsCanceled;
                }
            }
            else
            {
                Task finished = await Task.WhenAny(timeout, tcs.Task);
                return finished != timeout && !tcs.Task.IsCanceled;
            }
        }

        public static void ShowTruncatedTextAsToolTip(this TextBox textBox)
        {
            String wm = TextBoxHelper.GetWatermark(textBox);
            TextBoxHelper.SetWatermark(textBox, null);
            textBox.Measure(new Size(Double.MaxValue, Double.MaxValue));
            TextBoxHelper.SetWatermark(textBox, wm);
            var width = textBox.DesiredSize.Width;

            if (textBox.ActualWidth < width)
            {
                ToolTipService.SetToolTip(textBox, textBox.Text);
            }
            else
            {
                ToolTipService.SetToolTip(textBox, null);
            }
        }
    }
}
