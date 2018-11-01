using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Wireboard.Converters
{
    class IsConnectedServerToVisibility : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] == null)
                return false;
            DiscoveredServerInfo server = (DiscoveredServerInfo)values[0];
            BbServerConnection connection = (BbServerConnection)values[1];
            if (connection.CurrentServer != null && connection.CurrentServer.Attached && connection.CurrentServer.ServerIP.Equals(server.ConnectionIP)
                && connection.CurrentServer.Port == server.Port && connection.CurrentServer.ServerGUID == server.ServerGUID)
            {
                return Visibility.Visible;
            }
            else
            {
                return Visibility.Collapsed;
            }
        }

        public object[] ConvertBack(object values, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}