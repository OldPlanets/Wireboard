using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard
{
    public abstract class BBProtocol
    {
        private static String TAG = typeof(BBProtocol).Name;

        public const UInt16 DEFAULT_TCP_PORT = 7591;
        public const byte CURRENT_DISCOVERY_PROTOCOL_VERSION = 0x01;
        public const byte CURRENT_PROTOCOL_VERSION = 0x01;
        public const byte MIN_PROTOCOL_VERSION = 0x01;
        public const ushort DISCOVERYPORT = 52946;


        public const byte REQUESTED_DISCOVERY_PROTOCOL_VERSION = 0x01;
        public const byte REQUESTED_PROTOCOL_VERSION = 0x01;

        public const ushort PROTOCOL_HEADER = 0x2D4A;
        public static readonly byte[] ENCRYPTION_MAGICVALUE = new byte[] { 0x39, 0xF2, 0x1A, 0x82 };
        public const ushort DISCOVERY_HEADERSIZE = 5; // protocol header[16] packet size[16]
        public const ushort TCP_HEADERSIZE = 5; // protocol header[16] packet size[16]

        public const ushort MAX_TCPPACKETSIZE_WITHOUTHEADER = Int16.MaxValue - TCP_HEADERSIZE;
        public const ushort MAX_DISCPACKETSIZE_WITHOUTHEADER = Int16.MaxValue - DISCOVERY_HEADERSIZE;
        public const uint MAX_STRINGSIZE = 256 * 1024;

        public const byte OP_DISCOVERY_REQUEST = 0x01;
        public const byte OP_DISCOVERY_ANSWER = 0x02;

        public const byte OP_HELLO_REQUEST = 0x03;
        public const byte OP_HELLO_ANSWER = 0x04;
        public const byte OP_FRAGMENT = 0x05;
        public const byte OP_TEST = 0x06;
        public const byte OP_ATTACH_REQUEST = 0x07;
        public const byte OP_ATTACH_ANSWER = 0x08;
        public const byte OP_SENDTEXT = 0x09;
        public const byte OP_REMOVETEXT = 0x0A;
        public const byte OP_SENDSPECIALKEY = 0x0B;
        public const byte OP_TEXTSYNCUPDATE = 0x0C;
        public const byte OP_INPUTFOCUSCHANGE = 0x11;
        public const byte OP_SENDICON = 0x12;
        public const byte OP_DOIMEACTION = 0x13;
        public const byte OP_SETDISPLAYLOCK = 0x14;
        public const byte OP_REQUESTICON = 0x15;
        public const byte OP_SWITCHAPP = 0x16;
        public const byte OP_BATTERYSTATUS = 0x17;
        public const byte OP_PING = 0x18;
        public const byte OP_PING_ACK = 0x19;

        public const byte OP_STARTSENDFILE = 0x20;
        public const byte OP_SENDFILECOMMAND = 0x21;
        public const byte OP_SENDFILEDATA = 0x22;
        public const byte OP_SHARETEXT = 0x23;
        public const byte OP_SHAREDCLIPBOARDCONTENT = 0x24;

        public const byte OP_OPTIONSCHANGE = 0x25;

        public const byte OP_STARTENCRYPTION = 0x30;
        public const byte OP_ENCRYPTIONACK = 0x31;
        public const byte OP_ENCRYPTIONAUTHREQ = 0x32;
        public const byte OP_ENCRYPTIONAUTHANS = 0x33;




        public const int TYPE_MASK_CLASS = 15;
        public const int TYPE_MASK_VARIATION = 4080;
        public const int IME_MASK_ACTION = 0x000000FF;
        public const int IME_ACTION_DONE = 6;
        public const int IME_ACTION_GO = 2;
        public const int IME_ACTION_NEXT = 5;
        public const int IME_ACTION_NONE = 1;
        public const int IME_ACTION_PREVIOUS = 7;
        public const int IME_ACTION_SEARCH = 3;
        public const int IME_ACTION_SEND = 4;
        public const int IME_ACTION_UNSPECIFIED = 0;
        public const int IME_FLAG_FORCE_ASCII = -2147483648;
        public const int IME_FLAG_NAVIGATE_NEXT = 134217728;
        public const int IME_FLAG_NAVIGATE_PREVIOUS = 67108864;
        public const int IME_FLAG_NO_ACCESSORY_ACTION = 536870912;
        public const int IME_FLAG_NO_ENTER_ACTION = 1073741824;
        public const int IME_NULL = 0;




        public static void WriteString(String str, BinaryWriter buf)
        {
            if (str == null)
            {
                buf.Write((UInt32)0);
            }
            else
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str);
                if (bytes.Length > MAX_STRINGSIZE)
                    buf.Write((UInt32)0);
                else
                {
                    buf.Write((UInt32)bytes.Length);
                    buf.Write(bytes);
                }
            }
        }

        public static String ReadString(BinaryReader buf)
        {
            uint len = buf.ReadUInt32();
            if (len > 0)
            {
                byte[] strBuf = buf.ReadBytes((int)len);
                String res = null;
                try
                {
                    res = System.Text.Encoding.UTF8.GetString(strBuf);
                }
                catch (Exception e) when (e is ArgumentException || e is DecoderFallbackException)
                {
                    Log.e(TAG, "Error decoding string");
                }
                return (res != null) ? res : "";
            }
            else
                return "";
        }

        public static void WriteByteArray(byte[] array, BinaryWriter buf)
        {
            buf.Write((UInt16)array.Length);
            buf.Write(array);
        }

        public static byte[] ReadByteArray(BinaryReader buf)
        {
            ushort len = buf.ReadUInt16();
            if (len > 0 && len < MAX_TCPPACKETSIZE_WITHOUTHEADER)
            {
                return buf.ReadBytes(len);
            }
            else
                return new byte[0];
        }

        public static int GetMaxEncodedBytes(String str)
        {
            if (str == null)
                return 4;
            else
                return 4 + Encoding.UTF8.GetMaxByteCount(str.Length);
        }


        public static void WriteInetAddress(IPAddress address, BinaryWriter buf)
        {
            if (address == null)
            {
                buf.Write((byte)0); // none / empty / unknown
            }
            else if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                buf.Write((byte)1); // ipv4
                Debug.Assert(address.GetAddressBytes().Length == 4);
                buf.Write(address.GetAddressBytes(), 0, 4);
            }
            else if ((address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6))
                {
                buf.Write((byte)2); // ipv6
                Debug.Assert(address.GetAddressBytes().Length == 16);
                buf.Write(address.GetAddressBytes(), 0, 16);
            }
        }

        public static IPAddress ReadInetAddress(BinaryReader buf)
        {
            byte type = buf.ReadByte();
            if (type == 1)
            {
                //ipv4
                byte[] ipBuf = buf.ReadBytes(4);
                return new IPAddress(ipBuf);
            }
            else if (type == 2)
            {
                //ipv6
                byte[] ipBuf = buf.ReadBytes(16);
                return new IPAddress(ipBuf);
            }
            else
                return null;
        }
    }

}
