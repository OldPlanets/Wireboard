//
//  Emoji.Wpf — Emoji support for WPF
//
//  Copyright © 2017—2019 Sam Hocevar <sam@hocevar.net>
//
//  This library is free software. It comes without any warranty, to
//  the extent permitted by applicable law. You can redistribute it
//  and/or modify it under the terms of the Do What the Fuck You Want
//  to Public License, Version 2, as published by the WTFPL Task Force.
//  See http://www.wtfpl.net/ for more details.
//

using Emoji.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Wireboard.Emoji
{
    public static class EmojiData
    {
        private const int MAX_EMOJI_PER_GROUP = 104;
        private const int MAX_EMOJIGROUPS = 7;

        public static EmojiTypeface Typeface { get; private set; }

        public static IEnumerable<Emoji> AllEmoji
        {
            get
            {
                foreach (var group in AllGroups)
                    foreach (var emoji in group.EmojiList)
                        yield return emoji;
            }
        }

        public static ObservableCollection<Group> AllGroups { get; private set; }

        public static IDictionary<string, Emoji> Lookup { get; private set; }

        public static Regex MatchOne { get; private set; }
        public static Regex MatchMultiple { get; private set; }

        static EmojiData()
        {
            Typeface = new EmojiTypeface();
            ParseEmojiList();
        }

        public class Emoji
        {
            public string Name { get; set; }
            public string Text { get; set; }
            public Group Group { get; set; }

            public ObservableCollection<Emoji> VariationList { get; } = new ObservableCollection<Emoji>();
        }

        public class Group
        {
            public string Name { get; set; }
            private String m_strIcon;
            public string Icon
            {
                get
                {
                    return m_strIcon ?? EmojiList[0].Text;
                }
                set
                {
                    m_strIcon = value;
                }
            }

            public int EmojiCount => EmojiList.Count;
            public ObservableCollection<Emoji> EmojiList { get; } = new ObservableCollection<Emoji>();
        }

        private static void ParseEmojiList()
        {
            var modifiers_list = new string[] { "🏻", "🏼", "🏽", "🏾", "🏿" };
            var modifiers_string = "(" + string.Join("|", modifiers_list) + ")";

            var match_group = new Regex(@"^# group: (.*)");
            var match_subgroup = new Regex(@"^# subgroup: (.*)");
            var match_sequence = new Regex(@"^([0-9a-fA-F ]+[0-9a-fA-F]).*; (fully-|minimally-|un)qualified.*# [^ ]* (.*)");
            var match_modifier = new Regex(modifiers_string);
            var list = new ObservableCollection<Group>();
            var lookup = new Dictionary<string, Emoji>();
            var alltext = new List<string>();

            Group last_group = null;
            Emoji last_emoji = null;

            foreach (var line in EmojiDescriptionLines())
            {
                var m = match_group.Match(line);
                if (m.Success)
                {
                    if (list.Count >= MAX_EMOJIGROUPS)
                        break;
                    last_group = new Group() { Name = m.Groups[1].ToString() };
                    list.Add(last_group);
                    continue;
                }
                if (last_group != null && last_group.EmojiCount >= MAX_EMOJI_PER_GROUP)
                {
                    continue;
                }

                m = match_subgroup.Match(line);
                if (m.Success)
                {
                    continue;
                }

                m = match_sequence.Match(line);
                if (m.Success)
                {
                    string sequence = m.Groups[1].ToString();
                    string name = m.Groups[3].ToString();

                    string text = "";
                    foreach (var item in sequence.Split(' '))
                    {
                        int codepoint = Convert.ToInt32(item, 16);
                        text += char.ConvertFromUtf32(codepoint);
                    }

                    // Only include emojis that we know how to render
                    if (!Typeface.CanRender(text))
                        continue;

                    bool has_modifier = false;
                    bool has_high_modifier = false;
                    var regex_text = match_modifier.Replace(text, (x) =>
                    {
                        has_modifier = true;
                        has_high_modifier |= x.Value != modifiers_list[0];
                        return modifiers_string;
                    });

                    if (!has_high_modifier)
                        alltext.Add(has_modifier ? regex_text : text);

                    // Only add fully-qualified characters to the groups, or we will
                    // end with a lot of dupes.
                    if (line.Contains("unqualified") || line.Contains("minimally-qualified"))
                    {
                        // Skip this if there is already a fully qualified version
                        if (lookup.ContainsKey(text + "\ufe0f"))
                            continue;
                        if (lookup.ContainsKey(text.Replace("\u20e3", "\ufe0f\u20e3")))
                            continue;
                    }

                    var emoji = new Emoji() { Name = name, Text = text, Group = last_group };
                    lookup[text] = emoji;
                    if (has_modifier)
                    {
                        // We assume this is a variation of the previous emoji
                        if (last_emoji.VariationList.Count == 0)
                            last_emoji.VariationList.Add(last_emoji);
                        last_emoji.VariationList.Add(emoji);
                    }
                    else
                    {
                        last_emoji = emoji;
                        last_group.EmojiList.Add(emoji);
                    }
                }
            }

            // Remove empty groups, for instance the Components
            for (int i = list.Count; --i > 0;)
                if (list[i].EmojiCount == 0)
                    list.RemoveAt(i);

            AllGroups = list;
            Lookup = lookup;

            // Build a regex that matches any Emoji
            var textarray = alltext.ToArray();
            Array.Sort(textarray, (a, b) => b.Length - a.Length);
            var regextext = "(" + string.Join("|", textarray).Replace("*", "[*]") + ")";
            MatchOne = new Regex(regextext);
            MatchMultiple = new Regex(regextext + "+");
        }

        private static IEnumerable<string> EmojiDescriptionLines()
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("emojis.txt"))
            using (StreamReader sr = new StreamReader(s))
            {
                foreach (var line in sr.ReadToEnd().Split('\r', '\n'))
                {
                    yield return line;
                }
            }
        }
    }
}
