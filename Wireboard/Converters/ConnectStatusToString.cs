using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Wireboard.Converters
{
    class ConnectStatusToString : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(values[0] is bool && values[1] is bool && values[2] is bool && values[3] is bool))
                return "";
            bool bConnected = (bool)values[0];
            bool bConnecting = (bool)values[1];
            bool bNeverConnected = (bool)values[2];
            bool bDiscovering = (bool)values[3];

            if (bConnected)
                return "Connected";
            else if (bConnecting)
                return "Connecting";
            else if (bDiscovering)
                return "Discovering";
            else if (bNeverConnected)
                return "Not Connected";
            else
                return "Disconnected";

        }

        public object[] ConvertBack(object values, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}