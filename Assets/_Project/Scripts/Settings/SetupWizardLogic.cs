using System;
using AICompanion.AI;
using AICompanion.Core;

namespace AICompanion.Settings
{
    public static class RelationshipStyles
    {
        public const string Friend = "friend";
        public const string Girlfriend = "girlfriend";

        public static string Normalize(string value)
        {
            if (string.Equals(value, Girlfriend, StringComparison.OrdinalIgnoreCase))
                return Girlfriend;
            return Friend;
        }

        public static string Label(string value)
        {
            return Normalize(value) == Girlfriend ? "Girlfriend" : "Friend";
        }
    }

    /// <summary>
    /// Name and relationship become the chat system prompt. An empty name with
    /// the friend style keeps the original companion prompt, so a settings file
    /// from before this wizard does not change anyone's persona.
    /// </summary>
    public static class CompanionPersona
    {
        public const string DefaultName = "Leia";

        public static bool LooksLikeSecret(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            if (value.IndexOf("xai-", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (value.IndexOf("api_key", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (value.IndexOf("bearer ", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        public static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var trimmed = name.Trim().Replace("\r", string.Empty).Replace("\n", string.Empty);
            if (trimmed.Length > 40)
                trimmed = trimmed.Substring(0, 40);
            if (LooksLikeSecret(trimmed))
                return string.Empty;
            return trimmed;
        }

        public static string Build(string name, string relationship)
        {
            var style = RelationshipStyles.Normalize(relationship);
            var clean = SanitizeName(name);
            if (clean.Length == 0)
            {
                if (style == RelationshipStyles.Friend)
                    return ChatDefaults.SystemPrompt;
                clean = DefaultName;
            }

            if (style == RelationshipStyles.Girlfriend)
            {
                return "You are " + clean
                    + ", a warm desktop companion sitting beside the user. You are their girlfriend: affectionate and close, and still kind, light, and appropriate for any audience. Reply in one or two short, conversational sentences.";
            }

            return "You are " + clean
                + ", a warm, friendly desktop companion sitting beside the user. You are their friend. Reply in one or two short, conversational sentences. Stay kind, light, and appropriate for any audience.";
        }
    }

    /// <summary>
    /// First launch is the absence of both settings.json and a saved API key.
    /// Either one means an existing user. <see cref="ForceArgument"/> shows the
    /// wizard again without deleting those files.
    /// </summary>
    public static class SetupWizardGate
    {
        public const string ForceArgument = "--setup";

        public static bool CommandRequestsWizard(string[] args)
        {
            if (args == null || args.Length == 0)
                return false;

            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], ForceArgument, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool ShouldPresent(bool settingsFileExists, bool savedKeyExists, bool force)
        {
            if (force)
                return true;
            return !settingsFileExists && !savedKeyExists;
        }

        public static bool ShouldPresentOnLaunch(bool settingsFileExists, bool savedKeyExists, string[] commandLine)
        {
            return ShouldPresent(settingsFileExists, savedKeyExists, CommandRequestsWizard(commandLine));
        }

        public static bool HasSavedApiKey()
        {
            try
            {
                string ignored;
                var found = ApiKeyFile.TryReadKey(ApiKeyFile.PreferredPath(), SecretProtector.Create(), out ignored);
                ignored = null;
                return found;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool EnvironmentHasApiKey()
        {
            var value = Environment.GetEnvironmentVariable(ApiKeyResolver.EnvName);
            var present = !string.IsNullOrWhiteSpace(value);
            value = null;
            return present;
        }
    }

    public static class SetupWizardFlow
    {
        public const int Welcome = 0;
        public const int Ai = 1;
        public const int Avatar = 2;
        public const int Personality = 3;
        public const int Tour = 4;
        public const int StepCount = 5;
        public const int Done = 5;

        public static bool CanGoBack(int step)
        {
            return step > Welcome && step <= Tour;
        }

        public static bool IsLast(int step)
        {
            return step == Tour;
        }

        public static bool IsComplete(int step)
        {
            return step >= Done;
        }

        public static int Back(int step)
        {
            if (step <= Welcome)
                return Welcome;
            if (step > Tour)
                return Tour;
            return step - 1;
        }

        public static int Forward(int step)
        {
            if (step < Welcome)
                return Welcome;
            if (step >= Tour)
                return Done;
            return step + 1;
        }

        public static string Title(int step)
        {
            switch (step)
            {
                case Welcome:
                    return "Welcome";
                case Ai:
                    return "AI";
                case Avatar:
                    return "Avatar";
                case Personality:
                    return "Personality";
                case Tour:
                    return "Controls";
                default:
                    return "Setup";
            }
        }

        public static void ApplySkip(CompanionSettings settings, int step)
        {
            if (settings == null)
                return;

            switch (step)
            {
                case Ai:
                    settings.ProviderId = ChatDefaults.LocalId;
                    break;
                case Avatar:
                    settings.AvatarSource = AvatarSourceNames.Auto;
                    break;
                case Personality:
                    settings.CompanionName = CompanionPersona.DefaultName;
                    settings.Relationship = RelationshipStyles.Friend;
                    break;
            }

            settings.Normalize();
        }

        /// <summary>
        /// Next on the AI step. A key (typed, already saved, or in the environment)
        /// keeps Grok. No key continues on the local model. This does not write the key.
        /// </summary>
        public static void ApplyAiContinue(CompanionSettings settings, bool willHaveKey)
        {
            if (settings == null)
                return;

            settings.ProviderId = willHaveKey ? ChatDefaults.GrokId : ChatDefaults.LocalId;
            settings.Normalize();
        }

        public static void ApplyAvatar(CompanionSettings settings, string source)
        {
            if (settings == null)
                return;

            settings.AvatarSource = source;
            settings.Normalize();
        }

        public static void ApplyPersonality(CompanionSettings settings, string name, string relationship)
        {
            if (settings == null)
                return;

            settings.CompanionName = name ?? string.Empty;
            settings.Relationship = relationship;
            settings.Normalize();
        }

        public static HotkeyEntry[] TourHotkeys(string pushToTalkKey)
        {
            return HotkeyCatalog.Current(pushToTalkKey);
        }
    }

    public static class SetupWizardCopy
    {
        public static string StepLabel(int step)
        {
            var index = step + 1;
            if (index < 1)
                index = 1;
            if (index > SetupWizardFlow.StepCount)
                index = SetupWizardFlow.StepCount;
            return "Step " + index.ToString() + " of " + SetupWizardFlow.StepCount.ToString() + "  ·  " + SetupWizardFlow.Title(step);
        }

        public static string WelcomeLead()
        {
            return AppName.Display + " is a desktop companion. She sits in a transparent window over your other apps. There is no main menu. When this setup finishes, you go straight to her.";
        }

        public static string WelcomeCloud()
        {
            return "Grok chat and xAI text-to-speech send what you type, and what she says, to xAI. That is the cloud path.";
        }

        public static string WelcomeLocal()
        {
            return "Private mode uses a local Ollama model instead. That content stays on this PC. Private mode never sends it to cloud TTS, the voice agent, or Uthana.";
        }

        public static string AiNote()
        {
            return "Paste an xAI API key, or skip and use the local model. The key is saved only in the secret store (%APPDATA%\\ProjectLeia\\secrets.json). It is never written to settings.json, the install folder, or the log, and it is not shown again after saving.";
        }

        public static string AiSkipNote()
        {
            return "Skip leaves any saved key where it is and switches chat to the local model. Next without a key does the same. Next after a key uses Grok. Test connection uses the same check as Settings.";
        }

        public static string AvatarNote()
        {
            return "These are the same sources as Settings, Avatar. The choice applies in this window. Skip keeps Auto.";
        }

        public static string PersonalityNote()
        {
            return "Her name and how she relates to you become the chat persona. Skip uses " + CompanionPersona.DefaultName + ", as a friend. Clearing the name keeps the original friend prompt.";
        }

        public static string[] TourNotes()
        {
            return new[]
            {
                "The tray icon shows and hides " + AppName.Display + ". Right-click it for chat, settings, pause, and quit.",
                "Drag a visible pixel to move the window. Right-click a visible pixel to turn rotate mode on. R resets that rotation. Esc leaves rotate mode.",
                "These keys match Settings, Controls. " + HotkeyCatalog.RebindNote
            };
        }
    }
}
