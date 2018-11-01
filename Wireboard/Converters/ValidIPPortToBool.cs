using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Wireboard.Converters
{
    class ValidIPPortToBool : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            String strIP = (String)values[0];
            String strPort = (String)values[1];
            return (String.IsNullOrEmpty(strPort) || UInt16.TryParse(strPort, out UInt16 port)) && strIP.IsValidIP();
        }

        public object[] ConvertBack(object values, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}