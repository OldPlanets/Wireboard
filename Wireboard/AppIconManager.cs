using Wireboard.BbEventArgs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Wireboard
{
    public class AppIconManager : INotifyPropertyChanged
    {
        protected static String TAG = typeof(AppIconManager).Name;
        private const int MAX_RECENTAPPS = 5;

        public class AppItem : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            public readonly String m_strPackageName;
            public String PackageName => m_strPackageName;
            private BitmapImage m_icon;
            public BitmapImage Icon
            {
                get { return m_icon; }
                set { m_icon = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Icon")); }
            }
            public bool DefaultItem { get; set; } = false;
            public int FieldID { get; set; }

            public AppItem(String strPackageName, BitmapImage icon, int nFieldID)
            {
                m_strPackageName = strPackageName;
                m_icon = icon;
                FieldID = nFieldID;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private List<AppItem> m_liCachedEntries = new List<AppItem>();
        public ObservableCollection<AppItem> RecentApps { get; } = new ObservableCollection<AppItem>();
        private readonly BitmapImage m_iconDefault;
        private int m_nCurrentSessionID = 0;
        private int m_nSelected = -1;
        public int Selected
        {
            get { return m_nSelected; }
            set { m_nSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Selected")); }
        }

        public AppIconManager()
        {
            m_iconDefault = new BitmapImage(new Uri("pack://application:,,,/Resources/android_default.png"));
            AppItem defItem = new AppItem("", m_iconDefault, -1);
            defItem.DefaultItem = true;
            RecentApps.Add(defItem);
        }


        public bool AddCurrentlyActiveApp(String strPackageName, int nFieldID)
        {
            // it's already the currently active app
            if (RecentApps[Selected].PackageName.Equals(strPackageName, StringComparison.OrdinalIgnoreCase))
            {
                RecentApps[Selected].FieldID = nFieldID;
                return true;
            }

            int nOldSelected = Selected;
            Selected = -1;

            if (RecentApps.Last().DefaultItem)
                RecentApps.Clear();

            for (int i = 0; i < nOldSelected; i++) // remove all forwards
                RecentApps.RemoveAt(0);

            for (int i = 0; i < RecentApps.Count; i++) // no duplicates in back history
            {
                if (RecentApps[i].PackageName.Equals(strPackageName, StringComparison.OrdinalIgnoreCase))
                {
                    RecentApps[i].FieldID = nFieldID;
                    RecentApps.Move(i, 0);
                    Selected = 0;
                    return true;
                }
            }

            AppItem inList = m_liCachedEntries.Find((x) => x.PackageName.Equals(strPackageName, StringComparison.OrdinalIgnoreCase));
            if (inList != null)
            {
                inList.FieldID = nFieldID;
                RecentApps.Insert(0, inList);
                Selected = 0;
                return true;
            }

            AppItem newItem = new AppItem(strPackageName, m_iconDefault, nFieldID);
            m_liCachedEntries.Add(newItem);
            RecentApps.Insert(0, newItem);
            Selected = 0;
            return false;
        }

        public void onReceivedIcon(object sender, ReceivedIconEventArgs eventArgs)
        {
            Log.d(TAG, "New Icon received, Thread: " + Thread.CurrentThread.ManagedThreadId);
            AppItem inList = m_liCachedEntries.Find((x) => x.PackageName.Equals(eventArgs.PackageName, StringComparison.OrdinalIgnoreCase));
            if (inList != null)
            {
                inList.Icon = eventArgs.Image;
            }
            else
            {
                Log.w(TAG, "Received icon for unknown packagename: " + eventArgs.PackageName);
            }
        }

        public void onConnectionEvent(object sender, ConnectionEventArgs e)
        {
            // connected to another device, clear old the old app history
            if (e.NewState == ConnectionEventArgs.EState.CONNECTED && m_nCurrentSessionID != 0 && m_nCurrentSessionID != e.SessionID)
            {
                Selected = -1;
                RecentApps.Clear();
                AppItem defItem = new AppItem("", m_iconDefault, -1);
                defItem.DefaultItem = true;
                RecentApps.Add(defItem);
                Selected = 0;
            }
        }
    }
}
