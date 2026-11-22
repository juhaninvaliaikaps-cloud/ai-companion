using System.Collections.Generic;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.UI
{
    /// <summary>
    /// JUH-171: when her last replies keep reusing the same phrases, the next request asks her to
    /// vary them (<see cref="RepeatedPhrases"/>). Built once per send, never per frame. A reply
    /// that stays on this machine (an 18+ line) is looked at only when this send is private, so
    /// none of its words can reach a cloud request.
    /// </summary>
    public sealed partial class ChatPanel
    {
        readonly List<string> _recentReplies = new List<string>(RepeatedPhrases.RepliesLooked);

        string WithRepeats(string prompt, bool privateMode)
        {
            if (_conversation == null)
                return prompt;
            _recentReplies.Clear();
            var lines = _conversation.Lines;
            for (var i = lines.Count - 1; i >= 0 && _recentReplies.Count < RepeatedPhrases.RepliesLooked; i--)
            {
                var line = lines[i];
                if (line.Kind != ChatLineKind.Assistant || string.IsNullOrEmpty(line.Text))
                    continue;
                if (line.LocalOnly && !privateMode)
                    continue;
                _recentReplies.Insert(0, line.Text);
            }

            var phrases = RepeatedPhrases.Find(_recentReplies);
            if (phrases.Count == 0)
                return prompt;
            // Counts only: the phrases come from her own replies and stay out of Player.log.
            Debug.Log("Repeats: asked her to vary " + phrases.Count + " phrase(s)");
            return (prompt ?? string.Empty).TrimEnd() + "\n\n" + RepeatedPhrases.Line(phrases);
        }
    }
}
