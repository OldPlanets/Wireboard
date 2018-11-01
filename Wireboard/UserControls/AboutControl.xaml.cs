using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// Interaction logic for AboutControl.xaml
    /// </summary>
    public partial class AboutControl : UserControl
    {
        public string VersionString => Assembly.GetEntryAssembly().GetName().Version.Major.ToString() + "." + Assembly.GetEntryAssembly().GetName().Version.Minor.ToString();
        public string BuildDateString
        {
            get
            {
                Version version = Assembly.GetEntryAssembly().GetName().Version;
                return (new DateTime(2000, 1, 1).Add(new TimeSpan(TimeSpan.TicksPerDay * version.Build + TimeSpan.TicksPerSecond * 2 * version.Revision))).ToString();
            }
        }

        public AboutControl()
        {            
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = e.Uri.RunHyperlink();
        }
    }
}
