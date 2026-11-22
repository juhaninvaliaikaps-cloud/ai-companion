using System.Text;
using AICompanion.Core;

namespace AICompanion.UI
{
    /// <summary>
    /// JUH-156: her lorebook. Entries whose keys are in the new message or the last few lines
    /// are added to the prompt (<see cref="LoreNow"/>, <see cref="LoreMatcher"/>). An 18+
    /// lorebook adds nothing while the gate is closed, and an open gate keeps chat local.
    /// </summary>
    public sealed partial class ChatPanel
    {
        readonly StringBuilder _loreScan = new StringBuilder(2048);

        string WithLore(string prompt, string text)
        {
            if (LoreNow.Book == null)
                return prompt;
            return LoreNow.Append(prompt, LoreScanText(text));
        }

        /// <summary>The new message, then up to <see cref="LoreMatcher.ScanLines"/> earlier user and companion lines.</summary>
        string LoreScanText(string text)
        {
            _loreScan.Length = 0;
            _loreScan.Append(text ?? string.Empty);
            if (_conversation == null)
                return _loreScan.ToString();
            var lines = _conversation.Lines;
            var taken = 0;
            // JUH-177: in public mode, private lines do not pick lore either.
            var includePrivate = _conversation.MarkLocalNow;
            for (var i = lines.Count - 1; i >= 0 && taken < LoreMatcher.ScanLines; i--)
            {
                var line = lines[i];
                if (line == null || line.Kind == ChatLineKind.System || string.IsNullOrEmpty(line.Text))
                    continue;
                if (line.LocalOnly && !includePrivate)
                    continue;
                _loreScan.Append('\n').Append(line.Text);
                taken++;
            }

            return _loreScan.ToString();
        }
    }
}
