using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.Overlay;
using AICompanion.Settings;
using AICompanion.UI;
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
    /// Steps: Start, Identity, Description, Pisara (JUH-151), Voice, Look and Review (JUH-152).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PersonaStudioView : MonoBehaviour, IOverlayKeepAwake
    {
        enum Step
        {
            Start = 0,
            Identity = 1,
            Description = 2,
            Pisara = 3,
            Voice = 4,
            Look = 5,
            Review = 6,
        }

        const Step LastStep = Step.Review;

        static readonly string[] StepLabels = { "Start", "Identity", "Description", "Pisara", "Voice", "Look", "Review" };

        /// <summary>Kokoro's bundled voices, the same list as the Voice tab.</summary>
        static readonly string[] KokoroVoices =
        {
            "af_heart", "af_bella", "af_nicole", "af_sky", "bf_emma", "bf_isabella", "bm_george", "bm_lewis",
            "am_adam", "am_michael", "am_puck", "am_santa", "hm_omega", "hm_psi", "jf_alpha", "jm_kumo",
        };

        /// <summary>The model may take a minute or two locally; the bar fills over this long.</summary>
        const float ExpectedReadSeconds = 90f;

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

        // JUH-152: the test chat on the Review step. Nothing in it is saved.
        readonly List<ChatMessage> _test = new List<ChatMessage>();
        string _testInput = string.Empty;
        bool _testing;

        // JUH-151: the Pisara step.
        CharacterSheet _sheet;
        bool _sheetChanged;
        TemperamentAxes _axes;
        bool _reading;
        bool _condensing;
        string _readPart;
        float _workStarted;
        CancellationTokenSource _work;
        TextMeshProUGUI _progressText;
        RectTransform _progressFill;
        int _shownSeconds = -1;
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
            CancelWork();
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
            ResetSheet(null);
            ClearTest();
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
            ResetSheet(_settings.StudioSheet(persona.Id));
            ClearTest();
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
            CancelWork();
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
            TickProgress();
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
            if (step < Step.Start || step > LastStep)
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
            _progressText = null;
            _progressFill = null;
            _shownSeconds = -1;
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
            _next.Button.interactable = _step < LastStep && _draft != null;
            _save.Button.interactable = _draft != null;
            _save.Label.text = _isNew ? "Save and use her" : "Save";

            _form.Begin(_body, _width, _s);
            try
            {
                switch (_step)
                {
                    case Step.Identity: BuildIdentity(); break;
                    case Step.Description: BuildDescription(); break;
                    case Step.Pisara: BuildPisara(); break;
                    case Step.Voice: BuildVoice(); break;
                    case Step.Look: BuildLook(); break;
                    case Step.Review: BuildReview(); break;
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
        }

        void BuildDescription()
        {
            _form.Section("Description");
            _form.Area("Who she is, her life, how she acts (up to 32,000 characters)", _draft.Description, 230f, PersonaDefinition.MaxDescriptionLength,
                v => Edit(p =>
                {
                    if (!string.Equals(p.Description, v, StringComparison.Ordinal))
                        p.Condensed = false;
                    p.Description = v;
                }, false), UpdateMeter);
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

            _form.Note("Next, Pisara reads her description and fills in who she is. You can also skip it: Pisara then reads her in the background after you save.");
        }

        // ---- Voice, Look, Review (JUH-152) ---------------------------------------------------

        void BuildVoice()
        {
            _form.Section("Her voice");
            _form.Note("Each voice engine has its own voice id. Empty keeps the engine's voice from the Voice tab.");
            _form.Field("Piper voice id (local), for example en_US-lessac-medium", _draft.PiperVoiceId, PersonaDefinition.MaxPiperVoiceIdLength, v => Edit(p => p.PiperVoiceId = v, false));
            var kokoro = new List<string> { string.Empty };
            kokoro.AddRange(KokoroVoices);
            if (!string.IsNullOrEmpty(_draft.KokoroVoiceId) && !kokoro.Contains(_draft.KokoroVoiceId))
                kokoro.Add(_draft.KokoroVoiceId);
            _form.Dropdown("Kokoro voice (local)", VoiceLabel(_draft.KokoroVoiceId), kokoro, VoiceLabel, v => v == (_draft.KokoroVoiceId ?? string.Empty),
                v => Edit(p => p.KokoroVoiceId = v));
            _form.Field("Chatterbox voice id (local)", _draft.ChatterboxVoiceId, PersonaDefinition.MaxChatterboxVoiceIdLength, v => Edit(p => p.ChatterboxVoiceId = v, false));
            _form.Field("Cloud voice id (xAI), for example eve", _draft.CloudVoiceId, PersonaDefinition.MaxCloudVoiceIdLength, v => Edit(p => p.CloudVoiceId = v, false));
            _form.Note("The xAI voice is used only when xAI is the cloud AI API. With DeepSeek or another API she speaks with the local voice.");

            _form.Section("Hear her");
            var active = _settings.CurrentSettings != null ? _settings.CurrentSettings.PersonaId : string.Empty;
            var isActive = !_isNew && !string.IsNullOrEmpty(_draft.Id) && string.Equals(active, _draft.Id, StringComparison.OrdinalIgnoreCase);
            if (isActive || _isNew)
            {
                _form.Button(_dirty || _isNew ? (_isNew ? "Save, use her and hear her" : "Save and hear her") : "Hear her", HearHer);
                _form.Note("She says her greeting, or a short hello, in the voice she'll use in chat.");
            }
            else
            {
                _form.Note("She isn't the active persona, so you would hear someone else's voice. Use her first.");
                _form.Button("Save and use her", () =>
                {
                    if (_dirty)
                        Save();
                    if (!_dirty && !string.IsNullOrEmpty(_draft.Id))
                    {
                        _settings.StudioUse(_draft.Id);
                        _form.Rebuild = true;
                    }
                });
            }
        }

        void HearHer()
        {
            var chat = GetComponent<ChatPanel>();
            if (chat == null)
                chat = FindFirstObjectByType<ChatPanel>();
            if (chat == null)
            {
                Status("The chat isn't running, so she can't speak.");
                return;
            }

            if (_isNew || _dirty)
            {
                Save();
                if (_dirty || _isNew)
                    return;
            }

            if (!chat.SpeakSample(SampleLine()))
                Status("She couldn't speak. Check the Voice tab.");
        }

        string SampleLine()
        {
            if (!string.IsNullOrWhiteSpace(_draft.Greeting))
                return _draft.Greeting.Trim();
            var name = string.IsNullOrWhiteSpace(_draft.DisplayName) ? "your companion" : _draft.DisplayName.Trim();
            return "Hi, I'm " + name + ". This is how I sound.";
        }

        static string VoiceLabel(string id)
        {
            return string.IsNullOrEmpty(id) ? "(the Voice tab's voice)" : id;
        }

        void BuildLook()
        {
            _form.Section("Her 3D avatar");
            var source = _draft.AvatarSource ?? string.Empty;
            _form.Choice("Same as the Look tab", source.Length == 0, () => Edit(p => p.AvatarSource = string.Empty));
            _form.Choice("Auto", SameText(source, AvatarSourceNames.Auto), () => Edit(p => p.AvatarSource = AvatarSourceNames.Auto));
            _form.Choice("Local prefab", SameText(source, AvatarSourceNames.Local), () => Edit(p => p.AvatarSource = AvatarSourceNames.Local));
            _form.Choice("VRM sample", SameText(source, AvatarSourceNames.Vrm), () => Edit(p => p.AvatarSource = AvatarSourceNames.Vrm));
            _form.Choice("FBX sample", SameText(source, AvatarSourceNames.Fbx), () => Edit(p => p.AvatarSource = AvatarSourceNames.Fbx));

            _form.Section("Expression portraits");
            _form.Field("Portrait folder (empty uses %APPDATA%\\ProjectLeia\\portraits)", _draft.PortraitFolder, PersonaDefinition.MaxPathLength,
                v => Edit(p => p.PortraitFolder = v, false));
            _form.Note("The 2D video avatar and its folder are set for all personas on the Look tab.");
        }

        static bool SameText(string a, string b)
        {
            return string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        void BuildReview()
        {
            _form.Section("Her");
            if (_pictureTexture != null)
                _form.Picture(_pictureTexture, 84f, PersonaPictureShapes.Normalize(_draft.PictureShape) == PersonaPictureShapes.Round);
            _form.Label(string.IsNullOrWhiteSpace(_draft.DisplayName) ? "(no name yet)" : _draft.DisplayName);
            if (!string.IsNullOrWhiteSpace(_draft.Tagline))
                _form.Note(_draft.Tagline);
            _form.Note(_draft.Condensed ? "Description: condensed by Pisara for the chat model." : PersonaDescription.Meter(_draft.Description));
            if (_sheet != null)
            {
                var choice = PisaraReading.Choose(_draft.TemperamentMode, _draft.Temperament, false, _sheet);
                if (!string.IsNullOrEmpty(choice.Line))
                    _form.Note(choice.Line);
                _form.Note(PisaraReading.RelationshipLine(_sheet));
            }
            else
            {
                _form.Note("Pisara hasn't read her here. It reads her in the background after you save.");
            }

            _form.Section("First greeting");
            _form.Area("Her first message in a new chat", _draft.Greeting, 70f, PersonaDefinition.MaxGreetingLength, v => Edit(p => p.Greeting = v, false));
            _form.Note("Shown when the chat is empty: after you switch to her, at launch with no history, or after clearing the history. It isn't spoken.");

            _form.Section("Test chat");
            _form.Note("Talk to her before you save. Nothing here is kept: no memory, no history, nothing in the real chat. It uses the chat model, and private mode keeps it local.");
            var name = string.IsNullOrWhiteSpace(_draft.DisplayName) ? "Her" : _draft.DisplayName.Trim();
            for (var i = 0; i < _test.Count; i++)
                _form.Label((_test[i].Role == ChatRole.User ? "You: " : name + ": ") + _test[i].Content);
            if (_testing)
                _form.Note(name + " is typing…");
            _form.Area("Say something", _testInput, 48f, 1000, v => _testInput = v, v => _testInput = v);
            _form.Buttons(
                new KeyValuePair<string, Action>(_testing ? "Wait…" : "Send", SendTest),
                new KeyValuePair<string, Action>("Clear test chat", () =>
                {
                    ClearTest();
                    _form.Rebuild = true;
                }));
        }

        CancellationTokenSource _testCancel;

        async void SendTest()
        {
            if (_testing || _draft == null || string.IsNullOrWhiteSpace(_testInput))
                return;
            var chat = GetComponent<ChatPanel>();
            if (chat == null)
                chat = FindFirstObjectByType<ChatPanel>();
            if (chat == null)
            {
                Status("The chat isn't running.");
                return;
            }

            _form.BlurFields();
            _test.Add(new ChatMessage(ChatRole.User, _testInput.Trim()));
            _testInput = string.Empty;
            _testing = true;
            _form.Rebuild = true;

            var copy = _draft.Copy();
            PersonaDescription.ApplyTo(copy);
            var name = string.IsNullOrWhiteSpace(copy.DisplayName) ? "her" : copy.DisplayName;
            var cancel = new CancellationTokenSource();
            _testCancel = cancel;
            ChatResult result;
            try
            {
                result = await chat.RequestTestReply(PersonaText.Build(copy, name, RelationshipRoles.Friend), new List<ChatMessage>(_test), cancel.Token);
            }
            catch (Exception ex)
            {
                result = new ChatResult(false, string.Empty, ChatErrorKind.Unreachable, "The test chat failed.", ex.GetType().Name);
            }

            if (ReferenceEquals(_testCancel, cancel))
                _testCancel = null;
            var cancelled = cancel.IsCancellationRequested;
            cancel.Dispose();
            _testing = false;
            if (this == null || !_attached || cancelled)
                return;
            if (result.Ok && !string.IsNullOrWhiteSpace(result.Text))
                _test.Add(new ChatMessage(ChatRole.Assistant, result.Text.Trim()));
            else
                Status(string.IsNullOrEmpty(result.UserMessage) ? "She didn't answer." : result.UserMessage);
            while (_test.Count > 20)
                _test.RemoveAt(0);
            _form.Rebuild = true;
        }

        void ClearTest()
        {
            if (_testCancel != null)
            {
                try
                {
                    _testCancel.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            _test.Clear();
            _testInput = string.Empty;
            _testing = false;
        }

        // ---- 3 · Pisara (JUH-151) -----------------------------------------------------------

        void BuildPisara()
        {
            var reader = _settings.StudioReader;
            var ready = false;
            var who = reader != null ? reader.DraftReaderLabel(out ready) : "no model is ready";

            _form.Section("Pisara");
            _form.Note("Pisara reads her description and fills in who she is: her temperament, how she knows you, her life and what lands with her. "
                       + "Change anything before you save. Read by " + who + ".");
            if (string.IsNullOrWhiteSpace(_draft.Description))
            {
                _form.Note("Write or paste her description first (step 2).");
                return;
            }

            if (_reading)
            {
                _progressText = _form.Label(ProgressLine(_readPart == null ? "Reading her" : "Reading her " + PartName(_readPart)));
                _progressFill = _form.Progress(0f);
                _form.Button("Skip", CancelWork);
            }
            else if (ready)
            {
                _form.Button(_sheet == null ? "Read her" : "Read her again (everything)", () => StartRead(null));
            }
            else
            {
                _form.Note("No model can read her right now. Set one up on the AI tab, or save now and Pisara reads her in the background later.");
            }

            if (_sheet == null)
            {
                if (!_reading)
                    _form.Note("Not read yet. If you skip this step, Pisara reads her in the background after you save, as before.");
            }
            else
            {
                if (!string.IsNullOrEmpty(_sheet.readBy))
                    _form.Note("Read by " + _sheet.readBy + (_sheet.readInCloud ? " (in the cloud)." : "."));
                BuildTemperament(ready);
                BuildRelationship(ready);
                BuildLife(ready);
                BuildLanding(ready);
            }

            BuildCondense(ready);
            if (_settings.CurrentSettings != null && _settings.CurrentSettings.IntimateExpressions)
            {
                _form.Section("18+", true);
                _form.Note("Her innocence and her 18+ secrets are read on this PC, by the local model, while the 18+ gate is open. That happens after you save, as it does today. They never go to the cloud.");
            }
        }

        void BuildTemperament(bool ready)
        {
            _form.Section("Temperament");
            var manual = TemperamentModes.Normalize(_draft.TemperamentMode) == TemperamentModes.Manual;
            var choice = PisaraReading.Choose(TemperamentModes.Auto, _draft.Temperament, false, _sheet);
            if (!string.IsNullOrEmpty(choice.Line))
                _form.Note(choice.Line);
            _form.Choice("Let Pisara decide (Auto)", !manual, () => Edit(p =>
            {
                p.TemperamentMode = TemperamentModes.Auto;
                p.Axes = null;
                _axes = null;
            }));
            _form.Choice("Use my own sliders", manual, () => Edit(p =>
            {
                _axes = (choice.Axes ?? TemperamentAxes.ForPreset(p.Temperament)).Clone();
                p.SetAxes(_axes);
                p.TemperamentMode = TemperamentModes.Manual;
            }));
            if (manual)
            {
                if (_axes == null)
                    _axes = (_draft.Axes ?? TemperamentAxes.ForPreset(_draft.Temperament)).Clone();
                for (var i = 0; i < TemperamentAxes.Count; i++)
                {
                    var axis = i;
                    _form.Slider(v => TemperamentAxes.Names[axis] + ": " + Mathf.RoundToInt(v), _axes.Value(axis), 0f, 100f, true, v =>
                    {
                        _axes.Set(axis, v);
                        _draft.SetAxes(_axes);
                        _draft.TemperamentMode = TemperamentModes.Manual;
                        MarkDirty();
                    });
                }
            }

            if (ready && !_reading)
                _form.Button("Redo temperament", () => StartRead(PartTemperament));
        }

        void BuildRelationship(bool ready)
        {
            _form.Section("How she knows you");
            var r = _sheet.relationship ?? (_sheet.relationship = new SheetRelationship());
            _form.Note(PisaraReading.RelationshipLine(_sheet));
            var roles = new List<string>(RelationshipRoles.All);
            _form.Dropdown("Role", Pretty(r.role), roles, Pretty, role => role == RelationshipRoles.Normalize(r.role),
                role => SetRelationship(role, r.label, r.sharedHistory));
            _form.Field("What she calls it (for example stepsister, old classmate)", r.label, CharacterSheet.MaxLabelLength,
                v => SetRelationship(r.role, v, r.sharedHistory));
            _form.Toggle("You share a past", r.sharedHistory, v => SetRelationship(r.role, r.label, v));
            _form.Note("Her starting affection and familiarity follow from the role and the shared past.");
            if (ready && !_reading)
                _form.Button("Redo relationship", () => StartRead(PartRelationship));
        }

        void SetRelationship(string role, string label, bool sharedHistory)
        {
            if (_sheet == null)
                return;
            var r = _sheet.relationship ?? new SheetRelationship();
            PisaraCorrections.SetRelationship(_sheet, role, label, r.affection, r.familiarity, sharedHistory);
            SheetEdited(true);
        }

        void BuildLife(bool ready)
        {
            _form.Section("Her life");
            var world = _sheet.world ?? (_sheet.world = new SheetWorld());
            _form.Area("Backstory (one fact per line)", Lines(world.backstory), 110f, CharacterSheet.MaxBackstory * (CharacterSheet.MaxLineLength + 1),
                v => { world.backstory = SplitLines(v, CharacterSheet.MaxBackstory, CharacterSheet.MaxLineLength); SheetEdited(false); });
            _form.Field("Right now", world.rightNow, CharacterSheet.MaxLineLength, v => { world.rightNow = v ?? string.Empty; SheetEdited(false); });
            if (world.lastWeek != null && world.lastWeek.Count > 0)
            {
                var week = new System.Text.StringBuilder();
                for (var i = 0; i < world.lastWeek.Count; i++)
                {
                    var entry = world.lastWeek[i];
                    if (entry == null)
                        continue;
                    week.Append(entry.daysAgo == 0 ? "Today" : entry.daysAgo == 1 ? "Yesterday" : entry.daysAgo + " days ago").Append(": ").Append(entry.text).Append('\n');
                }

                _form.Note("Her last week:\n" + week.ToString().TrimEnd());
            }

            if (world.openThreads != null && world.openThreads.Count > 0)
            {
                var threads = new System.Text.StringBuilder();
                for (var i = 0; i < world.openThreads.Count; i++)
                {
                    if (world.openThreads[i] != null)
                        threads.Append("• ").Append(world.openThreads[i].text).Append('\n');
                }

                _form.Note("What's going on in her life:\n" + threads.ToString().TrimEnd());
            }

            if (ready && !_reading)
                _form.Button("Redo her life", () => StartRead(PartLife));
        }

        void BuildLanding(bool ready)
        {
            _form.Section("What lands with her");
            var landing = _sheet.landing ?? (_sheet.landing = new SheetLanding());
            _form.Area("Touchy about (one per line)", Lines(landing.touchy), 70f, CharacterSheet.MaxLandingPhrases * (CharacterSheet.MaxPhraseLength + 1),
                v => { landing.touchy = SplitLines(v, CharacterSheet.MaxLandingPhrases, CharacterSheet.MaxPhraseLength); SheetEdited(false); });
            _form.Area("Wins her over (one per line)", Lines(landing.winsHerOver), 70f, CharacterSheet.MaxLandingPhrases * (CharacterSheet.MaxPhraseLength + 1),
                v => { landing.winsHerOver = SplitLines(v, CharacterSheet.MaxLandingPhrases, CharacterSheet.MaxPhraseLength); SheetEdited(false); });
            if (ready && !_reading)
                _form.Button("Redo what lands", () => StartRead(PartLanding));
        }

        void BuildCondense(bool ready)
        {
            if (PersonaDescription.Fits(_draft.Description))
                return;
            _form.Section("Condensed description");
            _form.Note("Her description is longer than the chat model can take. Pisara can condense it to "
                       + PersonaDescription.ModelBudget.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                       + " characters, keeping who she is, how she talks and what matters.");
            if (_condensing)
            {
                _progressText = _form.Label(ProgressLine("Condensing"));
                _progressFill = _form.Progress(0f);
                _form.Button("Skip", CancelWork);
            }
            else if (ready && !_reading)
            {
                _form.Button(_draft.Condensed ? "Condense again" : "Condense her description", StartCondense);
            }

            if (_draft.Condensed)
                _form.Area("What the chat model sees", _draft.Personality, 160f, PersonaDescription.ModelBudget, v => Edit(p => p.Personality = v, false));
            else
                _form.Note("Until then the chat model sees the first "
                           + PersonaDescription.ModelText(_draft.Description).Length.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                           + " characters.");
        }

        const string PartTemperament = "temperament";
        const string PartRelationship = "relationship";
        const string PartLife = "life";
        const string PartLanding = "landing";

        static string PartName(string part)
        {
            switch (part)
            {
                case PartTemperament: return "temperament";
                case PartRelationship: return "relationship";
                case PartLife: return "life";
                case PartLanding: return "likes and dislikes";
                default: return string.Empty;
            }
        }

        async void StartRead(string part)
        {
            var reader = _settings.StudioReader;
            if (_reading || _condensing || reader == null || _draft == null)
                return;
            _reading = true;
            _readPart = part;
            _workStarted = Time.unscaledTime;
            var cancel = new CancellationTokenSource();
            _work = cancel;
            _form.Rebuild = true;

            // Pisara reads the whole description, not the cut the chat model gets.
            var input = _draft.Copy();
            input.Personality = _draft.Description;
            var name = string.IsNullOrWhiteSpace(_draft.DisplayName) ? "her" : _draft.DisplayName;
            PisaraReadResult result = null;
            try
            {
                result = await reader.ReadDraftAsync(name, PersonaText.Build(input, name, RelationshipRoles.Friend), cancel.Token);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Persona Studio: Pisara reading failed (" + ex.GetType().Name + ")");
            }

            var cancelled = cancel.IsCancellationRequested;
            if (ReferenceEquals(_work, cancel))
                _work = null;
            cancel.Dispose();
            _reading = false;
            if (this == null || !_attached)
                return;
            _form.Rebuild = true;
            if (cancelled)
            {
                Status("Skipped.");
                return;
            }

            if (result == null || result.Sheet == null || result.Sheet.source != CharacterSheet.SourceModel)
            {
                var why = result != null && !string.IsNullOrEmpty(result.Failure) ? result.Failure : "no usable reply";
                Status("Pisara couldn't read her (" + why + "). Nothing was changed.");
                return;
            }

            MergeReading(result.Sheet, part);
            Status(part == null ? "Pisara read her. Check it, then save." : "Pisara read her " + PartName(part) + " again.");
        }

        void MergeReading(CharacterSheet read, string part)
        {
            if (_sheet == null || part == null)
            {
                _sheet = read;
            }
            else
            {
                switch (part)
                {
                    case PartTemperament: _sheet.temperament = read.temperament; break;
                    case PartRelationship:
                        _sheet.relationship = read.relationship;
                        _sheet.relationshipLocked = false;
                        break;
                    case PartLife: _sheet.world = read.world; break;
                    case PartLanding: _sheet.landing = read.landing; break;
                }

                _sheet.readBy = read.readBy;
                _sheet.readInCloud = _sheet.readInCloud || read.readInCloud;
            }

            if (part == null || part == PartTemperament)
                _axes = null;
            SheetEdited(true);
        }

        async void StartCondense()
        {
            var reader = _settings.StudioReader;
            if (_reading || _condensing || reader == null || _draft == null)
                return;
            _condensing = true;
            _workStarted = Time.unscaledTime;
            var cancel = new CancellationTokenSource();
            _work = cancel;
            _form.Rebuild = true;
            PersonaCondenseResult result = null;
            try
            {
                result = await reader.CondenseDraftAsync(string.IsNullOrWhiteSpace(_draft.DisplayName) ? "her" : _draft.DisplayName, _draft.Description, cancel.Token);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Persona Studio: condensing failed (" + ex.GetType().Name + ")");
            }

            var cancelled = cancel.IsCancellationRequested;
            if (ReferenceEquals(_work, cancel))
                _work = null;
            cancel.Dispose();
            _condensing = false;
            if (this == null || !_attached)
                return;
            _form.Rebuild = true;
            if (cancelled)
            {
                Status("Skipped.");
                return;
            }

            if (result == null || !result.Ok)
            {
                Status("Couldn't condense her description" + (result != null && result.Failure.Length > 0 ? ": " + result.Failure : "."));
                return;
            }

            _draft.Personality = result.Text;
            _draft.Condensed = true;
            MarkDirty();
            Status("Condensed. Check the text, then save.");
        }

        void CancelWork()
        {
            if (_testCancel != null)
            {
                try
                {
                    _testCancel.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            if (_work != null)
            {
                try
                {
                    _work.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        void ResetSheet(CharacterSheet sheet)
        {
            CancelWork();
            _sheet = sheet;
            _sheetChanged = false;
            _axes = null;
        }

        void SheetEdited(bool rebuild)
        {
            _sheetChanged = true;
            MarkDirty();
            if (rebuild)
                _form.Rebuild = true;
        }

        string ProgressLine(string what)
        {
            var seconds = Mathf.Max(0, Mathf.FloorToInt(Time.unscaledTime - _workStarted));
            return what + "… " + seconds + " s. A local model can take a minute or two.";
        }

        void TickProgress()
        {
            if ((!_reading && !_condensing) || _progressText == null)
                return;
            var seconds = Mathf.FloorToInt(Time.unscaledTime - _workStarted);
            if (seconds == _shownSeconds)
                return;
            _shownSeconds = seconds;
            var what = _condensing ? "Condensing" : _readPart == null ? "Reading her" : "Reading her " + PartName(_readPart);
            _progressText.text = HappiUiKit.Literal(ProgressLine(what));
            if (_progressFill != null)
                _progressFill.anchorMax = new Vector2(Mathf.Clamp01(seconds / ExpectedReadSeconds) * 0.95f, 1f);
        }

        static string Pretty(string role)
        {
            var r = RelationshipRoles.Normalize(role).Replace('_', ' ');
            return r.Length == 0 ? r : char.ToUpperInvariant(r[0]) + r.Substring(1);
        }

        static string Lines(List<string> lines)
        {
            return lines == null ? string.Empty : string.Join("\n", lines);
        }

        static List<string> SplitLines(string text, int max, int maxLength)
        {
            var list = new List<string>();
            var parts = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < parts.Length && list.Count < max; i++)
            {
                var line = parts[i].Trim();
                if (line.Length == 0)
                    continue;
                list.Add(line.Length > maxLength ? line.Substring(0, maxLength) : line);
            }

            return list;
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
            ResetSheet(null);
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
            var sheet = _settings.StudioSheet(copy.Id);
            copy.Id = string.Empty;
            copy.DisplayName = (string.IsNullOrWhiteSpace(copy.DisplayName) ? "Persona" : copy.DisplayName) + " copy";
            copy.HasPicture = false;
            _draft = copy;
            _isNew = true;
            _dirty = true;
            SetPicture(picture, picture != null);
            ResetSheet(sheet);
            _sheetChanged = sheet != null;
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
            if (_sheet != null)
                _sheet.Normalize();
            if (!_settings.StudioSave(_draft, _pictureChanged, _picturePng, _sheetChanged ? _sheet : null, _isNew, out savedId, out message))
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
            _sheetChanged = false;
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
