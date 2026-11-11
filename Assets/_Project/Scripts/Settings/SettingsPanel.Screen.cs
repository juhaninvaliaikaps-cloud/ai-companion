using AICompanion.AI;
using AICompanion.ScreenLook;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.Settings
{
    public sealed partial class SettingsPanel
    {
        int _screenRevision = -1;
        bool _screenPreviewBig;
        bool _screenPreviewFit = true;
        GameObject _screenPreviewRoot;

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
            if (look != null && look.PreviewTexture != null)
                AddButton("Inspect exact frame", OpenExactFrame);
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
            DestroyExactPreview();
            if (_screenPreviewBig && look.PreviewTexture != null)
                BuildExactFrame(look);
        }

        void OpenExactFrame()
        {
            _screenPreviewBig = true;
            _screenPreviewFit = true;
            _rebuild = true;
        }

        void CloseExactFrame()
        {
            _screenPreviewBig = false;
            DestroyExactPreview();
        }

        void DestroyExactPreview()
        {
            if (_screenPreviewRoot != null)
                Destroy(_screenPreviewRoot);
            _screenPreviewRoot = null;
        }

        /// <summary>
        /// Full-panel view of the exact decoded JPEG that was sent. Fit shows the whole frame;
        /// 1:1 shows real pixels and scrolls. It only reads the in-memory texture and never
        /// writes the frame to disk.
        /// </summary>
        void BuildExactFrame(ScreenLookController look)
        {
            var tex = look.PreviewTexture;
            if (tex == null)
                return;

            var root = new GameObject("ExactFrame");
            root.transform.SetParent(_canvasObject.transform, false);
            var rootRect = root.AddComponent<RectTransform>();
            Stretch(rootRect, 0f, 0f, 0f, 0f);
            var backdrop = root.AddComponent<Image>();
            backdrop.sprite = _white;
            backdrop.color = new Color(0.02f, 0.02f, 0.03f, 0.98f);
            backdrop.raycastTarget = true;
            _screenPreviewRoot = root;

            var details = string.IsNullOrEmpty(look.PreviewDetails) ? look.DeliveryNote : look.PreviewDetails;
            var title = CreateText(root.transform, _font, 12, TextAnchor.UpperLeft,
                "Exact frame sent to the vision model. " + details);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.offsetMin = new Vector2(10f, -60f);
            titleRect.offsetMax = new Vector2(-10f, -8f);

            var close = CreateButton(root.transform, "Close", CloseExactFrame);
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(70f, 24f);
            closeRect.anchoredPosition = new Vector2(-10f, -8f);

            var zoom = CreateButton(root.transform, _screenPreviewFit ? "1:1" : "Fit", () =>
            {
                _screenPreviewFit = !_screenPreviewFit;
                _rebuild = true;
            });
            var zoomRect = zoom.GetComponent<RectTransform>();
            zoomRect.anchorMin = new Vector2(1f, 1f);
            zoomRect.anchorMax = new Vector2(1f, 1f);
            zoomRect.pivot = new Vector2(1f, 1f);
            zoomRect.sizeDelta = new Vector2(70f, 24f);
            zoomRect.anchoredPosition = new Vector2(-86f, -8f);

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(root.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            Stretch(viewportRect, 10f, 64f, 10f, 10f);
            viewport.AddComponent<RectMask2D>();

            var content = new GameObject("Image");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            var raw = content.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;

            var scroll = root.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 60f;

            // Fit uses the panel size, so it needs no layout pass. Upscaling is capped at 1:1
            // so fitted text is not blurred by filtering.
            var panel = _panelRect != null ? _panelRect.rect.size : new Vector2(420f, 640f);
            var viewW = Mathf.Max(60f, panel.x - 20f);
            var viewH = Mathf.Max(60f, panel.y - 74f);
            var scale = _screenPreviewFit
                ? Mathf.Min(1f, Mathf.Min(viewW / tex.width, viewH / tex.height))
                : 1f;
            contentRect.sizeDelta = new Vector2(tex.width * scale, tex.height * scale);
            if (_screenPreviewFit)
                contentRect.anchoredPosition = new Vector2(Mathf.Max(0f, (viewW - tex.width * scale) * 0.5f), 0f);
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
            var aspect = tex.height > 0 ? tex.width / (float)tex.height : 1f;
            if (aspect < 0.2f)
                aspect = 0.2f;
            var viewH = 400f / aspect;
            if (viewH > 360f)
                viewH = 360f;
            if (viewH < 160f)
                viewH = 160f;
            Place(rect, viewH);
            var image = go.AddComponent<RawImage>();
            image.texture = tex;
            image.color = Color.white;
            image.raycastTarget = false;
            if (!string.IsNullOrEmpty(look.DeliveryNote))
                NoteBlock(look.DeliveryNote);
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
