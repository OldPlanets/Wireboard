using Wireboard.Native;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.IO;
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

namespace Wireboard.UserControls
{
    /// <summary>
    /// Interaction logic for GeneralSettingsControl.xaml
    /// </summary>
    public partial class GeneralSettingsControl : UserControl
    {
        public GeneralSettingsControl()
        {
            InitializeComponent();
        }

        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog
            {
                BrowseShares = false,
                Title = "Choose a folder to store files received from your Android device",
                ShowStatusText = false,
                BrowseFiles = false,
            };
           
            if (dialog.ShowDialog() == true)
                Properties.Settings.Default.PrefDownloadDir = dialog.SelectedPath;


        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            (sender as TextBox)?.ShowTruncatedTextAsToolTip();
        }

        private async void ButtonClearPws_Click(object sender, RoutedEventArgs e)
        {
            await BbPasswordManager.ClearPasswords();
        }

        private void ButtonOpenLog_Click(object sender, RoutedEventArgs e)
        {
            (Application.Current.MainWindow as MainWindow)?.OpenLog();
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue as bool? == true)
            {
                if (String.IsNullOrWhiteSpace(Properties.Settings.Default.PrefDownloadDir))
                    TextBoxHelper.SetWatermark(tbDirectory, ReceiveFilesManager.GetDefaultDownloadDirectory(false));
            }
        }

        private void TbDirectory_SourceUpdated(object sender, DataTransferEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (String.IsNullOrWhiteSpace(Properties.Settings.Default.PrefDownloadDir))
                    TextBoxHelper.SetWatermark(textBox, ReceiveFilesManager.GetDefaultDownloadDirectory(false));
                else
                    TextBoxHelper.SetWatermark(textBox, null);
            }
        }
    }
}
