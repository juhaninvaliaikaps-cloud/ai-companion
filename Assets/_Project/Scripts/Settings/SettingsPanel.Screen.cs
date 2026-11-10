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
            if (_settings.PrivateMode)
                NoteBlock("Private mode is on. Images stay on this PC, and cloud speech stays off. The local model cannot look at a picture.");
            else if (!_settings.ScreenCloudConsent)
                NoteBlock("Cloud screen analysis is off. What do you see? will not send a frame until you allow it.");
            else
                NoteBlock("Cloud screen analysis is allowed for the window you select. It does not include the rest of the desktop.");

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
                Note("Selected: " + WindowLabel(look.Selected));

            AddPreview(look);
            AddButton("Look at screen", () =>
            {
                if (look.IsBusy)
                    look.Stop();
                else
                    look.RequestLook();
            });
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
