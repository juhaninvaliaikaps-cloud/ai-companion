using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Memory;
using AICompanion.UI;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// A developer tool for tuning and checking the reading (JUH-118). Start the app with
    /// <c>-pisara-read &lt;folder&gt;</c>: every persona file (<c>*.json</c>, plain JSON as in
    /// Resources/Personas) in that folder is read by the configured model with the real request and
    /// parser, and the result is written next to it as <c>&lt;id&gt;.sheet.json</c> (plain, so
    /// it can be pasted into a pull request) and <c>&lt;id&gt;.reply.txt</c> (the raw reply).
    /// The app then quits.
    /// <para>
    /// When the Pisara cloud setting is on and Private mode is off, the sheet is read by that
    /// service (JUH-126); otherwise the local model is used. The normal keeper does not run.
    /// Optional: <c>-pisara-model &lt;tag&gt;</c> uses another local model,
    /// <c>-pisara-prompt &lt;file&gt;</c> tries another system prompt without a rebuild.
    /// </para>
    /// </summary>
    public sealed class PisaraReadingTool : MonoBehaviour
    {
        public const string Flag = "-pisara-read";
        public const string ModelFlag = "-pisara-model";
        public const string PromptFlag = "-pisara-prompt";

        /// <summary>
        /// With this flag each persona also gets <c>&lt;id&gt;.world.txt</c> (JUH-118 sections 2 and
        /// 3): the memories seeded from the sheet, her first reply (opening mid-scene), what
        /// happened while the user was away with her reply to it, and the session memories of
        /// that short talk. The memory file it uses is a throwaway one in the same folder.
        /// </summary>
        public const string WorldFlag = "-pisara-world";

        public static bool Has(string[] args, string flag)
        {
            if (args == null)
                return false;
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool Requested(string[] args)
        {
            return Value(args, Flag) != null;
        }

        /// <summary>The value after a flag, or null when the flag or its value is missing.</summary>
        public static string Value(string[] args, string flag)
        {
            if (args == null)
                return null;
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(args[i + 1]))
                    return args[i + 1];
            }

            return null;
        }

        async void Start()
        {
            var args = Environment.GetCommandLineArgs();
            var folder = Value(args, Flag);
            var code = 1;
            try
            {
                code = await Run(folder, Value(args, ModelFlag), Value(args, PromptFlag));
            }
            catch (Exception e)
            {
                Debug.LogError("Pisara tool: failed (" + e.GetType().Name + ": " + e.Message + ")");
            }

            Debug.Log("Pisara tool: done, exit " + code);
            Application.Quit(code);
        }

        async System.Threading.Tasks.Task<int> Run(string folder, string model, string promptFile)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Debug.LogError("Pisara tool: folder not found.");
                return 2;
            }

            var chat = GetComponent<ChatPanel>();
            var settings = chat != null ? chat.RuntimeChat : null;
            var baseUrl = settings != null && !string.IsNullOrWhiteSpace(settings.localBaseUrl) ? settings.localBaseUrl.Trim() : ChatDefaults.LocalBaseUrl;
            var tag = !string.IsNullOrWhiteSpace(model)
                ? model.Trim()
                : settings != null && !string.IsNullOrWhiteSpace(settings.localModel) ? settings.localModel.Trim() : ChatDefaults.LocalModel;
            var system = !string.IsNullOrEmpty(promptFile) && File.Exists(promptFile) ? File.ReadAllText(promptFile) : null;

            var companion = GetComponent<SettingsPanel>() != null ? GetComponent<SettingsPanel>().CurrentSettings : null;
            var keys = new PisaraApiKeySource();
            var ownKey = keys.TryGetOwn(out var ownValue) ? ownValue : null;
            var grokKey = keys.TryGetGrok(out var grokValue) ? grokValue : null;
            var pisaraChoice = PisaraModelChoice.For(
                companion,
                companion != null && companion.PrivateMode,
                IntimateExpressions.Active,
                ownKey != null,
                grokKey != null);
            var keepsLocal = !pisaraChoice.UseCloud;

            // The local provider is still used for the world and session-memory checks.
            var local = new OpenAiCompatibleChatProvider(new OpenAiProviderConfig
            {
                ProviderId = ChatDefaults.LocalId,
                DisplayName = "Local model",
                RequiresKey = false,
                BaseUrl = () => baseUrl,
                Model = () => tag,
                TimeoutSeconds = () => 600f,
                ApiKey = () => null,
                Transport = new UnityChatTransport(this),
            });
            var provider = pisaraChoice.UseCloud
                ? PisaraCloudProvider.Create(pisaraChoice.BaseUrl, pisaraChoice.Model,
                    () => pisaraChoice.KeySource == PisaraKeySource.Pisara ? ownKey
                        : pisaraChoice.KeySource == PisaraKeySource.Grok ? grokKey : null,
                    new UnityChatTransport(this))
                : local;

            Debug.Log("Pisara tool: " + (pisaraChoice.UseCloud ? "cloud model " + pisaraChoice.Model : "local model " + tag)
                + (system == null ? string.Empty : ", prompt from file"));
            var files = Directory.GetFiles(folder, "*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var read = 0;
            for (var i = 0; i < files.Length; i++)
            {
                if (files[i].EndsWith(".sheet.json", StringComparison.OrdinalIgnoreCase)
                    || files[i].EndsWith(".memory.json", StringComparison.OrdinalIgnoreCase))
                    continue;
                var persona = PersonaJson.Parse(File.ReadAllText(files[i]));
                if (persona == null)
                {
                    Debug.LogWarning("Pisara tool: not a persona file: " + Path.GetFileName(files[i]));
                    continue;
                }

                var prompt = PersonaText.Build(persona, persona.DisplayName, string.Empty);
                var started = DateTime.UtcNow;
                var result = await PisaraReader.ReadAsync(
                    provider,
                    persona.DisplayName,
                    prompt,
                    keepsLocal,
                    false,
                    PisaraKeywords.Temperament,
                    CancellationToken.None,
                    600f,
                    system);
                var seconds = (DateTime.UtcNow - started).TotalSeconds;
                if (result.FromModel)
                {
                    result.Sheet.readInCloud = pisaraChoice.UseCloud;
                    result.Sheet.readBy = (string.IsNullOrWhiteSpace(pisaraChoice.Model) ? "the chat model" : pisaraChoice.Model)
                        + (pisaraChoice.UseCloud ? " (cloud)" : " (chat)");
                }

                var stem = Path.Combine(folder, persona.Id);
                File.WriteAllText(stem + ".sheet.json", result.Sheet.ToJson(), new UTF8Encoding(false));
                File.WriteAllText(stem + ".reply.txt", result.RawReply ?? string.Empty, new UTF8Encoding(false));
                var choice = PisaraReading.Choose(TemperamentModes.Auto, string.Empty, false, result.Sheet);
                Debug.Log(PisaraReading.LogLine(persona.Id, result.Sheet)
                    + (result.Failure.Length == 0 ? string.Empty : " (" + result.Failure + ")")
                    + " in " + seconds.ToString("0") + " s");
                Debug.Log("Pisara tool: " + persona.Id + " | " + choice.Line + " | " + PisaraReading.RelationshipLine(result.Sheet));
                if (Has(Environment.GetCommandLineArgs(), WorldFlag) && result.FromModel)
                    await World(local, persona, prompt, result.Sheet, stem, WorldMemory.SeedKeepsLocal(result.Sheet));
                read++;
            }

            return read > 0 ? 0 : 3;
        }

        /// <summary>
        /// Her world for one persona, with the same calls the app makes: guard, seed, first
        /// scene, an away event, and session memories. The replies come from the local model
        /// through the memory layer; the cue instructions are left out so the text reads plainly.
        /// </summary>
        async System.Threading.Tasks.Task World(IChatProvider local, PersonaDefinition persona, string prompt, CharacterSheet sheet, string stem, bool seedKeepsLocal)
        {
            var text = new StringBuilder();
            var memoryPath = stem + ".memory.json";
            if (File.Exists(memoryPath))
                File.Delete(memoryPath);
            var store = MemoryStore.Load(memoryPath);
            var now = DateTime.UtcNow;

            var dropped = new List<string>();
            var safe = WorldGuard.ForSeeding(sheet, dropped);
            var added = WorldMemory.Seed(store, safe, now, seedKeepsLocal);
            text.Append("== Seeded memories (").Append(added).Append(") ==\n");
            var entries = store.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var when = WorldMemory.When(entry, now);
                text.Append("- ").Append(entry.isThread ? "[thread] " : entry.IsShortTerm ? "[short] " : "[long] ");
                if (when.Length > 0)
                    text.Append('(').Append(when).Append(") ");
                text.Append(entry.text);
                if (entry.isThread)
                    text.Append("  | good: ").Append(entry.goodOutcome).Append("  | bad: ").Append(entry.badOutcome);
                text.Append("\n");
            }

            text.Append("Left out by the shared-history guard (").Append(dropped.Count).Append("):\n");
            for (var i = 0; i < dropped.Count; i++)
                text.Append("- ").Append(dropped[i]).Append("\n");

            // The same memory layer the chat uses, on the throwaway store.
            var provider = new MemoryChatProvider(local, store);
            var scene = WorldGuard.FirstScene(sheet);
            var messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, "Hi.") };
            WorldPromptNow.Reset();
            WorldPromptNow.SetFirstScene(scene);
            var first = await Ask(provider, WorldPromptNow.Append(prompt, true), messages);
            text.Append("\n== First scene line ==\n").Append(scene.Length == 0 ? "(none)" : scene).Append("\n");
            text.Append("\n== First reply (user: \"Hi.\") ==\n").Append(first).Append("\n");
            messages.Add(new ChatMessage(ChatRole.Assistant, first));

            var roll = UnityEngine.Random.value;
            var away = WorldMemory.ResolveThread(store, 8d * 3600d, roll, now, true);
            text.Append("\n== While the user was away (8 hours, roll ").Append(roll.ToString("0.00")).Append(") ==\n");
            if (away.Happened)
            {
                text.Append("Away: ").Append(persona.Id).Append(" = ").Append(away.Text).Append(away.WentWell ? "  (went well)" : "  (went badly)").Append("\n");
                WorldPromptNow.SetAway(away.PromptLine);
                messages.Add(new ChatMessage(ChatRole.User, "I'm back. How have you been?"));
                var second = await Ask(provider, WorldPromptNow.Append(prompt, false), messages);
                text.Append("Reply (user: \"I'm back. How have you been?\"):\n").Append(second).Append("\n");
                messages.Add(new ChatMessage(ChatRole.Assistant, second));
            }
            else
            {
                text.Append("(nothing: no open thread)\n");
            }

            WorldPromptNow.Reset();
            var transcript = SessionMemories.BuildTranscript(messages, persona.DisplayName);
            var capture = await SessionMemories.CaptureAsync(
                local, transcript, persona.DisplayName, SessionMemories.MinUserMessages, true, false, store, 600f, CancellationToken.None, true);
            var memories = capture.Memories;
            text.Append("\n== Session memories of this short talk (").Append(memories.Count).Append(") ==\n");
            for (var i = 0; i < memories.Count; i++)
                text.Append("- ").Append(memories[i]).Append("\n");
            text.Append("New thread: ").Append(capture.Thread == null
                ? "(none)"
                : capture.Thread.text + "  | good: " + capture.Thread.goodOutcome + "  | bad: " + capture.Thread.badOutcome).Append("\n");

            File.WriteAllText(stem + ".world.txt", text.ToString(), new UTF8Encoding(false));
            Debug.Log("Pisara tool: " + persona.Id + " world: " + added + " seeded, " + dropped.Count + " left out, away "
                + (away.Happened ? (away.WentWell ? "went well" : "went badly") : "none") + ", " + memories.Count + " session memories, new thread "
                + (capture.Thread != null ? "yes" : "no"));
        }

        static async System.Threading.Tasks.Task<string> Ask(IChatProvider provider, string personaPrompt, List<ChatMessage> messages)
        {
            var request = new ChatRequest
            {
                SystemPrompt = personaPrompt + "\n\n" + IntimateBoundaries.PromptLine(false),
                Messages = new List<ChatMessage>(messages),
                Temperature = ChatDefaults.Temperature,
                MaxTokens = 200,
                BlockCloud = true,
            };
            var reply = await provider.StreamChat(request, _ => { }, CancellationToken.None);
            return reply.Ok ? reply.Text.Trim() : "(no reply: " + reply.Error + ")";
        }
    }
}
