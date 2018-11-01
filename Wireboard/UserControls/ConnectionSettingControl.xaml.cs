using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Wireboard.UserControls
{
    /// <summary>
    /// Interaction logic for ConnectionSettingControl.xaml
    /// </summary>
    public partial class ConnectionSettingControl : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ServerConnectionDependencyProperty = DependencyProperty.Register("ServerConnection", typeof(BbServerConnection)
            , typeof(ConnectionSettingControl), new PropertyMetadata(onServerConnectionPropertyChanged));
        public BbServerConnection ServerConnection
        {
            get { return GetValue(ServerConnectionDependencyProperty) as BbServerConnection; }
            set { SetValue(ServerConnectionDependencyProperty, value); }
        }
        public bool ConnectedToServer => ServerConnection != null ? ServerConnection.ConnectedToServer : false;
        public bool IsDiscovering => ServerConnection != null ? ServerConnection.IsDiscovering : false;
        public bool IsConnecting => ServerConnection != null ? ServerConnection.IsConnecting : false;
        public event PropertyChangedEventHandler PropertyChanged;
        public DiscoveredServerInfo SelectedServer { get; set; }
        public bool ShowHint { get; set; } = false;

        public ConnectionSettingControl()
        {
            InitializeComponent();
        }

        private async void Button_Refresh_Click(object sender, RoutedEventArgs e)
        {
            await ServerConnection.DiscoverAsync();
        }

        private static void onServerConnectionPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ConnectionSettingControl uc = sender as ConnectionSettingControl;
            if (uc == null)
                return;
            if (e.OldValue is BbServerConnection oldValue)
            {
                oldValue.PropertyChanged -= uc.onServerConnectionChanged;
            }
            if (e.NewValue is BbServerConnection newValue)
            {
                newValue.PropertyChanged += uc.onServerConnectionChanged;
            }
        }

        public void onServerConnectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("IsDiscovering"))
            {
                ShowHint = !IsDiscovering && ServerConnection.DiscoveryFinder.FoundServer.Count == 0;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ShowHint"));
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(e.PropertyName));
        }

        private async void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DiscoveredServerInfo server = ((FrameworkElement)e.OriginalSource).DataContext as DiscoveredServerInfo;
            if (server != null)
            {
                e.Handled = true;
                await ServerConnection.ConnectToServerAsync(server.ConnectionIP, server.Port, server.ServerGUID);                
            }
        }

        private async void Button_Connect_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedServer != null)
                await ServerConnection.ConnectToServerAsync(SelectedServer.ConnectionIP, SelectedServer.Port, SelectedServer.ServerGUID);
        }

        private async void ucConnectionSettings_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;
            if (e.NewValue is bool bNewValue && bNewValue)
            {
                await ServerConnection?.DiscoverAsync();
            }
        }

        private void MenuItem_SetDefault_Click(object sender, RoutedEventArgs e)
        {
            DiscoveredServerInfo server = ((FrameworkElement)e.OriginalSource).DataContext as DiscoveredServerInfo;
            if (server != null)
            {
                Properties.Settings.Default.DefaultServer = server.ServerGUID;
                Properties.Settings.Default.Save();
            }
        }

        private void MenuItem_ClearDefault_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.DefaultServer = 0;
            Properties.Settings.Default.Save();
        }
    }
}
