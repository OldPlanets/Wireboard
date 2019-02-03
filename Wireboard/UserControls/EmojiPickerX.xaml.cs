using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Wireboard.Emoji;

namespace Wireboard.UserControls
{
    partial class EmojiPickerX : StackPanel
    {

        private ToggleButton m_current_toggle;
        //public ObservableCollection<GroupX> EmojiGroups { get; } = new ObservableCollection<GroupX>();
        public ObservableCollection<EmojiData.Group> EmojiGroups => EmojiData.AllGroups;
        private EmojiData.Group m_recentGroup;
        public event EventHandler EmojiChosen;


        public string Selection { get; set; } = "";
       
        public EmojiPickerX()
        {
            ModifyEmojiList();
            InitializeComponent();
        }

        private void ModifyEmojiList()
        {
            // add our own recent group
            m_recentGroup = new EmojiData.Group
            {
                Name = "Recently used",
                Icon = "🕒"
            };
            EmojiGroups.Insert(0, m_recentGroup);
            // load
            foreach (String entry in Properties.Settings.Default.RecentEmojiList.Split('|'))
            {
                String[] aEmoji = entry.Split(':');
                if (aEmoji.Length == 2)
                {
                    m_recentGroup.EmojiList.Add(new EmojiData.Emoji() { Name = aEmoji[1], Text = aEmoji[0], Group = m_recentGroup });
                }
            }
        }

        public void AddRecentEmoji(String strEmoji, String strName)
        {
            if (strEmoji.Length == 0)
                return;

            var inList = m_recentGroup.EmojiList.SingleOrDefault(x => x.Text == strEmoji);
            if (inList != null)
            {
                m_recentGroup.EmojiList.Remove(inList);
                m_recentGroup.EmojiList.Insert(0, inList);
            }
            else
            {
                m_recentGroup.EmojiList.Insert(0, new EmojiData.Emoji() { Name = strName, Text = strEmoji, Group = m_recentGroup });
                if (m_recentGroup.EmojiCount > 22)
                {
                    m_recentGroup.EmojiList.RemoveAt(m_recentGroup.EmojiCount - 1);
                }
            }
            String strSave = "";
            foreach (EmojiData.Emoji e in m_recentGroup.EmojiList)
                strSave += e.Text + ":" + e.Name + "|";
            Properties.Settings.Default.RecentEmojiList = strSave;
        }

        private void OnEmojiSelected(object sender, RoutedEventArgs e)
        {
            if (m_current_toggle != null)
            {
                m_current_toggle.IsChecked = false;
                m_current_toggle.Focusable = false;
                m_current_toggle = null;
            }

            var emoji = (sender as Control).DataContext as EmojiData.Emoji;
            if (emoji.VariationList.Count == 0 || sender is Button)
            {
                Selection = emoji.Text;
                Button.IsChecked = false;
                if (Selection.Length > 0)
                {
                    AddRecentEmoji(Selection, emoji.Name);
                    EmojiChosen?.Invoke(this, EventArgs.Empty);
                }
                e.Handled = true;
            }

            if (sender is ToggleButton && emoji.VariationList.Count > 0)
            {
                m_current_toggle = sender as ToggleButton;
            }
        }

        private void Button_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // we use left click on the popup button as shortcut to paste the last used emoji
            if (Selection.Length > 0)
            {
                EmojiChosen?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Popup_Opened(object sender, EventArgs e)
        {
            // don't select the recent panel if we don't have any recent emojis
            if (tabControlGroups.SelectedIndex == 0 && EmojiGroups[0].EmojiCount == 0)
            {
                tabControlGroups.SelectedIndex = 1;
            }
        }
    }
}
