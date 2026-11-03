using System;
using AICompanion.Avatar;
using AICompanion.Overlay;
using AICompanion.UI;
using AICompanion.Voice;
using UnityEngine;

namespace AICompanion.Settings
{
    public static class SettingsApplicator
    {
        public static void Apply(CompanionSettings settings, Component host)
        {
            if (settings == null || host == null)
                return;

            settings.Normalize();
            var provider = AICompanion.Voice.OutboundPrivacy.ChatProviderId(settings.PrivateMode, settings.ProviderId);

            var chat = host.GetComponent<ChatPanel>();
            if (chat != null)
                ApplyChat(chat.RuntimeChat, settings, provider);

            var speaker = host.GetComponent<VoiceSpeaker>();
            if (speaker != null)
            {
                ApplyChat(speaker.RuntimeChat, settings, provider);
                var voice = speaker.RuntimeVoice;
                if (voice != null)
                {
                    voice.enabled = settings.TtsEnabled;
                    voice.voiceId = settings.VoiceId;
                    voice.language = settings.TtsLanguage;
                    voice.volume = settings.Volume;
                }

                speaker.SetMuted(settings.Muted);
                speaker.ApplyPlaybackVolume();
            }

            var listener = host.GetComponent<SpeechListener>();
            if (listener != null)
            {
                var stt = listener.RuntimeStt;
                if (stt != null)
                {
                    stt.enabled = settings.SttEnabled;
                    stt.language = settings.SttLanguage;
                    stt.modelFile = settings.WhisperModel;
                    stt.pushToTalkKey = ParseKey(settings.PushToTalkKey);
                }

                listener.ApplyRecognitionSettings();
            }

            var window = host.GetComponent<OverlayWindow>();
            if (window != null)
            {
                if (window.IsTopmost != settings.AlwaysOnTop)
                    window.SetTopmost(settings.AlwaysOnTop);
                window.SetClickThroughMode(OverlayClickThroughPolicy.Parse(settings.ClickThrough));
                window.SetIdleFrameRate(settings.IdleFrameRate);
                if (Mathf.Abs(window.WindowScale - settings.Scale) > 0.001f)
                    window.SetWindowScale(settings.Scale);
            }

            var tray = host.GetComponent<CompanionTray>();
            if (tray != null)
            {
                if (tray.IsPaused != settings.Paused)
                    tray.SetPaused(settings.Paused);
                if (tray.HideTaskbarButton != settings.HideTaskbarButton)
                    tray.SetHideTaskbarButton(settings.HideTaskbarButton);
                if (tray.Menu.StartWithWindows != settings.StartWithWindows)
                {
                    var stuck = tray.TrySetStartWithWindows(settings.StartWithWindows);
                    if (!stuck)
                        settings.StartWithWindows = tray.Menu.StartWithWindows;
                }
            }

            var life = host.GetComponent<ProceduralLife>();
            if (life != null)
                life.CursorLookEnabled = settings.CursorLookAt;

            var mount = host.GetComponent<AvatarMount>();
            if (mount != null)
            {
                var desired = AvatarSourceNames.To(settings.AvatarSource);
                if (mount.LoadedPreference != desired)
                    mount.Select(desired);
            }
        }

        static void ApplyChat(AICompanion.AI.ChatSettings chat, CompanionSettings settings, string provider)
        {
            if (chat == null)
                return;

            chat.providerId = provider;
            chat.model = settings.Model;
            chat.localModel = settings.LocalModel;
            chat.baseUrl = settings.BaseUrl;
            chat.localBaseUrl = settings.LocalBaseUrl;
            chat.timeoutSeconds = settings.TimeoutSeconds;
            ApplyPersona(chat, settings);
        }

        public static void ApplyPersona(AICompanion.AI.ChatSettings chat, CompanionSettings settings)
        {
            if (chat == null || settings == null)
                return;

            chat.systemPrompt = CompanionPersona.Build(settings.CompanionName, settings.Relationship);
        }

        static KeyCode ParseKey(string name)
        {
            KeyCode key;
            if (!Enum.TryParse(name, true, out key) || key == KeyCode.None || SttSettings.Clashes(key))
                return KeyCode.V;
            return key;
        }
    }
}
