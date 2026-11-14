namespace AICompanion.Core
{
    /// <summary>
    /// Lines about her own life that the chat adds to the next request (JUH-118 sections 2 and
    /// 3, docs/PISARA.md). The persona side sets them; the chat panel reads them, because the
    /// chat assembly cannot see the settings assembly (the same hand-off as
    /// <see cref="CompanionMoodNow"/>).
    /// <list type="bullet">
    /// <item>The first scene: what she was doing when the user first talks to her. The chat
    /// adds it only to the first message of a conversation with no history.</item>
    /// <item>The away line: what happened to her while the user was gone. It is added to
    /// requests until one reply has finished, then dropped.</item>
    /// </list>
    /// Main thread only. With nothing set, <see cref="Append"/> returns the prompt unchanged.
    /// </summary>
    public static class WorldPromptNow
    {
        static string _firstScene = string.Empty;
        static string _away = string.Empty;
        static string _herName = string.Empty;

        /// <summary>The active persona's display name, for the session-memory request. Empty with the setup name.</summary>
        public static string HerName
        {
            get { return _herName; }
        }

        public static void SetHerName(string name)
        {
            _herName = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        }

        public static string FirstScene
        {
            get { return _firstScene; }
        }

        public static string Away
        {
            get { return _away; }
        }

        public static void SetFirstScene(string line)
        {
            _firstScene = string.IsNullOrWhiteSpace(line) ? string.Empty : line.Trim();
        }

        public static void SetAway(string line)
        {
            _away = string.IsNullOrWhiteSpace(line) ? string.Empty : line.Trim();
        }

        /// <summary>
        /// The persona prompt with the pending lines after it. The first scene is added only
        /// when <paramref name="firstMessage"/> is true.
        /// </summary>
        public static string Append(string personaPrompt, bool firstMessage)
        {
            var prompt = personaPrompt == null ? string.Empty : personaPrompt.TrimEnd();
            if (firstMessage && _firstScene.Length > 0)
                prompt = prompt.Length == 0 ? _firstScene : prompt + "\n\n" + _firstScene;
            if (_away.Length > 0)
                prompt = prompt.Length == 0 ? _away : prompt + "\n\n" + _away;
            return prompt;
        }

        /// <summary>A reply finished: the away line has been used once and is dropped.</summary>
        public static void ReplyFinished()
        {
            _away = string.Empty;
        }

        public static void Reset()
        {
            _firstScene = string.Empty;
            _away = string.Empty;
            _herName = string.Empty;
        }
    }
}
