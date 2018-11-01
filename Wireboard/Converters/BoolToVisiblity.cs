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
    class BoolToVisiblity : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool bValue = (Boolean)value;
            Visibility notVisible = Visibility.Collapsed;
            if (parameter != null)
            {
                if (parameter is String s)
                {
                    if (s.Equals("Invert"))
                        bValue = !bValue;
                    else if (s.Equals("Hidden"))
                        notVisible = Visibility.Hidden;
                }                
            }

            if ( bValue == false)
            {
                return notVisible;
            }
            else
                return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
