using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Wireboard.Converters
{
    class IsDefaultServerToBool : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] == null)
                return false;
            DiscoveredServerInfo server = (DiscoveredServerInfo)values[0];
            int nDefaultServerGUID = (int)values[1];
            if (parameter != null)
            {
                if (parameter is String s)
                {
                    if (s.Equals("Invert"))
                        return server.ServerGUID != nDefaultServerGUID;
                }
            }
            return server.ServerGUID == nDefaultServerGUID;

        }

        public object[] ConvertBack(object values, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}