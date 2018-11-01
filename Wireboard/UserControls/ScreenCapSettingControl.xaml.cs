using Wireboard.Native;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Navigation;

namespace Wireboard.UserControls
{
    /// <summary>
    /// Interaction logic for ScreenCapSettingControl.xaml
    /// </summary>
    public partial class ScreenCapSettingControl : UserControl
    {
        public ScreenCapSettingControl()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = e.Uri.RunHyperlink();
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue as bool? == true)
            {
                tbResultText.Visibility = Visibility.Collapsed;
                rectFailIcon.Visibility = Visibility.Collapsed;
                rectOKIcon.Visibility = Visibility.Collapsed;
                if (!String.IsNullOrEmpty(AdbWrapper.GetAdbLocation(true)))
                    TextBoxHelper.SetWatermark(tbDirectory, AdbWrapper.GetAdbLocation(true));
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
                textBox.ShowTruncatedTextAsToolTip();
        }

        private async void ButtonTest_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement feSender = (FrameworkElement)sender;
            Binding binding = BindingOperations.GetBinding(feSender, IsEnabledProperty);
            feSender.IsEnabled = false;
            tbDirectory.IsReadOnly = true;
            tbResultText.Visibility = Visibility.Collapsed;
            rectFailIcon.Visibility = Visibility.Collapsed;
            rectOKIcon.Visibility = Visibility.Collapsed;
            progressRingTest.Visibility = Visibility.Visible;

            Tuple<bool, String> res = await CheckAdbAsync();

            UpdateTestStatus(res.Item2);
            if (res.Item1)
                rectOKIcon.Visibility = Visibility.Visible;
            else
                rectFailIcon.Visibility = Visibility.Visible;
            progressRingTest.Visibility = Visibility.Collapsed;
            tbDirectory.IsReadOnly = false;
            feSender.IsEnabled = true;
            feSender.SetBinding(IsEnabledProperty, binding);
        }

        private async Task<Tuple<bool, String>> CheckAdbAsync()
        {
            String strResult = "";
            String strAdbPath = AdbWrapper.GetAdbLocation(false);
            if (strAdbPath == null)
            {
                return new Tuple<bool, string>(false, "Adb.exe not found in specified directory");
            }
            else
            {
                Version adbVersion = await AdbWrapper.GetAdbVersionAsync(strAdbPath);
                if (adbVersion == null)
                {
                    return new Tuple<bool, string>(false, "Unable to communicate with Adb.exe or invalid version");
                }
                else if (adbVersion < new Version(1, 0, 39))
                {
                    strResult += "ADB version outdated. ";
                }
                else
                    strResult += "ADB version ok. ";
                UpdateTestStatus(strResult);

                int nDevices = await AdbWrapper.GetADBConnectedDevicesAsync(strAdbPath);
                if (nDevices == 0)
                {
                    return new Tuple<bool, string>(false, strResult + "No connected device found (Is debugging enabled?).");
                }
                else if (nDevices < 0)
                {
                    return new Tuple<bool, string>(false, strResult + "Error while getting connected devices.");
                }
                else if (nDevices > 1)
                {
                    return new Tuple<bool, string>(false, strResult + "Multiple devices found connected - please connect only one.");
                }
                strResult += "Device found. ";
                UpdateTestStatus(strResult);
                if (await AdbWrapper.GetADBScreenCapAsync(strAdbPath, default(CancellationToken), false) == null)
                {
                    if (await AdbWrapper.GetADBScreenCapAsync(strAdbPath, default(CancellationToken), true) == null)
                    {
                        return new Tuple<bool, string>(false, strResult + "Unable to get/decode screen capture.");
                    }
                    strResult += "Using fallback method. ";
                }
                return new Tuple<bool, string>(true, strResult + "Successfully received screen capture.");
            }
        }

        private void UpdateTestStatus(String strText)
        {
            tbResultText.Visibility = Visibility.Visible;
            tbResultText.Text = strText;
        }

        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog
            {
                BrowseShares = false,
                Title = "Choose the ADB installation directory, which contains adb.exe",
                ShowStatusText = false,
                BrowseFiles = false,
                HideNewFolderButton = true
            };
            
            if (dialog.ShowDialog() == true)
            {
                String strRes = dialog.SelectedPath;
                if (!File.Exists(strRes + Path.DirectorySeparatorChar + "adb.exe") &&
                    File.Exists(strRes + Path.DirectorySeparatorChar + "platform-tools" + Path.DirectorySeparatorChar + "adb.exe"))
                {
                    strRes = strRes + Path.DirectorySeparatorChar + "platform-tools";
                }
                Properties.Settings.Default.ADBPath = strRes;
            }

        }
    }
}
