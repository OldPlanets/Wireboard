using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Interaction logic for InterfaceSettingsControl.xaml
    /// </summary>
    public partial class InterfaceSettingsControl : UserControl
    {
        public InterfaceSettingsControl()
        {
            InitializeComponent();
        }

        private void CheckBox_Topmost_Checked(object sender, RoutedEventArgs e)
        {
            // FIXME: Mainwindow Binding is not updating for some reason when the settings change.
            Application.Current.MainWindow.Topmost = Properties.Settings.Default.AlwaysOnTop;
        }
    }
}
