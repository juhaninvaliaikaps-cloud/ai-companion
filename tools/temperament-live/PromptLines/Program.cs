using System;
using System.IO;
using System.Text;
using System.Text.Json;
using AICompanion.Core;

// JUH-236: the real prompt pieces the chat gets for one slider setting, built by the production
// code so the paired runner never copies them by hand. For each <axis>:<step> it prints:
//   manner        PersonaManner.Line (only used before a voice reading exists)
//   conversation  PersonaManner.ConversationLine (every ordinary chat turn, after the voice block)
//   voice         VoicePrompt.Block for the synthetic voice sheet (the cached-voice path),
//                 with the 18+ gate closed, no mood line and a user she is still getting to know
// Every other axis sits at step 5 ("none" is that neutral profile).
// Usage: PromptLines --voice <voice.json> <axis>:<step> [<axis>:<step> ...]
static class Program
{
    static int Main(string[] args)
    {
        VoiceSheet voice = null;
        var name = "she";
        var sb = new StringBuilder("[");
        var first = true;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--voice" && i + 1 < args.Length)
            {
                voice = ReadVoice(args[++i], out name);
                continue;
            }

            var parts = args[i].Split(':');
            var axes = TemperamentAxes.Neutral();
            var neutral = TemperamentSteps.ValueForStep(5);
            for (var a = 0; a < TemperamentAxes.Count; a++)
                axes.Set(a, neutral);
            var axis = Array.FindIndex(TemperamentAxes.Names, n => string.Equals(n, parts[0], StringComparison.OrdinalIgnoreCase));
            var step = parts.Length > 1 ? int.Parse(parts[1]) : 5;
            if (axis >= 0)
                axes.Set(axis, TemperamentSteps.ValueForStep(step));
            var block = voice == null ? string.Empty
                : VoicePrompt.Block(name, voice, null, CompanionMood.Content, 0f, 20f, false, 50f, axes);

            if (!first)
                sb.Append(',');
            first = false;
            sb.Append("{\"key\":").Append(Quote(args[i]))
              .Append(",\"stored\":").Append(axis >= 0 ? TemperamentSteps.ValueForStep(step) : neutral)
              .Append(",\"manner\":").Append(Quote(PersonaManner.Line(axes)))
              .Append(",\"conversation\":").Append(Quote(PersonaManner.ConversationLine(axes)))
              .Append(",\"voice\":").Append(Quote(block))
              .Append('}');
        }

        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine(sb.Append(']').ToString());
        return 0;
    }

    static VoiceSheet ReadVoice(string path, out string name)
    {
        using (var doc = JsonDocument.Parse(File.ReadAllText(path)))
        {
            var root = doc.RootElement;
            // A fixtures file keeps the sheet under "voice"; a bare sheet works too.
            if (root.TryGetProperty("voice", out var nested) && nested.ValueKind == JsonValueKind.Object)
                root = nested;
            name = Text(root, "name", "she");
            return new VoiceSheet
            {
                summary = Text(root, "summary", string.Empty),
                register = Text(root, "register", string.Empty),
                rhythm = Text(root, "rhythm", string.Empty),
                vocabulary = Text(root, "vocabulary", string.Empty),
                habits = Items(root, "habits"),
                never = Items(root, "never"),
                stranger = Text(root, "stranger", string.Empty),
                close = Text(root, "close", string.Empty),
            };
        }
    }

    static string Text(JsonElement root, string key, string fallback)
    {
        return root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : fallback;
    }

    static string[] Items(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Array)
            return new string[0];
        var items = new string[value.GetArrayLength()];
        var i = 0;
        foreach (var item in value.EnumerateArray())
            items[i++] = item.GetString() ?? string.Empty;
        return items;
    }

    static string Quote(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in s ?? string.Empty)
        {
            if (c == '"' || c == '\\')
                sb.Append('\\').Append(c);
            else if (c == '\n')
                sb.Append("\\n");
            else if (c == '\r')
                sb.Append("\\r");
            else if (c < 0x20)
                sb.Append(' ');
            else
                sb.Append(c);
        }

        return sb.Append('"').ToString();
    }
}
