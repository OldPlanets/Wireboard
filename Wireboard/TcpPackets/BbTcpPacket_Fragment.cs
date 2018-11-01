using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.TcpPackets
{
    class BbTcpPacket_Fragment : BbTcpPacket
    {
        public int FragmentID { get; private set; }
        private int m_nFragmentNumber;
        private int m_nTotalFragmentsCount;
        private MemoryStream m_bbData;
        private byte m_byPayloadOpcode;
        private List<BbTcpPacket_Fragment> m_liFragments;

        internal BbTcpPacket_Fragment(BbTcpPacket src) : base(src)
        {
        }

        internal void ProcessData(BinaryReader data)
        {
            //[FragmentID 4] [FragmentCur 4] [FragmentLast 4] [Opcode 1] [payload]
            FragmentID = data.ReadInt32();
            m_nFragmentNumber = data.ReadInt32();
            m_nTotalFragmentsCount = data.ReadInt32();
            m_byPayloadOpcode = data.ReadByte();
            Log.d(TAG, $"Received Fragment, ID: {FragmentID}, Total: {m_nTotalFragmentsCount}, Part: {m_nFragmentNumber}, PayloadOp: {m_byPayloadOpcode}");
            m_bbData = (MemoryStream)data.BaseStream;
            if (m_nTotalFragmentsCount <= 1 || m_nFragmentNumber >= m_nTotalFragmentsCount)
            {
                IsValid = false;
                ParseError = "Invalid fragment header";
            }
            else
            {
                IsValid = true;
            }
        }

        public bool addFragment(BbTcpPacket_Fragment fragment)
        {
            if (m_liFragments == null)
            {
                m_liFragments = new List<BbTcpPacket_Fragment>();
                m_liFragments.Add(this);
            }
            if (m_liFragments.Contains(fragment))
                return false;
            m_liFragments.Add(fragment);
            return m_liFragments.Count == m_nTotalFragmentsCount;
        }

        public BbTcpPacket assemblePacket()
        {
            if (m_liFragments.Count != m_nTotalFragmentsCount)
            {
                ParseError = "Not all fragments present";
                return null;
            }

            BbTcpPacket_Fragment[] aFragments = new BbTcpPacket_Fragment[m_nTotalFragmentsCount];
            int nCompletedPacketSize = 0;
            foreach (BbTcpPacket_Fragment fragment in m_liFragments)
            {
                if (fragment.m_nFragmentNumber >= aFragments.Length)
                {
                    ParseError = "Invalid fragment numbers present";
                    return null;
                }
                aFragments[fragment.m_nFragmentNumber] = fragment;
                nCompletedPacketSize += (int)fragment.m_bbData.Remaining();
            }
            m_liFragments.Clear();

            using (BinaryWriter buf = new BinaryWriter(new MemoryStream(nCompletedPacketSize)))
            {
                for (int i = 0; i < m_nTotalFragmentsCount; i++)
                {
                    if (aFragments[i] == null)
                    {
                        ParseError = "Invalid fragment numbers present / fragment missing";
                        return null;
                    }
                    buf.Write(aFragments[i].m_bbData.GetBuffer(), (int)aFragments[i].m_bbData.Position, (int)aFragments[i].m_bbData.Remaining());
                }

                BbTcpPacket res = new BbTcpPacket(m_byPayloadOpcode, nCompletedPacketSize);
                return res.ReadPacket(((MemoryStream)buf.BaseStream).GetBuffer(), m_byProtocolVersion);
            }
        }
    }
}
