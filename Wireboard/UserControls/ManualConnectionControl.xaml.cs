using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
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
    /// Interaction logic for ManualConnectionControl.xaml
    /// </summary>
    public partial class ManualConnectionControl : UserControl
    {
        public static readonly DependencyProperty ServerConnectionDependencyProperty = DependencyProperty.Register("ServerConnection", typeof(BbServerConnection)
            , typeof(ManualConnectionControl), new PropertyMetadata(onServerConnectionPropertyChanged));
        public BbServerConnection ServerConnection
        {
            get { return GetValue(ServerConnectionDependencyProperty) as BbServerConnection; }
            set { SetValue(ServerConnectionDependencyProperty, value); }
        }
        private bool m_bWaitingForConResult = false;
        private String m_strUsedIP;
        private String m_strUsedPort;

        public ManualConnectionControl()
        {
            InitializeComponent();
        }

        private static void onServerConnectionPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ManualConnectionControl uc = sender as ManualConnectionControl;
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

        private async void Button_Connect_Click(object sender, RoutedEventArgs e)
        {
            await TryConnectAsync();
        }

        public void onServerConnectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("IsConnecting") && m_bWaitingForConResult)
            {
                m_bWaitingForConResult = false;
                if (ServerConnection.ConnectedToServer)
                {
                    tbResultText.Visibility = Visibility.Visible;
                    tbResultText.Text = "Successfully connected to " + ServerConnection.CurrentServer.ServerName + ".";
                    rectFailIcon.Visibility = Visibility.Collapsed;
                    rectOKIcon.Visibility = Visibility.Visible;
                    Properties.Settings.Default.ManualServerIP = m_strUsedIP;
                    Properties.Settings.Default.ManualServerPort = m_strUsedPort;
                    Properties.Settings.Default.Save();
                }
                else
                {
                    tbResultText.Visibility = Visibility.Visible;
                    tbResultText.Text = "Remote device refused connection.";
                    rectFailIcon.Visibility = Visibility.Visible;
                    rectOKIcon.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ucManualConnection_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool bNewValue && bNewValue)
            {
                tbResultText.Visibility = Visibility.Collapsed;
                rectFailIcon.Visibility = Visibility.Collapsed;
                rectOKIcon.Visibility = Visibility.Collapsed;
                m_bWaitingForConResult = false;
                tbIP.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
                tbPort.GetBindingExpression(TextBox.TextProperty).UpdateTarget();
            }
        }

        private async void tbIP_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && buttonConnect.IsEnabled)
            {
                await TryConnectAsync();
            }
        }

        private async Task TryConnectAsync()
        {
            m_strUsedIP = tbIP.Text;
            m_strUsedPort = tbPort.Text;
            tbResultText.Visibility = Visibility.Collapsed;
            rectFailIcon.Visibility = Visibility.Collapsed;
            rectOKIcon.Visibility = Visibility.Collapsed;
            IPAddress ip;
            UInt16 nPort = BBProtocol.DEFAULT_TCP_PORT;
            if (IPAddress.TryParse(m_strUsedIP, out ip) && (String.IsNullOrEmpty(m_strUsedPort) || UInt16.TryParse(m_strUsedPort, out nPort)))
            {
                if (await ServerConnection.ConnectToServerAsync(ip, nPort))
                {
                    m_bWaitingForConResult = true;
                    return;
                }
            }
            tbResultText.Visibility = Visibility.Visible;
            tbResultText.Text = "Failed to connect to device. Make sure " + (String)Application.Current.FindResource("AppName") + " is set as active keyboard and that the IP and Port are correct.";
            rectFailIcon.Visibility = Visibility.Visible;
        }
    }
}
