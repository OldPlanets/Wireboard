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
    class IntToFontsize : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int nFontSize = (int)value;
            if (nFontSize == 0)
                return Application.Current.MainWindow != null ? (int)Application.Current.MainWindow.FontSize : 0;
            else
                return nFontSize;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int nSettingsInt = (int)value;
            if (nSettingsInt == Application.Current.MainWindow.FontSize)
                return 0;
            else
                return nSettingsInt;
        }
    }
}