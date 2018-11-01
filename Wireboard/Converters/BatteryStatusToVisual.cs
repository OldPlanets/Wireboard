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
    class BatteryStatusToVisual : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            BbBatteryStatus status = (BbBatteryStatus)value;
            if (status.PluggedIn)
                return Application.Current.FindResource("appbar_battery_charging") as Visual;
            else
            {
                int nLevel = Math.Min(3, Math.Max(0, ((status.BatteryLevel - 1) / 25)));
                return Application.Current.FindResource("appbar_battery_" + nLevel) as Visual;
            }

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
