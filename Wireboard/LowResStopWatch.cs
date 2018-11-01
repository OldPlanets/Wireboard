using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wireboard
{
    public class LowResStopWatch
    {
        private int m_nStartTicks = 0;
        private int m_nStopTicks = 0;
        private int m_nCountDown = 0;

        public LowResStopWatch(bool bStart = true)
        {
            if (bStart)
                Start();
        }

        public void StartCountDown(int nMilliSec)
        {
            Start();
            m_nCountDown = nMilliSec;
        }

        public void Start()
        {
            m_nStartTicks = Environment.TickCount;
            m_nStopTicks = 0;
            m_nCountDown = 0;
        }

        public void Stop()
        {
            if (m_nStopTicks == 0)
                m_nStopTicks = Environment.TickCount;
        }

        public int ElapsedMilliseconds
        {
            get
            {
                if (m_nStartTicks == 0)
                {
                    Console.WriteLine("Warning: ElapsedMilliseconds requested on LowResStopWatch which was never started");
                    return 0;
                }
                else
                {
                    int nStop = ((m_nStopTicks != 0) ? m_nStopTicks : Environment.TickCount);
                    return nStop - m_nStartTicks;
                }
            }
        }

        public int RemainingMillisecondsToCountdown
        {
            get
            {
                if (m_nStartTicks == 0 || m_nCountDown == 0)
                {
                    Console.WriteLine("Warning: RemainingMillisecondsToCountdown without set countdown");
                    return 0;
                }
                else
                    return m_nCountDown - ElapsedMilliseconds;
            }
        }

        public int CountdownInterval => m_nCountDown;

        public bool IsRunning
        {
            get
            {
                return m_nStartTicks != 0 && m_nStopTicks == 0;
            }
        }

        public bool IsCountdownReached
        {
            get
            {
                if (m_nCountDown == 0)
                    return false;
                else
                    return ElapsedMilliseconds > m_nCountDown;
            }
        }
    }
}
