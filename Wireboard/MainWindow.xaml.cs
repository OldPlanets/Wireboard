using Wireboard.BbEventArgs;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using MahApps.Metro.Controls.Dialogs;
using System.Windows.Data;
using System.Threading.Tasks;
using System.Windows.Threading;
using Wireboard.UserControls;
using SimpleRtspPlayer.RawFramesDecoding.DecodedFrames;

namespace Wireboard
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        protected static String TAG = typeof(MainWindow).Name;
        public event PropertyChangedEventHandler PropertyChanged;

        public BbServerConnection ServerConnection { get; set; }
        public AppIconManager m_appIconManager = new AppIconManager();
        public BbPasswordManager PasswordManager { get; set; } = new BbPasswordManager(DialogCoordinator.Instance);
        public BbSharedClipboard SharedClipboard { get; } = new BbSharedClipboard();

        private int m_nInputEventIDCounter = 0;
        private int m_nCurrentImeOptions = -1;
        private String m_strCurrentlyActiveApp = "";
        private bool m_bUpdateWarningShown = false;
        private bool m_bListenToTextChanges = true;
        private bool ListenToTextChanges
        {
            set
            {
                if (value && !m_bListenToTextChanges)
                {
                    textRemoteField.TextChanged += TextRemoteField_TextChanged;
                }
                else if (!value)
                {
                    textRemoteField.TextChanged -= TextRemoteField_TextChanged;
                }
                m_bListenToTextChanges = value;
            }
        }


        public AppIconManager AppIconManager => m_appIconManager;
        public BbBatteryStatus BatteryStatus { get; private set; } = new BbBatteryStatus();
        public AdbWrapper Adb { get; } = new AdbWrapper();
        public IScreenCapture ScreenCapture { get; private set; }

        public MainWindow()
        {
            Loaded += WinMain_Loaded;
            ServerConnection = new BbServerConnection(this);
            InitScreenCapture();

            InitializeComponent();

            Log.StatusChanged += OnStatusChanged;
            Log.LogAdded += OnLogAdded;
            Log.i(TAG, "Ready", true);

            AppIconManager.PropertyChanged += OnAppSelectionChanged;
            ServerConnection.PropertyChanged += OnPropertyChanged;
            ServerConnection.SoftwareUpdateRequired += OnSoftwareUpdateNeeded;
            Properties.Settings.Default.PropertyChanged += OnSettingsChanged;
            SharedClipboard.ClipboardChanged += OnClipboardChanged;
            ServerConnection.ScreenCapture.ScreenCaptureCancelled += OnScreenCaptureCancelled;
            
            SetActionButton(0);
        }

        private void InitScreenCapture()
        {
            if (Properties.Settings.Default.ScreenCapMethod == "2")
            {
                ScreenCapture = Adb;
                Log.d(TAG, "Setting ADB for screen capture");
            }
            else
            {
                ScreenCapture = ServerConnection.ScreenCapture;
                Log.d(TAG, "Setting android native for screen capture");
            }
        }

        private async void WinMain_Loaded(object sender, EventArgs e)
        {
            Loaded -= WinMain_Loaded;
            await PasswordManager.LoadPasswords();
            if (Properties.Settings.Default.ConnectOnStart)
            {
                await Task.Delay(500);
                await Connect();
            }
        }

        private void WinMain_Closed(object sender, EventArgs e)
        {
            Properties.Settings.Default.Save();
            ServerConnection.DisconnectAsync().Wait(1000);
        }

        public void OnInputFeedbackEvent(object sender, InputFeedbackEventArgs e)
        {
            ListenToTextChanges = false;
            if (e.InputEvent == InputFeedbackEventArgs.EInputEvent.FOCUSCHANGE)
            {
                m_strCurrentlyActiveApp = e.PackageName;     
                AppIconManager.PropertyChanged -= OnAppSelectionChanged;
                SetActionButton(e.ImeOptions);
                if ((e.InputType & (BBProtocol.TYPE_MASK_CLASS | BBProtocol.TYPE_MASK_VARIATION)) != 0)
                {
                    textRemoteField.IsEnabled = true;
                    textRemoteField.Text = e.Text;
                    textRemoteField.CaretIndex = e.CursorPos;
                    textRemoteField.Focus();
                    TextBoxHelper.SetWatermark(textRemoteField, e.Hint);
                    if (!AppIconManager.AddCurrentlyActiveApp(e.PackageName, e.FieldID))
                    {
                        Log.d(TAG, "Requestion Icon for " + e.PackageName);
                        ServerConnection.CurrentServer?.SendIconRequest(e.PackageName);
                    }
                }
                else
                {
                    textRemoteField.Text = "";
                    TextBoxHelper.SetWatermark(textRemoteField, "");
                    textRemoteField.IsEnabled = false;
                }                
                AppIconManager.PropertyChanged += OnAppSelectionChanged;
            }
            else if (e.InputEvent == InputFeedbackEventArgs.EInputEvent.TEXTUPDATE)
            {
                if (!textRemoteField.Text.Equals(e.Text, StringComparison.Ordinal))
                {
                    Log.i(TAG, "Text seems out of sync with device, correcting");
                    textRemoteField.Text = e.Text;
                    textRemoteField.CaretIndex = e.CursorPos;

                    // Notify the user the text was changed unexpectedly and interrupt his typing for him to adjust
                    // (border will flash red)
                    textRemoteField.IsReadOnly = true;
                    DispatcherTimer timer = new DispatcherTimer
                    {
                        Interval = new TimeSpan(0, 0, 1)
                    };
                    timer.Tick += (se, ev) =>
                    {
                        timer.Stop();
                        textRemoteField.IsReadOnly = false;
                    };
                    timer.Start();
                }
            }
            ListenToTextChanges = true;
        }

        private void OnStatusChanged(object sender, Log.LogEventArgs e)
        {
            lblStatusText.Text = e.Message;
        }

        private void OnLogAdded(object sender, Log.LogEventArgs e)
        {
            if (e.Severity == Log.ESeverity.STATUS)
                return;
            TextRange tr = new TextRange(rtbDebugLog.Document.ContentEnd, rtbDebugLog.Document.ContentEnd);
            tr.Text = e.Message + "\n";
            if (e.ShowInStatusbar)
            {
                tr.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);

                TextRange tr2 = new TextRange(rtbLog.Document.ContentEnd, rtbLog.Document.ContentEnd);
                tr2.Text = e.Message + "\n";
                tr2.ColorLogText(e.Severity);
                rtbLog.ScrollToEnd();
            }
            tr.ColorLogText(e.Severity);
            rtbDebugLog.ScrollToEnd();
        }

        private void SetActionButton(int nImeOptions)
        {
            if (nImeOptions != m_nCurrentImeOptions)
            {
                m_nCurrentImeOptions = nImeOptions;
                if (m_nCurrentImeOptions == BBProtocol.IME_NULL)
                {
                    btnAction.IsEnabled = false;
                }
                else
                {
                    bool bEnabled = true;
                    switch (nImeOptions & BBProtocol.IME_MASK_ACTION)
                    {
                        case BBProtocol.IME_ACTION_SEND:
                            vbActionIconBrush.Visual = FindResource("appbar_message_send") as Canvas;
                            tblActionText.Text = "Send";
                            break;
                        case BBProtocol.IME_ACTION_SEARCH:
                            vbActionIconBrush.Visual = FindResource("appbar_magnify") as Canvas;
                            tblActionText.Text = "Search";
                            break;
                        case BBProtocol.IME_ACTION_NEXT:
                            vbActionIconBrush.Visual = FindResource("appbar_navigate_next") as Canvas;
                            tblActionText.Text = "Next";
                            break;
                        case BBProtocol.IME_ACTION_PREVIOUS:
                            vbActionIconBrush.Visual = FindResource("appbar_navigate_previous") as Canvas;
                            tblActionText.Text = "Previous";
                            break;
                        case BBProtocol.IME_ACTION_UNSPECIFIED:
                        case BBProtocol.IME_ACTION_GO:
                            vbActionIconBrush.Visual = FindResource("appbar_check") as Canvas;
                            tblActionText.Text = "Go";
                            break;
                        case BBProtocol.IME_ACTION_DONE:
                            vbActionIconBrush.Visual = FindResource("appbar_check") as Canvas;
                            tblActionText.Text = "Done";
                            break;
                        default:
                            bEnabled = false;
                            break;
                    }
                    btnAction.IsEnabled = bEnabled;
                }
            }
        }

        private void TextRemoteFied_PreviewKeyDown(object sender, KeyEventArgs e)// PreviewKeydown + KeyDown handler
        {
            if (!ServerConnection.ConnectedToServer)
                return;

            bool bSend = false;

            // keys we want to forward as special keys
            switch (e.Key)
            {
                case Key.Tab:
                case Key.Escape:
                    bSend = true;
                    e.Handled = true;
                    break;
                case Key.Left:
                case Key.Right:
                case Key.Up:
                case Key.Down:
                    bSend = true;
                    e.Handled = (sender != textRemoteField);
                    break;
                case Key.Enter:
                    bSend = true;
                    // if the enter key will cause an action by the remote field rather than a new line, don't forward it to our editview
                    if (sender != textRemoteField || (e.Key == Key.Enter && (m_nCurrentImeOptions & BBProtocol.IME_FLAG_NO_ENTER_ACTION) != BBProtocol.IME_FLAG_NO_ENTER_ACTION
                            && (m_nCurrentImeOptions & BBProtocol.IME_MASK_ACTION) != BBProtocol.IME_ACTION_NONE))
                    {
                        e.Handled = true;
                    }
                    else
                        e.Handled = (sender != textRemoteField);
                    break;
                case Key.Space:
                    bSend = (sender != textRemoteField);
                    e.Handled = (sender != textRemoteField);
                    break;
                default:
                    bSend = false;
                    e.Handled = false;
                    break;
            }
            if (bSend)
            {
                int nModifier = 0;
                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
                    nModifier += 1;
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    nModifier += 2;
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                    nModifier += 4;

                int nCursorPos = (sender == textRemoteField ? textRemoteField.CaretIndex : -1);
                ServerConnection.CurrentServer.SendSpecialKey(e.Key, nModifier, nCursorPos, ++m_nInputEventIDCounter);

                if (sender == textRemoteField && !e.Handled)
                {
                    // we don't want the TextChanged event which is caused by this keystroke (if any) because
                    // we sent this keystroke already. The event will be posted (queued) after returning / was
                    // post already / will be invoked depending on which key was hit :/ The double post adds it back late enough for all cases.
                    // Better solution?
                    ListenToTextChanges = false;
                    textRemoteField.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        textRemoteField.Dispatcher.BeginInvoke((Action)(() =>
                        {
                            ListenToTextChanges = true;
                        }));
                    }));
                }
            }
        }

        private void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            ServerConnection.CurrentServer?.SendDoImeAction();
        }

        private void ToggleButtonScreenLock_Checked(object sender, RoutedEventArgs e)
        {
            ServerConnection.CurrentServer?.SendSetDisplayLock(true, Properties.Settings.Default.ScreenlockBright);
        }

        private void ToggleButtonScreenLock_Unchecked(object sender, RoutedEventArgs e)
        {
            ServerConnection.CurrentServer?.SendSetDisplayLock(false, Properties.Settings.Default.ScreenlockBright);
        }

        private void TextRemoteField_TextChanged(object sender, TextChangedEventArgs e)
        {
            //if (server == null)
            //    return;
            int iCount = 0;
            foreach (TextChange change in e.Changes)
            {
                iCount++;
                if (change.RemovedLength > 0)
                {
                    ServerConnection.CurrentServer.SendTextRemove((uint)change.Offset, (uint)change.RemovedLength, ++m_nInputEventIDCounter);
                    //Log.d(TAG, "Removed " + change.RemovedLength + "chars at Offset " + change.Offset);
                }
                if (change.AddedLength > 0)
                {
                    String textAdded;
                    try
                    {
                        textAdded = textRemoteField.Text.Substring(change.Offset, change.AddedLength);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        Log.e(TAG, "TextChanged out of range! Ignored");
                        return;
                    }
                    int iTextAfter = (e.Changes.Count > iCount) ? -1 : (textRemoteField.Text.Length - change.Offset);
                    ServerConnection.CurrentServer.SendTextInput(textAdded, (uint)change.Offset, iTextAfter, ++m_nInputEventIDCounter);
                    //Log.d(TAG, "Text: '" + textAdded + "' added at Offset " + change.Offset);
                }
            }
        }

        private void OnAppSelectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Selected")
            {
                String strName = AppIconManager.RecentApps[AppIconManager.Selected]?.PackageName;
                if (strName != null && !strName.Equals(m_strCurrentlyActiveApp, StringComparison.OrdinalIgnoreCase))
                {
                    ServerConnection.CurrentServer?.SendSwitchApp(strName, AppIconManager.RecentApps[AppIconManager.Selected].FieldID);
                    Log.d(TAG, "Requested app switch to " + strName + " Field: " + AppIconManager.RecentApps[AppIconManager.Selected].FieldID);
                }
            }
        }

        public void OnBatteryStatusChanged(object sender, BatteryStatusEventArgs e)
        {
            BatteryStatus = e.BatteryStatus;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("BatteryStatus"));
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender == ServerConnection)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(e.PropertyName));
            if (e.PropertyName == "ConnectedToServer")
            {
                btnConnect.IsChecked = ServerConnection.ConnectedToServer;

                if (!ServerConnection.ConnectedToServer)
                {
                    textRemoteField.Text = "";
                    TextBoxHelper.SetWatermark(textRemoteField, "");
                    textRemoteField.IsEnabled = false;
                    BatteryStatus.Reset();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("BatteryStatus"));

                }
            }
        }

        public void OnSharedText(object sender, ShareTextEventArgs e)
        {
            if (String.IsNullOrWhiteSpace(e.Text))
                return;
            SharedClipboard.HandleReceivedText(e.Text, e.TextHtml, e.IsFromClipboard, this);
        }


        public async void OnConnectionEvent(object sender, ConnectionEventArgs e)
        {
            if (e.NewState == ConnectionEventArgs.EState.DISCONNECTED)
            {
                await Adb.StopScreenCappingAsync();
                ScreenCapAdjustLayout(false);
            }
        }

        public async void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ScreenCapMethod")
            {                
                ScreenCapAdjustLayout(false);
                IScreenCapture old = ScreenCapture;
                InitScreenCapture();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ScreenCapture"));
                await old.StopScreenCappingAsync();
            }
            else if (e.PropertyName == "ScreenlockBright" && Properties.Settings.Default.PrefInitialDisplayLock)
            {
                ServerConnection.CurrentServer?.SendSetDisplayLock(Properties.Settings.Default.PrefInitialDisplayLock, Properties.Settings.Default.ScreenlockBright);
            }
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await Connect();
        }

        private async Task Connect()
        {
            btnConnect.IsEnabled = false;
            btnConnect.IsChecked = ServerConnection.ConnectedToServer;
            if (!ServerConnection.ConnectedToServer)
            {
                await ServerConnection.ConnectAsync();
            }
            else
            {
                await ServerConnection.DisconnectAsync();
            }
            btnConnect.IsEnabled = true;
            btnConnect.IsChecked = ServerConnection.ConnectedToServer;
        }

        private void ButtonBack_Click(object sender, RoutedEventArgs e)
        {
            ServerConnection.CurrentServer?.SendSpecialKey(Key.BrowserBack, 0, -1, ++m_nInputEventIDCounter);
        }

        private void ButtonHome_Click(object sender, RoutedEventArgs e)
        {
            ServerConnection.CurrentServer?.SendSpecialKey(Key.BrowserHome, 0, -1, ++m_nInputEventIDCounter);
        }

        private void ButtonOpenFile_Click(object sender, RoutedEventArgs e)
        {
            ServerConnection.ReceiveFilesManager.OpenFinished(false);
        }

        private void ButtonOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            ServerConnection.ReceiveFilesManager.OpenFinished(true);
        }

        private void StatusBarItem_Log_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenLog();
        }

        public void OpenLog()
        {
            LogFlyout.IsOpen = true;
            OptionsFlyout.IsOpen = false;
        }

        private void StatusBarItem_Connect_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            BtnConnect_Click(sender, e);
        }

        private void WinMain_Drop(object sender, DragEventArgs e)
        {
            /*if (e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                if (e.Data.GetData(DataFormats.UnicodeText) is String strText && Uri.TryCreate(strText.Trim(), UriKind.Absolute, out Uri uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    
                    e.Handled = true;
                }
            }
            else*/ if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                ServerConnection.SendFilesManager.SendFiles((String[])e.Data.GetData(DataFormats.FileDrop), ServerConnection.CurrentServer);
                e.Handled = true;
            }
        }

        private void WinMain_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            /*if (e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                if (e.Data.GetData(DataFormats.UnicodeText) is String strText && Uri.TryCreate(strText.Trim(), UriKind.Absolute, out Uri uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                {
                    e.Effects = DragDropEffects.Copy;
                    return;
                }
            }
            else*/ if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                return;
            }
            e.Effects = DragDropEffects.None;

        }

        private void TextRemoteField_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
                e.Handled = false;

        }

        private void Button_Upload_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                CheckFileExists = true,
                Filter = "All Files (*.*)|*.*|Image (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|Video (*.mp4;*.mpg;*.avi;*.mkv;*.wmv)|*.mp4;*.mpg;*.avi;*.mkv;*.wmv|Audio (*.mp3;*.wav;*.flac)|*.mp3;*.wav;*.flac",
                Multiselect = true,
                ShowReadOnly = false,
                Title = "Upload File To Phone",
                DereferenceLinks = true
            };
            if (dialog.ShowDialog() == true)
            {
                ServerConnection.SendFilesManager.SendFiles(dialog.FileNames, ServerConnection.CurrentServer);
            }
        }

        private void Button_Options_Click(object sender, RoutedEventArgs e)
        {
            OptionsFlyout.IsOpen = !OptionsFlyout.IsOpen;
            if (OptionsFlyout.IsOpen)
            {
                LogFlyout.IsOpen = false;
                receivedFileFlyout.IsOpen = false;
            }
        }

        private void WinMain_Activated(object sender, EventArgs e)
        {
            Adb.IsScreenCapPaused = false;
            ServerConnection.CheckAlive();
        }

        private void WinMain_Deactivated(object sender, EventArgs e)
        {
            Adb.IsScreenCapPaused = true;
        }

        public void ScreenCapAdjustLayout(bool bEnabled)
        {
            if (bEnabled && gridSplitScreenCap.Visibility == Visibility.Collapsed)
            {
                double fChatHeight;
                Properties.Settings.Default.ScreenCapOffWinHeight = Height;
                Properties.Settings.Default.ScreenCapOffWinWidth = Width;
                if (Properties.Settings.Default.ScreenCapResize && Properties.Settings.Default.ScreenCapOnWinHeight > 0
                    && Properties.Settings.Default.ScreenCapOnWinWidth > 0)
                {
                    Height = Properties.Settings.Default.ScreenCapOnWinHeight;
                    Width = Properties.Settings.Default.ScreenCapOnWinWidth;
                    fChatHeight = Math.Max(Properties.Settings.Default.ScreenCapOnChatHeight, gridScreenCap.RowDefinitions[2].MinHeight);
                }
                else
                    fChatHeight = Math.Max(Properties.Settings.Default.LayoutScreenCapChatHeight, gridScreenCap.RowDefinitions[2].MinHeight);

                MinHeight = 500;
                gridSplitScreenCap.Visibility = Visibility.Visible;
                gridScreenCap.RowDefinitions[0].MinHeight = 100;                
                double fPictureHeight = gridScreenCap.ActualHeight - (fChatHeight + gridScreenCap.RowDefinitions[1].MinHeight);
                if (fPictureHeight < gridScreenCap.RowDefinitions[0].MinHeight)
                {
                    fChatHeight -= gridScreenCap.RowDefinitions[0].MinHeight - fPictureHeight;
                    fPictureHeight = gridScreenCap.RowDefinitions[0].MinHeight;
                }
                gridScreenCap.RowDefinitions[0].Height = new GridLength(fPictureHeight, GridUnitType.Star);
                gridScreenCap.RowDefinitions[2].Height = new GridLength(fChatHeight, GridUnitType.Star);
            }
            else if (!bEnabled && gridSplitScreenCap.Visibility == Visibility.Visible)
            {
                Properties.Settings.Default.ScreenCapOnWinHeight = Height;
                Properties.Settings.Default.ScreenCapOnWinWidth = Width;
                Properties.Settings.Default.ScreenCapOnChatHeight = gridScreenCap.RowDefinitions[2].Height.Value;
                Properties.Settings.Default.LayoutScreenCapChatHeight = gridScreenCap.RowDefinitions[2].ActualHeight;
                MinHeight = 250;
                gridSplitScreenCap.Visibility = Visibility.Collapsed;
                gridScreenCap.RowDefinitions[0].MinHeight = 0;
                gridScreenCap.RowDefinitions[0].Height = new GridLength(0, GridUnitType.Pixel);
                gridScreenCap.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);

                if (Properties.Settings.Default.ScreenCapResize && Properties.Settings.Default.ScreenCapOffWinHeight > 0
                    && Properties.Settings.Default.ScreenCapOffWinWidth > 0)
                {
                    Height = Properties.Settings.Default.ScreenCapOffWinHeight;
                    Width = Properties.Settings.Default.ScreenCapOffWinWidth;
                }
            }
        }

        private async void ScreenCapButton_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement f = (FrameworkElement)sender;
            Binding binding = BindingOperations.GetBinding(f, IsEnabledProperty);
            f.IsEnabled = false;

            if (ScreenCapture.IsScreenCapActive)
            {
                await ScreenCapture.StopScreenCappingAsync();
                ScreenCapAdjustLayout(false);
            }
            else
            {
                Log.i(TAG, "Trying to start screen capture", true);
                if (await ScreenCapture.StartScreenCappingAsync())
                {                    
                    ScreenCapAdjustLayout(true);
                }
            }
            f.SetBinding(IsEnabledProperty, binding);
        }

        public void OnScreenCaptureCancelled(object sender, EventArgs e)
        {
            if (sender == ScreenCapture && !ScreenCapture.IsScreenCapActive)
            {
                ScreenCapAdjustLayout(false);
            }
        }

        private void GridScreenCap_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Fixme(?) This seems to be a bug in the GridSplitter. When drag-shrinking the window and reaching the lower row minheight
            // it will stretch the container beyond it's parents containers height and get bugged/stuck rather than properly shrinking the upper row more
            // So this hack will fix it for now.
            if (gridScreenCap.ActualHeight > gridMainContent.RowDefinitions[1].ActualHeight)
            {
                gridScreenCap.RowDefinitions[0].Height = new GridLength(Math.Max(gridScreenCap.RowDefinitions[0].Height.Value - (gridScreenCap.ActualHeight - gridMainContent.RowDefinitions[1].ActualHeight)
                    , gridScreenCap.RowDefinitions[0].MinHeight), GridUnitType.Star);
            }
        }

        private async void ScreenCapImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ScreenCapture == Adb && Adb.IsScreenCapActive && !Adb.IsScreenCapOutOfDate && Properties.Settings.Default.ScreenCapEnableTap && Adb.CurrentScreenCap != null)
            {
                Image scImage = sender as Image;
                if (scImage == null)
                    return;
                Point point = e.MouseDevice.GetPosition((FrameworkElement)sender);
                point.X *= Adb.CurrentScreenCap.Width / scImage.ActualWidth;
                point.Y *= Adb.CurrentScreenCap.Height / scImage.ActualHeight;
                e.Handled = true;
                await Adb.DoTapAsync(point);
            }
        }

        private async void ContextMenu_SaveScreenCap(object sender, RoutedEventArgs e)
        {
            await ScreenCapture.SaveScreenCapAsync();
        }

        private async void OnSoftwareUpdateNeeded(object sender, EventArgs e)
        {
            if (m_bUpdateWarningShown)
                return;
            m_bUpdateWarningShown = true;

            var mySettings = new MetroDialogSettings()
            {
                AffirmativeButtonText = "Visit Website",
                NegativeButtonText = "Close",
                ColorScheme = MetroDialogColorScheme.Accented
            };

            MessageDialogResult result = await this.ShowMessageAsync("Update Required", "The " + (string)Application.Current.FindResource("AppName")
                +  " app on your phone/tablet requires a newer version of this client to establish a connection. Please update this software.",
                MessageDialogStyle.AffirmativeAndNegative, mySettings);

            if (result == MessageDialogResult.Affirmative)
            {
                new Uri((string)Application.Current.FindResource("AppWebsite")).RunHyperlink();
            }
        }

        private void OnClipboardChanged(object sender, ClipboardChangedEventArgs e)
        {
            if (!String.IsNullOrEmpty(e.TextPlain))
            {
                ServerConnection.CurrentServer?.SendClipboardContent(e.TextPlain, e.TextHtml);
            }
            else if (e.ModeChange)
            {
                ServerConnection.CurrentServer?.SendShareRemoteClipboard(BbSharedClipboard.IsSharedRemote);
            }
        }

        private void EmojiPickerX_EmojiChosen(object sender, EventArgs e)
        {
            String strEmoji = ((EmojiPickerX)sender).Selection;
            if (textRemoteField.IsEnabled && !textRemoteField.IsReadOnly && strEmoji.Length > 0)
            {
                textRemoteField.SelectedText = strEmoji;
                textRemoteField.CaretIndex += strEmoji.Length;
                textRemoteField.SelectionLength = 0;
            }
        }
    }
}
