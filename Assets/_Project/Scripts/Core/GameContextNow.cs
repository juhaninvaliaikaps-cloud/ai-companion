using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-160 / JUH-163: the short "what is happening in the game right now" note.
    /// The game companion writes it; chat and live commentary read it. It is state, not a
    /// memory, and it is cleared when the game closes or the feature is turned off.
    /// </summary>
    public static class GameContextNow
    {
        public const int MaxLength = 2200;

        static string _game = string.Empty;
        static string _observation = string.Empty;
        static string _patch = string.Empty;

        public static string Game
        {
            get { return _game; }
        }

        public static bool Active
        {
            get { return !string.IsNullOrEmpty(_game); }
        }

        public static void SetGame(string game)
        {
            var value = (game ?? string.Empty).Trim();
            if (string.Equals(value, _game, System.StringComparison.Ordinal))
                return;
            _game = value;
            if (_game.Length == 0)
            {
                _observation = string.Empty;
                _patch = string.Empty;
            }
        }

        public static void SetObservation(string observation)
        {
            _observation = Trim(observation);
        }

        public static void SetPatchContext(string context)
        {
            _patch = Trim(context);
        }

        public static void Clear()
        {
            _game = string.Empty;
            _observation = string.Empty;
            _patch = string.Empty;
        }

        /// <summary>Append the game note to a chat prompt when a game is active.</summary>
        public static string Append(string prompt)
        {
            if (!Active)
                return prompt ?? string.Empty;

            var sb = new StringBuilder((prompt ?? string.Empty).TrimEnd());
            sb.Append("\n\nGame you are watching: ").Append(_game).Append('.');
            if (!string.IsNullOrEmpty(_observation))
                sb.Append("\nLast screen observation: ").Append(_observation);
            if (!string.IsNullOrEmpty(_patch))
                sb.Append("\nRecent public patch/meta context (background only; do not invent beyond it):\n").Append(_patch);
            return sb.ToString();
        }

        static string Trim(string text)
        {
            var value = (text ?? string.Empty).Trim();
            if (value.Length > MaxLength)
                value = value.Substring(0, MaxLength).TrimEnd();
            return value;
        }
    }
}
