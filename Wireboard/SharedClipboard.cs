using Wireboard.BbEventArgs;
using Wireboard.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace Wireboard
{
    public class BbSharedClipboard
    {
        protected static String TAG = typeof(BbSharedClipboard).Name;
        public enum ClipboardSetting { Disabled = 0, Bidirectional, SharedRemote, SharedLocal };

        public event EventHandler<ClipboardChangedEventArgs> ClipboardChanged; 
        private DispatcherTimer m_clipboardChangeTimer;
        private String m_strLastClipboardText = null;
        public static bool IsSharedLocal => Settings.Default.SharedClipboard == (int) ClipboardSetting.Bidirectional || Settings.Default.SharedClipboard == (int)ClipboardSetting.SharedLocal;
        public static bool IsSharedRemote => Settings.Default.SharedClipboard == (int)ClipboardSetting.Bidirectional || Settings.Default.SharedClipboard == (int)ClipboardSetting.SharedRemote;

        public BbSharedClipboard()
        {
            if (Clipboard.ContainsText())
            {
                try
                {
                    m_strLastClipboardText = Clipboard.GetText();
                }
                catch (Exception err)
                {
                    Log.e(TAG, "Error while opening clipboard: " + err.Message);
                }
            }
            m_clipboardChangeTimer = new DispatcherTimer();
            m_clipboardChangeTimer.Tick += OnClipboardChangeTimer;
            m_clipboardChangeTimer.Interval = new TimeSpan(0, 0, 0, 0, 750);
            m_clipboardChangeTimer.Start();
            Settings.Default.PropertyChanged += OnPropertyChanged;
        }

        public void HandleReceivedText(String strPlainText, String strHtmlText, bool bFromClipboard, MainWindow mainWindow)
        {
            if (bFromClipboard && !IsSharedRemote)
            {
                Log.w(TAG, "Received remote clipboard content despite not having requested it");
                return;
            }

            Clipboard.Clear();
            if (Uri.TryCreate(strPlainText.Trim(), UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                Hyperlink hyperlink = new Hyperlink(new Run(strPlainText))
                {
                    NavigateUri = uriResult,
                    Foreground = mainWindow.lblStatusText.Foreground
                };
                hyperlink.RequestNavigate += OnHyperLinkClicked;
                mainWindow.lblStatusText.Inlines.Clear();
                mainWindow.lblStatusText.Inlines.Add(new Run("Received link: "));
                mainWindow.lblStatusText.Inlines.Add(hyperlink);
                
                Clipboard.SetText(strPlainText);
                m_strLastClipboardText = strPlainText;

                Hyperlink hyperlink2 = new Hyperlink(new Run(strPlainText))
                {
                    NavigateUri = uriResult,
                    Foreground = mainWindow.lblStatusText.Foreground,
                };
                hyperlink2.RequestNavigate += OnHyperLinkClicked;
                Paragraph para = new Paragraph
                {
                    Margin = new Thickness(0)
                };
                para.Inlines.Add(new Run("Received link: "));
                para.Inlines.Add(hyperlink2);
                Block last = mainWindow.rtbLog.Document.Blocks.LastBlock;
                mainWindow.rtbLog.Document.Blocks.Remove(last);
                mainWindow.rtbLog.Document.Blocks.Add(para);
                mainWindow.rtbLog.Document.Blocks.Add(last);
                mainWindow.rtbLog.ScrollToEnd();

            }
            else
            {
                m_strLastClipboardText = strPlainText;
                if (!String.IsNullOrWhiteSpace(strHtmlText))
                {
                    // Android is sending the plain HTML text, Windows expects a description / header for a html clipboard
                    // Additionally, the positions are based in bytecount rather than (unicode) characters, however since our header
                    // intro and outro are encoded with 1 byte, we only need to consider this for the actual html text

                    int nHeaderLen = "Version:0.9\n\rStartHTML:00000\n\rEndHTML:00000\n\rStartFragment:00000\n\rEndFragment:00000\n\r".Length;
                    String strHtmlIntro = @"<html><body><!--StartFragment-->";
                    String strHtmlOutro = @"<!--EndFragment--></body></html> ";
                    String strStartHtml = nHeaderLen.ToString().PadLeft(5, '0');
                    String strEndHtml = (nHeaderLen + Encoding.UTF8.GetByteCount(strHtmlText) + strHtmlIntro.Length + strHtmlOutro.Length).ToString().PadLeft(5, '0');
                    String strStartFragment = (nHeaderLen + strHtmlIntro.Length).ToString().PadLeft(5, '0');
                    String strEndFragment = (nHeaderLen + strHtmlIntro.Length + Encoding.UTF8.GetByteCount(strHtmlText)).ToString().PadLeft(5, '0');
                    String htmlFinalText = $"Version:0.9\n\rStartHTML:{strStartHtml}\n\rEndHTML:{strEndHtml}\n\rStartFragment:{strStartFragment}\n\rEndFragment:{strEndFragment}\n\r"
                        + strHtmlIntro + strHtmlText + strHtmlOutro;

                    // Clipboard class clears the clipboard with each set, so use dataobject instead
                    DataObject d = new DataObject();
                    d.SetText(htmlFinalText, TextDataFormat.Html);
                    d.SetText(strPlainText, TextDataFormat.UnicodeText);
                    Clipboard.SetDataObject(d);
                }
                else
                    Clipboard.SetText(strPlainText);

                Log.d(TAG, "Received text, copied to clipboard (Contains HTML: " + !String.IsNullOrWhiteSpace(strHtmlText) + ")");
                TextRange tr = new TextRange(mainWindow.rtbLog.Document.ContentEnd, mainWindow.rtbLog.Document.ContentEnd);
                tr.Text = "Received text: " + strPlainText + "\n";
                mainWindow.rtbLog.ScrollToEnd();
            }
        }

        private void OnHyperLinkClicked(object sender, RequestNavigateEventArgs args)
        {
            args.Handled = args.Uri.RunHyperlink();
        }

        private void OnClipboardChangeTimer(Object source, EventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                String curText;
                try
                {
                    curText = Clipboard.GetText();
                }
                catch (Exception err)
                {
                    Log.e(TAG, "Error while opening clipboard: " + err.Message);
                    return;
                }
                if (IsSharedLocal && m_strLastClipboardText != null && !curText.Equals(m_strLastClipboardText, StringComparison.Ordinal))
                {
                    String curTextHtml = null;
                    if (Clipboard.ContainsText(TextDataFormat.Html))
                    {
                        // Android wants the pure html text, peel away everything else (see above for the header)
                        String strClipHtml = Clipboard.GetText(TextDataFormat.Html);
                        int nStartFragmentDesc = strClipHtml.ToLower().IndexOf("startfragment:") + "startfragment:".Length;
                        int nEndFragmentDesc = strClipHtml.ToLower().IndexOf("endfragment:") + "endfragment:".Length;
                        if (nStartFragmentDesc > 0 && nStartFragmentDesc < strClipHtml.Length && nEndFragmentDesc > 0 && nEndFragmentDesc < strClipHtml.Length)
                        {
                            int nStartFragmentIdx;
                            int nEndFragmentIdx;
                            if (int.TryParse(new string(strClipHtml.Substring(nStartFragmentDesc).Trim().TakeWhile(c => char.IsDigit(c)).ToArray()), out nStartFragmentIdx)
                                && int.TryParse(new string(strClipHtml.Substring(nEndFragmentDesc).Trim().TakeWhile(c => char.IsDigit(c)).ToArray()), out nEndFragmentIdx))
                            {
                                if (nStartFragmentIdx < nEndFragmentIdx && nEndFragmentIdx <= Encoding.UTF8.GetByteCount(strClipHtml))
                                {
                                    // Positions are ByteCount rather than CharCount
                                    try
                                    {
                                        curTextHtml = System.Text.Encoding.UTF8.GetString(System.Text.Encoding.UTF8.GetBytes(strClipHtml), nStartFragmentIdx, nEndFragmentIdx - nStartFragmentIdx);
                                    }
                                    catch (Exception err) when (err is ArgumentException || err is DecoderFallbackException)
                                    {
                                        Log.e(TAG, "Error re-decoding string");
                                    }
                                }
                            }
                        }

                    }
                    Log.d(TAG, "Local Clipboard changed, propagating content, contains html: " + !String.IsNullOrEmpty(curTextHtml));
                    ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs(curText, curTextHtml));
                }
                m_strLastClipboardText = curText;
            }
            else
            {
                m_strLastClipboardText = null;
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SharedClipboard")
            {
                Log.d(TAG, "Clipboard mode changed");
                ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs(true));
            }
        }
    }
}
