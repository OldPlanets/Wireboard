using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Wireboard.Converters
{
    class StringToFontFamily : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            String strFont = (String)value;
            if (String.IsNullOrEmpty(strFont))
                return Application.Current.MainWindow != null ? Application.Current.MainWindow.FontFamily : new FontFamily("");
            else
                return new FontFamily(strFont); 
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (Application.Current.MainWindow == null)
                return "";
            FontFamily ff = (FontFamily)value;
            if (ff.Equals(Application.Current.MainWindow.FontFamily))
                return "";
            else
                return ff.ToString();
        }
    }
}