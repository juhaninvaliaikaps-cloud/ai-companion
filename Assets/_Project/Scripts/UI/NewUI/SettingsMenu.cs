using System.Collections.Generic;

namespace AICompanion.NewUI
{
    /// <summary>JUH-182: the eight top tabs of Settings, in order.</summary>
    public enum SettingsTab
    {
        Her = 0,
        Ai = 1,
        Voice = 2,
        Look = 3,
        Chat = 4,
        Senses = 5,
        Adult = 6,
        App = 7,
    }

    /// <summary>JUH-182: one page of Settings: a sub-tab under a top tab.</summary>
    public enum SettingsPage
    {
        HerPersona,
        HerPersonality,
        HerTalk,
        HerLife,
        HerMemories,

        AiMode,
        AiChat,
        AiPrivate,
        AiPisara,
        AiPersona,

        VoiceSpeaking,
        VoiceEngine,
        VoicePersona,
        VoiceListening,
        VoiceGames,

        LookAvatar,
        LookVideo,
        Look3d,
        LookWindow,

        ChatReplies,
        ChatText,
        ChatHistory,

        SensesScreen,
        SensesGames,
        SensesTools,

        AdultStatus,
        AdultHer,
        AdultSecrets,
        AdultTalk,

        AppKeys,
        AppData,
        AppDeveloper,
    }

    /// <summary>
    /// JUH-182 (Jussi, 2026-11-22): Settings like the Profile panel. One row of eight top tabs,
    /// a row of sub-tabs under it, and each topic in its own box. This is the table of tabs and
    /// pages and when each page shows; the view draws it. Every 18+ setting is on the 18+ tab
    /// (Jussi), and its pages other than Status show only while the 18+ gate is open, as before.
    /// The Developer page shows only with "Show developer settings". Pure, for tests.
    /// </summary>
    public static class SettingsMenu
    {
        public static readonly string[] TabLabels = { "Her", "AI", "Voice", "Look", "Chat", "Senses", "18+", "App" };

        /// <summary>The most sub-tabs any tab has: the view keeps this many buttons.</summary>
        public const int MaxPages = 5;

        public static int TabCount
        {
            get { return TabLabels.Length; }
        }

        static readonly SettingsPage[][] Pages =
        {
            new[] { SettingsPage.HerPersona, SettingsPage.HerPersonality, SettingsPage.HerTalk, SettingsPage.HerLife, SettingsPage.HerMemories },
            new[] { SettingsPage.AiMode, SettingsPage.AiChat, SettingsPage.AiPrivate, SettingsPage.AiPisara, SettingsPage.AiPersona },
            new[] { SettingsPage.VoiceSpeaking, SettingsPage.VoiceEngine, SettingsPage.VoicePersona, SettingsPage.VoiceListening, SettingsPage.VoiceGames },
            new[] { SettingsPage.LookAvatar, SettingsPage.LookVideo, SettingsPage.Look3d, SettingsPage.LookWindow },
            new[] { SettingsPage.ChatReplies, SettingsPage.ChatText, SettingsPage.ChatHistory },
            new[] { SettingsPage.SensesScreen, SettingsPage.SensesGames, SettingsPage.SensesTools },
            new[] { SettingsPage.AdultStatus, SettingsPage.AdultHer, SettingsPage.AdultSecrets, SettingsPage.AdultTalk },
            new[] { SettingsPage.AppKeys, SettingsPage.AppData, SettingsPage.AppDeveloper },
        };

        public static string Label(SettingsPage page)
        {
            switch (page)
            {
                case SettingsPage.HerPersona: return "Persona";
                case SettingsPage.HerPersonality: return "Personality";
                case SettingsPage.HerTalk: return "How she talks";
                case SettingsPage.HerLife: return "Her life";
                case SettingsPage.HerMemories: return "Memories";
                case SettingsPage.AiMode: return "Mode";
                case SettingsPage.AiChat: return "Chat model";
                case SettingsPage.AiPrivate: return "Private model";
                case SettingsPage.AiPisara: return "Pisara";
                case SettingsPage.AiPersona: return "This persona";
                case SettingsPage.VoiceSpeaking: return "Speaking";
                case SettingsPage.VoiceEngine: return "Engine";
                case SettingsPage.VoicePersona: return "This persona";
                case SettingsPage.VoiceListening: return "Listening";
                case SettingsPage.VoiceGames: return "In games";
                case SettingsPage.LookAvatar: return "Avatar";
                case SettingsPage.LookVideo: return "2D video";
                case SettingsPage.Look3d: return "3D model";
                case SettingsPage.LookWindow: return "Window";
                case SettingsPage.ChatReplies: return "Replies";
                case SettingsPage.ChatText: return "Text and box";
                case SettingsPage.ChatHistory: return "History";
                case SettingsPage.SensesScreen: return "Screen";
                case SettingsPage.SensesGames: return "Games";
                case SettingsPage.SensesTools: return "Tools";
                case SettingsPage.AdultStatus: return "Status";
                case SettingsPage.AdultHer: return "Her";
                case SettingsPage.AdultSecrets: return "Secrets";
                case SettingsPage.AdultTalk: return "How she talks";
                case SettingsPage.AppKeys: return "Keys";
                case SettingsPage.AppData: return "Data";
                case SettingsPage.AppDeveloper: return "Developer";
                default: return page.ToString();
            }
        }

        public static SettingsTab TabOf(SettingsPage page)
        {
            for (var t = 0; t < Pages.Length; t++)
            {
                for (var i = 0; i < Pages[t].Length; i++)
                {
                    if (Pages[t][i] == page)
                        return (SettingsTab)t;
                }
            }

            return SettingsTab.Her;
        }

        /// <summary>Whether a page shows now: the 18+ pages but Status need the open gate, Developer its switch.</summary>
        public static bool Shows(SettingsPage page, bool adultOpen, bool developer)
        {
            switch (page)
            {
                case SettingsPage.AdultHer:
                case SettingsPage.AdultSecrets:
                case SettingsPage.AdultTalk:
                    return adultOpen;
                case SettingsPage.AppDeveloper:
                    return developer;
                default:
                    return true;
            }
        }

        /// <summary>The pages a tab shows now, in order. Never empty.</summary>
        public static List<SettingsPage> Visible(SettingsTab tab, bool adultOpen, bool developer)
        {
            var all = Pages[Index(tab)];
            var shown = new List<SettingsPage>(all.Length);
            for (var i = 0; i < all.Length; i++)
            {
                if (Shows(all[i], adultOpen, developer))
                    shown.Add(all[i]);
            }

            return shown;
        }

        /// <summary>The tab's first page.</summary>
        public static SettingsPage First(SettingsTab tab)
        {
            return Pages[Index(tab)][0];
        }

        /// <summary>
        /// The page to open on a tab: the one it was left on when it belongs to the tab and still
        /// shows, otherwise the tab's first page.
        /// </summary>
        public static SettingsPage Resolve(SettingsTab tab, SettingsPage remembered, bool adultOpen, bool developer)
        {
            if (TabOf(remembered) == tab && Shows(remembered, adultOpen, developer))
                return remembered;
            return First(tab);
        }

        static int Index(SettingsTab tab)
        {
            var i = (int)tab;
            return i < 0 || i >= Pages.Length ? 0 : i;
        }
    }
}

namespace AICompanion.NewUI
{
    /// <summary>JUH-182: long help notes show their first sentence, with "more" for the rest.</summary>
    public static class SettingsNotes
    {
        /// <summary>The shortest a first sentence may be before it counts as the short form.</summary>
        public const int MinSentence = 20;

        /// <summary>The first sentence of <paramref name="text"/>, or all of it when there is no sentence break.</summary>
        public static string Short(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            for (var i = MinSentence; i < text.Length - 1; i++)
            {
                var c = text[i];
                if ((c == '.' || c == '!' || c == '?') && text[i + 1] == ' ')
                {
                    // Not inside a path, a version or "e.g.": the next word must start a sentence.
                    var next = i + 2 < text.Length ? text[i + 2] : ' ';
                    if (char.IsUpper(next) || char.IsDigit(next) || next == '"' || next == '(')
                        return text.Substring(0, i + 1);
                }
            }

            return text;
        }
    }
}
