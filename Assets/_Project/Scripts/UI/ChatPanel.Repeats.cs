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

        readonly List<string> _voiceReplies = new List<string>(RepeatedPhrases.RepliesLooked);

        /// <summary>
        /// JUH-173: two of her own lines for this moment, mid-prompt, rotating with the number of
        /// her replies so far. A line she echoed lately is skipped. Only a typed or spoken message
        /// counts a showing (<paramref name="count"/>), which is what retires a line.
        /// </summary>
        string WithVoiceLines(string prompt, bool count)
        {
            if (_conversation == null || VoiceNow.Pool.Count == 0)
                return prompt;
            _voiceReplies.Clear();
            var turn = 0;
            var lines = _conversation.Lines;
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (line.Kind != ChatLineKind.Assistant || string.IsNullOrEmpty(line.Text))
                    continue;
                turn++;
                // Only used to tell whether she echoed a line; none of it goes into the prompt.
                if (_voiceReplies.Count < RepeatedPhrases.RepliesLooked)
                    _voiceReplies.Insert(0, line.Text);
            }

            return VoiceNow.AppendLines(prompt, turn, _voiceReplies, count);
        }

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
            // The voice reading's sample lines: a copied run is named at once.
            RepeatedPhrases.AddCopied(_recentReplies, VoiceNow.Samples, phrases);
            if (phrases.Count == 0)
                return prompt;
            // Counts only: the phrases come from her own replies and stay out of Player.log.
            Debug.Log("Repeats: asked her to vary " + phrases.Count + " phrase(s)");
            return (prompt ?? string.Empty).TrimEnd() + "\n\n" + RepeatedPhrases.Line(phrases);
        }
    }
}
