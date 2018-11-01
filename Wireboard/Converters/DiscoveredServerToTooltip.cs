using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Wireboard.Converters
{
    class DiscoveredServerToTooltip : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            DiscoveredServerInfo server = (DiscoveredServerInfo)value;
            return server.ServerName + "\n" + server.IP.ToString() + " : " + server.Port.ToString() + "\n"
                + "Password required: " + (server.PasswordRequired ? "Yes" : "No")
                + "\n" + ((uint)server.ServerGUID).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}