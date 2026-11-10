using AICompanion.AI;
using AICompanion.ScreenLook;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.Settings
{
    public sealed partial class SettingsPanel
    {
        int _screenRevision = -1;

        void TrackScreen()
        {
            if (!_open || _tab != Tab.Screen || _building)
                return;

            var look = GetComponent<ScreenLookController>();
            var revision = look != null ? look.Revision : 0;
            if (revision == _screenRevision)
                return;

            _screenRevision = revision;
            _rebuild = true;
        }

        void BuildScreen()
        {
            var look = GetComponent<ScreenLookController>();
            NoteBlock("Off until you turn it on. One window, one frame, then you ask what she sees. This is not continuous game commentary.");
            AddToggle("Read a window", _settings.ScreenLookEnabled, value =>
            {
                _settings.ScreenLookEnabled = value;
                ApplyLive();
                if (look != null)
                {
                    if (value)
                        look.RefreshWindows();
                    else
                        look.DisableFeature();
                }

                _rebuild = true;
            });
            if (!_settings.ScreenLookEnabled)
            {
                NoteBlock("Nothing is captured while this is off.");
                return;
            }

            NoteBlock("Preview stays on this PC. Allowing cloud screen analysis sends the selected window image to the configured cloud vision provider (Grok, at the Base URL on the AI tab). The image is not saved. Private mode never sends it and does not switch to a cloud model.");
            AddToggle("Allow cloud screen analysis", _settings.ScreenCloudConsent, value =>
            {
                _settings.ScreenCloudConsent = value;
                ApplyLive();
                _rebuild = true;
            });
            var localChat = _settings.PrivateMode || _settings.ProviderId == ChatDefaults.LocalId;
            var decision = look != null ? look.CurrentDecision() : new ScreenLookDecision(false, ScreenLookRoute.None, string.Empty);
            var plansLocal = look != null && look.PlansLocalVision();
            var visionEngine = plansLocal
                ? (string.IsNullOrEmpty(_settings.LocalVisionModel) ? "not set" : _settings.LocalVisionModel + " on this PC")
                : decision.Route == ScreenLookRoute.Cloud
                    ? "Grok after cloud consent"
                    : "not sending";
            NoteBlock("Chat engine: " + (localChat ? "local text (" + _settings.LocalModel + ")" : "Grok (" + _settings.Model + ")")
                + ". Vision engine: " + visionEngine + ".");
            AddField("Local vision model", _settings.LocalVisionModel, false, value =>
            {
                _settings.LocalVisionModel = value;
                ApplyLive();
            });
            AddButton("Use qwen2.5vl:3b", () =>
            {
                _settings.LocalVisionModel = OllamaVision.RecommendedTag;
                ApplyLive();
                _rebuild = true;
            });
            NoteBlock("The vision model and the chat model are different. Rocinante stays text-only. Install the vision model yourself with " + OllamaVision.PullCommand + ". One frame loads it, then unloads it, then the text model loads for the reply. A smaller tag is " + OllamaVision.SmallerTag + ". This screen does not download models or change Ollama's server settings.");
            if (decision.Route == ScreenLookRoute.Local)
                NoteBlock("What do you see? uses the local vision model with cloud screen analysis off. The image, the observation, the reply, and speech stay on this PC. There is no cloud fallback. qwen2.5vl:3b is not a guarantee that small text can be read.");
            else if (decision.Route == ScreenLookRoute.Cloud)
                NoteBlock("Cloud screen analysis is allowed for the window you select. It does not include the rest of the desktop.");
            else if (decision.Message == ScreenLookPolicy.ConsentRequired)
                NoteBlock("Cloud screen analysis is off, and no local vision model is set. What do you see? stays off until you set one or turn cloud screen analysis on.");
            else if (localChat && _settings.PrivateMode)
                NoteBlock("Private mode uses that local vision model, then the local text model. The image stays on this PC. There is no cloud fallback.");
            else if (!string.IsNullOrEmpty(decision.Message))
                NoteBlock(decision.Message);

            if (look == null)
            {
                NoteBlock("Window capture is not available in this session.");
                return;
            }

            NoteBlock(string.IsNullOrEmpty(look.StatusText) ? "Ready." : look.StatusText);
            AddButton("Refresh window list", () =>
            {
                look.RefreshWindows();
                _rebuild = true;
            });

            var windows = look.Windows;
            if (windows != null)
            {
                for (var i = 0; i < windows.Count; i++)
                {
                    var target = windows[i];
                    var label = WindowLabel(target);
                    AddButton(label, () => look.Select(target));
                }
            }

            if (look.Selected == null)
                Note("No window selected.");
            else
                Note("Selected: " + WindowLabel(look.Selected) + ". That name is the window title from Windows, not text read from the picture.");

            AddPreview(look);
            var lookButton = AddButton("Look at screen", () =>
            {
                if (look.IsBusy)
                    look.Stop();
                else
                    look.RequestLook();
            });
            var lookWidget = lookButton != null ? lookButton.GetComponent<Button>() : null;
            if (lookWidget != null)
                lookWidget.interactable = ScreenLookPolicy.LookButtonInteractable(decision.SendButtonEnabled, look.IsBusy);
            AddButton(look.IsBusy ? "Stop" : "Stop look", () => look.Stop());
            AddButton("Clear window", () => look.ClearSelection());
            NoteBlock("Stop cancels the current frame. Clear window drops the selection and the preview. Game support, exclusive fullscreen, and the performance target are not verified in this build.");
        }

        void AddPreview(ScreenLookController look)
        {
            var tex = look != null ? look.PreviewTexture : null;
            if (tex == null)
            {
                Note("The preview appears here after you pick a window.");
                return;
            }

            var go = new GameObject("Preview");
            go.transform.SetParent(_body, false);
            var rect = go.AddComponent<RectTransform>();
            Place(rect, 140f);
            var image = go.AddComponent<RawImage>();
            image.texture = tex;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        void NoteBlock(string text)
        {
            var label = CreateText(_body, _font, 12, TextAnchor.UpperLeft, text);
            label.fontStyle = FontStyle.Italic;
            label.color = new Color(0.80f, 0.76f, 0.86f, 1f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            var lines = 1;
            if (text != null && text.Length > 58)
                lines = (text.Length + 57) / 58;
            if (lines > 8)
                lines = 8;
            Place(label.rectTransform, lines <= 1 ? 20f : 15f * lines + 6f);
        }

        static string WindowLabel(WindowTarget target)
        {
            if (target == null)
                return "(none)";
            var title = string.IsNullOrEmpty(target.Title) ? "(untitled window)" : target.Title;
            if (title.Length > 42)
                title = title.Substring(0, 42);
            if (target.Minimized)
                title += " (minimised)";
            return title;
        }
    }
}
