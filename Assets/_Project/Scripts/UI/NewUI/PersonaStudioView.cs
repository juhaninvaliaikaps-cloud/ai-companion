using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Core;
using AICompanion.Overlay;
using AICompanion.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-150: the Persona Studio, a window of its own inside the overlay (Unity has one real
    /// window, so this is a movable panel with its own frame). New and Import open it from the
    /// Persona tab. It edits a draft and saves the whole persona on Save.
    ///
    /// Steps in this version: Start, Identity, Description. Pisara, Voice, Look and Review come
    /// in the following PRs (see JUH-150).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PersonaStudioView : MonoBehaviour, IOverlayKeepAwake
    {
        enum Step
        {
            Start = 0,
            Identity = 1,
            Description = 2,
        }

        static readonly string[] StepLabels = { "Start", "1 · Identity", "2 · Description" };

        const float PlaneDistance = 0.35f;
        const float WindowWidth = 600f;
        const float WindowHeight = 640f;

        SettingsPanel _settings;
        HappiTheme _theme;
        HappiForm _form;
        GameObject _canvasObject;
        Canvas _canvas;
        RectTransform _panel;
        TextMeshProUGUI _title;
        TextMeshProUGUI _status;
        readonly List<HappiButton> _steps = new List<HappiButton>();
        HappiButton _close;
        HappiButton _back;
        HappiButton _next;
        HappiButton _save;
        RectTransform _body;
        RectTransform _popup;
        TextMeshProUGUI _meter;
        float _s = 1f;
        float _width;
        float _layoutW = -1f;
        float _layoutH = -1f;
        bool _attached;
        bool _open;
        bool _focusClaimed;
        bool _positioned;

        Step _step = Step.Start;
        PersonaDefinition _draft;
        bool _isNew;
        bool _dirty;
        bool _pictureChanged;
        byte[] _picturePng;
        Texture2D _pictureTexture;
        string _paste = string.Empty;
        string _statusText = string.Empty;
        Action _afterDiscard;

        public bool IsOpen
        {
            get { return _open; }
        }

        public bool KeepFrameRateActive
        {
            get { return _attached && (_open || _focusClaimed); }
        }

        public void Attach(SettingsPanel settings)
        {
            if (_attached || settings == null)
                return;
            _settings = settings;
            _theme = HappiTheme.Current;
            _form = new HappiForm(_theme);
            BuildChrome();
            _attached = true;
            _canvasObject.SetActive(false);
        }

        public void Detach()
        {
            if (!_attached)
                return;
            _attached = false;
            ReleaseFocus();
            ClearPicture();
            if (_canvasObject != null)
                Destroy(_canvasObject);
            _canvasObject = null;
            _open = false;
        }

        void OnDestroy()
        {
            Detach();
        }

        /// <summary>Opens on the start screen: create, template, duplicate or import.</summary>
        public void OpenNew()
        {
            if (!_attached)
                return;
            _draft = null;
            _isNew = true;
            _dirty = false;
            SetPicture(null, false);
            _step = Step.Start;
            Show();
        }

        /// <summary>Opens a saved persona on her Identity step.</summary>
        public void OpenEdit(string id)
        {
            if (!_attached)
                return;
            var persona = _settings.StudioLoad(id);
            if (persona == null)
            {
                OpenNew();
                Status("That persona couldn't be read.");
                return;
            }

            _draft = persona;
            _isNew = false;
            _dirty = false;
            SetPicture(persona.HasPicture ? _settings.StudioPicture(persona.Id) : null, false);
            _step = Step.Identity;
            Show();
        }

        void Show()
        {
            var tray = GetComponent<CompanionTray>();
            if (tray != null)
                tray.ShowCompanion();
            _open = true;
            _canvasObject.SetActive(true);
            var typing = GetComponent<ITypingFocus>();
            if (typing != null)
                typing.FocusForTyping();
            _statusText = string.Empty;
            _form.Rebuild = true;
        }

        public void Close()
        {
            _open = false;
            _popup = DestroyPopup();
            _form.BlurFields();
            ReleaseFocus();
            if (_canvasObject != null)
                _canvasObject.SetActive(false);
        }

        /// <summary>Close, asking first when there are unsaved changes.</summary>
        void TryClose()
        {
            if (_dirty)
                AskDiscard(Close);
            else
                Close();
        }

        void Update()
        {
            if (!_attached || !_open || _canvasObject == null)
                return;
            HappiUiKit.FollowCamera(_canvas, PlaneDistance);
            Layout();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_popup != null)
                    _popup = DestroyPopup();
                else if (_form.AnyFieldFocused())
                    _form.BlurFields();
                else
                    TryClose();
            }

            SyncFocus();
            var shown = HappiUiKit.Literal(_statusText);
            if (_status.text != shown)
                _status.text = shown;
        }

        void LateUpdate()
        {
            if (!_attached || !_open || !_form.Rebuild)
                return;
            _form.Rebuild = false;
            RebuildBody();
        }

        // ---- Chrome ------------------------------------------------------------------------

        void BuildChrome()
        {
            _canvasObject = HappiUiKit.CreateCanvas(transform, "PersonaStudioCanvas", 85, out _canvas);
            var panel = HappiUiKit.Panel(_canvasObject.transform, "PersonaStudio", _theme.PanelBackground, true);
            _panel = panel.rectTransform;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = _theme.Border;
            outline.effectDistance = new Vector2(1f, -1f);

            _title = HappiUiKit.Text(_panel, "Title", "Happi · Persona Studio", _theme.FontTitle, _theme.Text, TextAlignmentOptions.MidlineLeft, true);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _title.raycastTarget = true;
            _title.gameObject.AddComponent<HappiWindowDrag>().Window = _panel;
            _close = HappiUiKit.Button(_panel, "Close", "×", _theme.FontBody, TryClose);

            for (var i = 0; i < StepLabels.Length; i++)
            {
                var index = i;
                _steps.Add(HappiUiKit.Button(_panel, "Step " + i, StepLabels[i], _theme.FontSmall, () => GoTo((Step)index)));
            }

            var scrollImage = HappiUiKit.Panel(_panel, "Body", _theme.PanelInset, true);
            var scroll = scrollImage.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            var viewport = HappiUiKit.Rect(scrollImage.transform, "Viewport");
            HappiUiKit.Stretch(viewport, 6f, 6f, 6f, 6f);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;
            _body = HappiUiKit.Rect(viewport, "Content");
            _body.anchorMin = new Vector2(0f, 1f);
            _body.anchorMax = new Vector2(1f, 1f);
            _body.pivot = new Vector2(0.5f, 1f);
            _body.sizeDelta = new Vector2(0f, 8f);
            _body.anchoredPosition = Vector2.zero;
            scroll.content = _body;

            _back = HappiUiKit.Button(_panel, "Back", "‹ Back", _theme.FontSmall, () => GoTo(_step - 1));
            _next = HappiUiKit.Button(_panel, "Next", "Next ›", _theme.FontSmall, () => GoTo(_step + 1));
            _save = HappiUiKit.Button(_panel, "Save", "Save", _theme.FontSmall, Save);
            _status = HappiUiKit.Text(_panel, "Status", string.Empty, _theme.FontSmall, _theme.TextMuted, TextAlignmentOptions.MidlineLeft, false);
            _status.fontStyle = FontStyles.Italic;
        }

        void Layout()
        {
            if (Mathf.Approximately(_layoutW, Screen.width) && Mathf.Approximately(_layoutH, Screen.height))
                return;
            _layoutW = Screen.width;
            _layoutH = Screen.height;
            _s = Mathf.Min(_theme.UiScale(Screen.dpi), Mathf.Max(1f, Screen.width / 640f));
            var s = _s;
            var margin = 8f;
            var width = Mathf.Min(Screen.width - 2f * margin, WindowWidth * s);
            var height = Mathf.Min(Screen.height - 2f * margin, WindowHeight * s);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            if (!_positioned)
            {
                _panel.anchoredPosition = Vector2.zero;
                _positioned = true;
            }

            _panel.sizeDelta = new Vector2(width, height);
            var pad = _theme.Padding * s;
            var gap = _theme.Spacing * s;
            var row = 26f * s;
            var inner = width - 2f * pad;
            _title.fontSize = _theme.FontTitle * s;
            HappiUiKit.Place(_title.rectTransform, pad, pad, inner - row - gap, row);
            HappiUiKit.Place(_close.Rect, width - pad - row, pad, row, row);
            _close.Label.fontSize = _theme.FontBody * s;

            var y = pad + row + gap;
            var stepW = (inner - gap * (_steps.Count - 1)) / _steps.Count;
            for (var i = 0; i < _steps.Count; i++)
            {
                HappiUiKit.Place(_steps[i].Rect, pad + i * (stepW + gap), y, stepW, row);
                _steps[i].Label.fontSize = _theme.FontSmall * s;
            }

            y += row + gap;
            var footer = row + 20f * s + gap * 2f;
            var bodyRect = (RectTransform)_body.parent.parent;
            HappiUiKit.Place(bodyRect, pad, y, inner, height - y - pad - footer);
            var buttonsY = height - pad - 20f * s - gap - row;
            var third = (inner - gap * 2f) / 3f;
            HappiUiKit.Place(_back.Rect, pad, buttonsY, third, row);
            HappiUiKit.Place(_next.Rect, pad + third + gap, buttonsY, third, row);
            HappiUiKit.Place(_save.Rect, pad + 2f * (third + gap), buttonsY, third, row);
            HappiUiKit.Place(_status.rectTransform, pad, height - pad - 20f * s, inner, 20f * s);
            _status.fontSize = _theme.FontSmall * s;
            _width = inner - 12f;
            _form.Rebuild = true;
        }

        // ---- Steps -------------------------------------------------------------------------

        void GoTo(Step step)
        {
            if (step < Step.Start || step > Step.Description)
                return;
            if (step != Step.Start && _draft == null)
            {
                Status("Pick how to start first.");
                return;
            }

            _form.BlurFields();
            _form.OpenDropdown = null;
            _step = step;
            _form.Rebuild = true;
        }

        void RebuildBody()
        {
            if (_body == null)
                return;
            _popup = DestroyPopup();
            _meter = null;
            for (var i = 0; i < _steps.Count; i++)
            {
                var colors = _steps[i].Button.colors;
                colors.normalColor = i == (int)_step ? _theme.Selected : _theme.Button;
                _steps[i].Button.colors = colors;
                _steps[i].Button.interactable = i == 0 || _draft != null;
            }

            var name = _draft != null && !string.IsNullOrWhiteSpace(_draft.DisplayName) ? _draft.DisplayName : (_isNew ? "New persona" : "Persona");
            _title.text = HappiUiKit.Literal("Happi · Persona Studio · " + name + (_dirty ? " •" : string.Empty));
            _back.Button.interactable = _step > Step.Start && !(_step == Step.Identity && !_isNew);
            _next.Button.interactable = _step < Step.Description && _draft != null;
            _save.Button.interactable = _draft != null;
            _save.Label.text = _isNew ? "Save and use her" : "Save";

            _form.Begin(_body, _width, _s);
            try
            {
                switch (_step)
                {
                    case Step.Identity: BuildIdentity(); break;
                    case Step.Description: BuildDescription(); break;
                    default: BuildStart(); break;
                }
            }
            catch (Exception ex)
            {
                _form.Note("This step could not be drawn (" + ex.GetType().Name + ").");
                Debug.LogWarning("Persona Studio: step " + _step + " failed: " + ex.Message);
            }

            _form.End();
        }

        void BuildStart()
        {
            _form.Section("New persona");
            _form.Button("Create a new persona", () => Begin(Blank(), Step.Identity));
            var templates = new List<string>(PersonaTemplates.Ids);
            _form.Dropdown("Start from a template", "pick one", templates, PersonaTemplates.Name, null, id => Begin(PersonaTemplates.Create(id, string.Empty), Step.Identity));
            _form.Note("A template fills in a short description, a speaking style and a temperament. Change anything you like.");

            var entries = _settings.StudioEntries();
            var ids = new List<string>();
            var names = new Dictionary<string, string>();
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null || entries[i].Persona == null)
                    continue;
                ids.Add(entries[i].Persona.Id);
                names[entries[i].Persona.Id] = PersonaName(entries[i].Persona);
            }

            if (ids.Count > 0)
                _form.Dropdown("Duplicate", "pick a persona", ids, id => names[id], null, Duplicate);

            var active = _settings.CurrentSettings != null ? _settings.CurrentSettings.PersonaId : string.Empty;
            if (!string.IsNullOrEmpty(active) && names.ContainsKey(active))
                _form.Button("Edit " + names[active], () => OpenEdit(active));

            _form.Section("Import");
            _form.Button("Import a .txt or .md file…", ImportTextFile);
            _form.Area("Or paste her description", _paste, 120f, PersonaDefinition.MaxDescriptionLength, v => _paste = v, v => _paste = v);
            _form.Button("Use this text", () =>
            {
                if (string.IsNullOrWhiteSpace(_paste))
                {
                    Status("Paste her description first.");
                    return;
                }

                var draft = Blank();
                draft.Description = _paste.Trim();
                _paste = string.Empty;
                Begin(draft, Step.Identity);
            });
            _form.Note("Character cards (SillyTavern or Chub PNG and JSON) come in the next update.");
        }

        void BuildIdentity()
        {
            _form.Section("Who she is");
            _form.Field("Name", _draft.DisplayName, 40, v => Edit(p => p.DisplayName = v));
            _form.Field("Tagline (one line, shown under her name)", _draft.Tagline, PersonaDefinition.MaxTaglineLength, v => Edit(p => p.Tagline = v));

            _form.Section("Picture");
            var round = PersonaPictureShapes.Normalize(_draft.PictureShape) == PersonaPictureShapes.Round;
            _form.Picture(_pictureTexture, 132f, round);
            if (_pictureTexture == null)
                _form.Note("No picture yet.");
            _form.Buttons(
                new KeyValuePair<string, Action>("Choose picture…", ChoosePicture),
                new KeyValuePair<string, Action>("Remove picture", () =>
                {
                    SetPicture(null, true);
                    MarkDirty();
                }));
            _form.Field("Or paste a picture's file path", string.Empty, 260, LoadPictureFrom);
            _form.Buttons(
                new KeyValuePair<string, Action>((round ? "✓ " : string.Empty) + "Round", () => Edit(p => p.PictureShape = PersonaPictureShapes.Round)),
                new KeyValuePair<string, Action>((!round ? "✓ " : string.Empty) + "Square", () => Edit(p => p.PictureShape = PersonaPictureShapes.Square)));
            _form.Note("The centre of the picture is used. Happi keeps a copy, encrypted on this PC; the original file isn't needed afterwards.");
            BuildDescribeFromPicture();
            BuildHistory();
        }

        // ---- JUH-154: describe her from the picture --------------------------------------------

        string _described = string.Empty;
        bool _describing;
        System.Threading.CancellationTokenSource _describeCancel;

        void BuildDescribeFromPicture()
        {
            if (_pictureTexture == null)
                return;
            _form.Button(_describing ? "Describing her picture…" : "Describe her from the picture", DescribePicture);
            if (_described.Length == 0)
            {
                _form.Note("A vision model writes a short appearance paragraph. You decide whether it goes into her description. In Private mode or with 18+ open, only the local vision model is used.");
                return;
            }

            _form.Note(_described);
            _form.Buttons(
                new KeyValuePair<string, Action>("Add to her description", () =>
                {
                    var text = _described;
                    _described = string.Empty;
                    Edit(p => p.Description = PictureDescribeGate.Append(p.Description, text));
                    Status("Added her appearance to the description. Save to keep it.");
                }),
                new KeyValuePair<string, Action>("Discard", () =>
                {
                    _described = string.Empty;
                    _form.Rebuild = true;
                }));
        }

        async void DescribePicture()
        {
            if (_describing || _pictureTexture == null)
                return;
            var privacy = GetComponent<IPrivacyMode>();
            var keepsLocal = IntimateExpressions.KeepsLocal(privacy != null && privacy.PrivateMode);
            var chat = GetComponent<AICompanion.UI.ChatPanel>();
            var settings = _settings != null ? _settings.CurrentSettings : null;
            var localVision = settings != null ? (settings.LocalVisionModel ?? string.Empty).Trim() : string.Empty;
            var choice = PictureDescribeGate.Choose(keepsLocal, chat != null && chat.CloudSeesImages, localVision.Length > 0);
            if (choice.Route == PictureDescribeRoute.None)
            {
                Status(choice.Reason);
                return;
            }

            byte[] jpeg;
            try
            {
                jpeg = _pictureTexture.EncodeToJPG(85);
            }
            catch (Exception ex)
            {
                Status("Couldn't prepare her picture (" + ex.GetType().Name + ").");
                return;
            }

            _describing = true;
            _described = string.Empty;
            _form.Rebuild = true;
            Status(choice.Route == PictureDescribeRoute.Cloud ? "Asking the cloud model to describe her…" : "Asking the local vision model to describe her…");
            if (_describeCancel != null)
                _describeCancel.Dispose();
            _describeCancel = new System.Threading.CancellationTokenSource();
            var token = _describeCancel.Token;
            string text = string.Empty;
            string failure = string.Empty;
            try
            {
                if (choice.Route == PictureDescribeRoute.Cloud)
                {
                    var result = await chat.RequestPictureDescription(jpeg, _pictureTexture.width, _pictureTexture.height, PictureDescribeGate.Prompt, 60f, token);
                    if (result.Ok)
                        text = result.Text;
                    else
                        failure = result.UserMessage.Length > 0 ? result.UserMessage : "The cloud model didn't answer.";
                }
                else
                {
                    var result = await AICompanion.ScreenLook.LocalVisionExchange.Run(new AICompanion.AI.UnityChatTransport(this), settings.LocalBaseUrl, settings.LocalModel,
                        localVision, jpeg, 120f, token, "0", PictureDescribeGate.Prompt);
                    if (result.Ok)
                        text = result.Observation;
                    else
                        failure = result.Message;
                }
            }
            catch (Exception ex)
            {
                failure = "Describing failed (" + ex.GetType().Name + ").";
            }

            if (this == null)
                return;
            _describing = false;
            _described = PictureDescribeGate.Clean(text);
            Status(_described.Length > 0 ? "Here is what the model saw. Add it to her description, or discard it." : (failure.Length > 0 ? failure : "The model gave no description."));
            _form.Rebuild = true;
        }

        // ---- JUH-154: version history --------------------------------------------------------------

        void BuildHistory()
        {
            if (_draft == null || string.IsNullOrWhiteSpace(_draft.Id))
                return;
            var versions = _settings.PersonaVersions(_draft.Id);
            if (versions.Count == 0)
                return;
            _form.Section("History");
            var paths = new List<string>();
            var labels = new Dictionary<string, string>();
            for (var i = 0; i < versions.Count; i++)
            {
                paths.Add(versions[i].Path);
                labels[versions[i].Path] = versions[i].Label;
            }

            _form.Dropdown("History…", "restore an earlier version", paths, p => labels[p], null, RestoreVersion);
            _form.Note("Each save keeps the version before it (the last 10). Restoring loads it here; Save to keep it. Her picture isn't part of the history.");
        }

        void RestoreVersion(string path)
        {
            var restored = PersonaHistory.Restore(path);
            if (restored == null || _draft == null)
            {
                Status("That version couldn't be read.");
                return;
            }

            // Keep her id and picture; everything else comes from the version.
            restored.Id = _draft.Id;
            restored.HasPicture = _draft.HasPicture;
            _draft = restored;
            if (string.IsNullOrWhiteSpace(_draft.Description))
                _draft.Description = _draft.Personality;
            _dirty = true;
            _form.Rebuild = true;
            Status("Restored the version from " + System.IO.Path.GetFileName(path) + ". Save to keep it.");
        }

        void BuildDescription()
        {
            _form.Section("Description");
            _form.Area("Who she is, her life, how she acts (up to 32,000 characters)", _draft.Description, 230f, PersonaDefinition.MaxDescriptionLength,
                v => Edit(p => p.Description = v, false), UpdateMeter);
            _meter = _form.Note(PersonaDescription.Meter(_draft.Description));
            _form.Area("Speaking style", _draft.SpeakingStyle, 54f, PersonaDefinition.MaxSpeakingStyleLength, v => Edit(p => p.SpeakingStyle = v, false));

            _form.Section("Example lines");
            _form.Area("A few short exchanges", _draft.ExampleLines, 100f, PersonaDefinition.MaxExampleLinesLength, v => Edit(p => p.ExampleLines = v, false));
            _form.Note("For example:  You: Hi!  Her: Oh, it's you again. Small models keep her voice better with a few of these.");

            if (_settings.CurrentSettings != null && _settings.CurrentSettings.IntimateExpressions)
            {
                _form.Section("18+ notes", true);
                _form.Area("Private notes for 18+ scenes", _draft.AdultNotes, 100f, PersonaDefinition.MaxAdultNotesLength, v => Edit(p => p.AdultNotes = v, false));
                _form.Note("These stay on this PC. They are added to her prompt only while the 18+ gate is open, and then chat is always local. They are never sent to a cloud model.");
            }

            _form.Note("After you save, Pisara reads her description again, as it does today. The Pisara step with its generators comes in the next update.");
        }

        void UpdateMeter(string text)
        {
            if (_meter != null)
                _meter.text = HappiUiKit.Literal(PersonaDescription.Meter(text));
        }

        // ---- Draft -------------------------------------------------------------------------

        static PersonaDefinition Blank()
        {
            var persona = PersonaDefinition.CreateStarter(string.Empty);
            persona.DisplayName = string.Empty;
            persona.Personality = string.Empty;
            persona.SpeakingStyle = string.Empty;
            persona.TemperamentMode = TemperamentModes.Auto;
            return persona;
        }

        void Begin(PersonaDefinition draft, Step step)
        {
            _draft = draft;
            _isNew = true;
            _dirty = true;
            if (!_pictureChanged)
                SetPicture(null, false);
            GoTo(step);
        }

        void Duplicate(string id)
        {
            var copy = _settings.StudioLoad(id);
            if (copy == null)
            {
                Status("That persona couldn't be read.");
                return;
            }

            var picture = copy.HasPicture ? _settings.StudioPicture(copy.Id) : null;
            copy.Id = string.Empty;
            copy.DisplayName = (string.IsNullOrWhiteSpace(copy.DisplayName) ? "Persona" : copy.DisplayName) + " copy";
            copy.HasPicture = false;
            _draft = copy;
            _isNew = true;
            _dirty = true;
            SetPicture(picture, picture != null);
            GoTo(Step.Identity);
        }

        void Edit(Action<PersonaDefinition> change, bool rebuild = true)
        {
            if (_draft == null || change == null)
                return;
            change(_draft);
            MarkDirty();
            if (rebuild)
                _form.Rebuild = true;
        }

        void MarkDirty()
        {
            if (_dirty)
                return;
            _dirty = true;
            _form.Rebuild = true;
        }

        void Save()
        {
            if (_draft == null)
                return;
            _form.BlurFields();
            if (string.IsNullOrWhiteSpace(_draft.DisplayName))
            {
                Status("Give her a name first.");
                GoTo(Step.Identity);
                return;
            }

            if (string.IsNullOrWhiteSpace(_draft.Description))
            {
                Status("Write or paste her description first.");
                GoTo(Step.Description);
                return;
            }

            string message;
            string savedId;
            if (!_settings.StudioSave(_draft, _pictureChanged, _picturePng, _isNew, out savedId, out message))
            {
                Status(message);
                return;
            }

            // Keep editing what was saved: the id is now known.
            var saved = _settings.StudioLoad(savedId);
            if (saved != null)
                _draft = saved;
            _isNew = false;
            _dirty = false;
            _pictureChanged = false;
            Status(message);
            _form.Rebuild = true;
        }

        // ---- Picture and import --------------------------------------------------------------

        void ChoosePicture()
        {
            string path;
            if (!AICompanion.Settings.OpenFileDialog.TryPick("Choose her picture", AICompanion.Settings.OpenFileDialog.Images, out path))
            {
                Status("No picture was picked. You can also paste a file path below.");
                return;
            }

            LoadPictureFrom(path);
        }

        void LoadPictureFrom(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            path = path.Trim().Trim('"');
            try
            {
                if (!File.Exists(path))
                {
                    Status("That file wasn't found.");
                    return;
                }

                if (new FileInfo(path).Length > PersonaPictureImage.MaxFileBytes)
                {
                    Status("That picture is larger than 20 MB.");
                    return;
                }

                byte[] png;
                string error;
                if (!PersonaPictureImage.TryPrepare(File.ReadAllBytes(path), out png, out error))
                {
                    Status(error);
                    return;
                }

                SetPicture(png, true);
                MarkDirty();
                Status("Picture ready. Save to keep it.");
                _form.Rebuild = true;
            }
            catch (Exception ex)
            {
                Status("Couldn't read that picture (" + ex.GetType().Name + ").");
            }
        }

        void ImportTextFile()
        {
            string path;
            if (!AICompanion.Settings.OpenFileDialog.TryPick("Import her description", AICompanion.Settings.OpenFileDialog.Texts, out path))
            {
                Status("No file was picked. You can also paste the text below.");
                return;
            }

            string text;
            string message;
            if (!SettingsPanel.StudioReadText(path, out text, out message))
            {
                Status(message);
                return;
            }

            var draft = Blank();
            draft.Description = text.Trim();
            draft.DisplayName = CompanionPersona.SanitizeName(Path.GetFileNameWithoutExtension(path));
            Begin(draft, Step.Identity);
            Status("Imported " + Path.GetFileName(path) + ".");
        }

        void SetPicture(byte[] png, bool changed)
        {
            ClearPicture();
            _picturePng = png;
            _pictureChanged = changed;
            _pictureTexture = PersonaPictureImage.Load(png);
        }

        void ClearPicture()
        {
            if (_pictureTexture != null)
                Destroy(_pictureTexture);
            _pictureTexture = null;
        }

        // ---- Pop-up --------------------------------------------------------------------------

        void AskDiscard(Action then)
        {
            _afterDiscard = then;
            _popup = DestroyPopup();
            var shade = HappiUiKit.Panel(_panel, "Popup", new Color(0f, 0f, 0f, 0.45f), true);
            _popup = shade.rectTransform;
            HappiUiKit.Stretch(_popup, 0f, 0f, 0f, 0f);
            var box = HappiUiKit.Panel(_popup, "Box", _theme.PanelBackground, true);
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = _theme.Border;
            var w = Mathf.Min(_panel.sizeDelta.x - 40f, 320f * _s);
            var h = 112f * _s;
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            box.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            box.rectTransform.sizeDelta = new Vector2(w, h);
            box.rectTransform.anchoredPosition = Vector2.zero;
            var text = HappiUiKit.Text(box.rectTransform, "Text", "Discard your unsaved changes?", _theme.FontBody * _s, _theme.Text, TextAlignmentOptions.Center, false);
            HappiUiKit.Place(text.rectTransform, 10f * _s, 12f * _s, w - 20f * _s, 40f * _s);
            var bw = (w - 30f * _s) / 2f;
            var discard = HappiUiKit.Button(box.rectTransform, "Discard", "Discard", _theme.FontSmall * _s, () =>
            {
                _popup = DestroyPopup();
                _dirty = false;
                var action = _afterDiscard;
                _afterDiscard = null;
                if (action != null)
                    action();
            });
            HappiUiKit.Place(discard.Rect, 10f * _s, h - 40f * _s, bw, 28f * _s);
            var keep = HappiUiKit.Button(box.rectTransform, "Keep", "Keep editing", _theme.FontSmall * _s, () => _popup = DestroyPopup());
            HappiUiKit.Place(keep.Rect, 20f * _s + bw, h - 40f * _s, bw, 28f * _s);
        }

        RectTransform DestroyPopup()
        {
            if (_popup != null)
                Destroy(_popup.gameObject);
            return null;
        }

        // ---- Helpers -------------------------------------------------------------------------

        void Status(string message)
        {
            _statusText = message ?? string.Empty;
        }

        static string PersonaName(PersonaDefinition persona)
        {
            return string.IsNullOrWhiteSpace(persona.DisplayName) ? persona.Id : persona.DisplayName;
        }

        void SyncFocus()
        {
            var focused = _form.AnyFieldFocused();
            if (focused && !_focusClaimed)
            {
                DebugInputGuard.ClaimTextField();
                _focusClaimed = true;
            }
            else if (!focused)
            {
                ReleaseFocus();
            }
        }

        void ReleaseFocus()
        {
            if (!_focusClaimed)
                return;
            DebugInputGuard.ReleaseTextField();
            _focusClaimed = false;
        }
    }
}
