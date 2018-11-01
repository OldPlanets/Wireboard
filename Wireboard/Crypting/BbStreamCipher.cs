using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard.Crypting
{
    class BbStreamCipher
    {
        private VmpcKsa3Engine m_engine;
        public byte[] IV { get; private set; }
        public bool IsReady => m_engine != null;
        public String Algorithm => m_engine != null ?  m_engine.AlgorithmName : "";

        public BbStreamCipher Init(byte[] abyKey, byte[] abyIV = null)
        {
            if (abyIV != null)
                IV = abyIV;

            if (abyKey.Length < 32 || IV.Length < 16)
                throw new ArgumentException("Key or IV size too small");

            m_engine = new VmpcKsa3Engine();
            m_engine.Init(true, new ParametersWithIV(new KeyParameter(abyKey, 0, 32), IV));
            return this;
        }

        public void Crypt(byte[] data, int nOffset = 0, int nLength = 0)
        {
            if (nLength == 0)
                nLength = data.Length;
            if (m_engine != null)
                m_engine.ProcessBytes(data, nOffset, nLength, data, nOffset);
            else
                throw new Exception("Engine not Initalized");
        }

        public byte[] GenerateIV()
        {
            IV = new byte[16];
            SecureRandom rand = new SecureRandom();
            rand.NextBytes(IV);
            return IV;
        }
    }
}
