using System;
using System.IO;
using System.Text;
using System.Threading;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.UI;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// A developer tool for tuning and checking the reading (JUH-118). Start the app with
    /// <c>-pisara-read &lt;folder&gt;</c>: every persona file (<c>*.json</c>, plain JSON as in
    /// Resources/Personas) in that folder is read by the LOCAL model with the real request and
    /// parser, and the result is written next to it as <c>&lt;id&gt;.sheet.json</c> (plain, so
    /// it can be pasted into a pull request) and <c>&lt;id&gt;.reply.txt</c> (the raw reply).
    /// The app then quits.
    /// <para>
    /// It never calls a cloud model, never touches the user's personas or sheets, and the
    /// normal keeper does not run. Optional: <c>-pisara-model &lt;tag&gt;</c> uses another local
    /// model, <c>-pisara-prompt &lt;file&gt;</c> tries another system prompt without a rebuild.
    /// </para>
    /// </summary>
    public sealed class PisaraReadingTool : MonoBehaviour
    {
        public const string Flag = "-pisara-read";
        public const string ModelFlag = "-pisara-model";
        public const string PromptFlag = "-pisara-prompt";

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

            // The local provider only: this tool never sends anything to a cloud model.
            var provider = new OpenAiCompatibleChatProvider(new OpenAiProviderConfig
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

            Debug.Log("Pisara tool: local model " + tag + (system == null ? string.Empty : ", prompt from file"));
            var files = Directory.GetFiles(folder, "*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var read = 0;
            for (var i = 0; i < files.Length; i++)
            {
                if (files[i].EndsWith(".sheet.json", StringComparison.OrdinalIgnoreCase))
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
                    true,
                    false,
                    PisaraKeywords.Temperament,
                    CancellationToken.None,
                    600f,
                    system);
                var seconds = (DateTime.UtcNow - started).TotalSeconds;
                var stem = Path.Combine(folder, persona.Id);
                File.WriteAllText(stem + ".sheet.json", result.Sheet.ToJson(), new UTF8Encoding(false));
                File.WriteAllText(stem + ".reply.txt", result.RawReply ?? string.Empty, new UTF8Encoding(false));
                var choice = PisaraReading.Choose(TemperamentModes.Auto, string.Empty, false, result.Sheet);
                Debug.Log(PisaraReading.LogLine(persona.Id, result.Sheet)
                    + (result.Failure.Length == 0 ? string.Empty : " (" + result.Failure + ")")
                    + " in " + seconds.ToString("0") + " s");
                Debug.Log("Pisara tool: " + persona.Id + " | " + choice.Line + " | " + PisaraReading.RelationshipLine(result.Sheet));
                read++;
            }

            return read > 0 ? 0 : 3;
        }
    }
}
