using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.GameCompanion;
using AICompanion.Overlay;
using AICompanion.ScreenLook;
using AICompanion.Settings;
using AICompanion.Settings.Presenters;
using AICompanion.UI;
using AICompanion.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-104 PR E: the app's settings view in the Happi theme. Every item of
    /// docs/ui-overhaul/INVENTORY.md section 4 is here. JUH-182 made it look like the Profile
    /// panel: eight top tabs with sub-tabs (<see cref="SettingsMenu"/>), and each topic in its
    /// own box; docs/ui-overhaul/SETTINGS_LAYOUT.md lists what is where. Every change goes through the PR B presenters
    /// and the old panel as host, so settings are applied and saved exactly as before.
    /// 18+ items appear under the same conditions as today.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HappiSettingsView : MonoBehaviour, IOverlayKeepAwake
    {
        // JUH-182: eight top tabs, each with sub-tabs (SettingsMenu). Developer is a page under App,
        // shown only with "Show developer settings"; the 18+ pages but Status only while the gate is open.
        const float PlaneDistance = 0.36f;

        SettingsPanel _settings;
        ChatPanel _chat;
        HappiTheme _theme;
        GameObject _canvasObject;
        Canvas _canvas;
        RectTransform _panel;
        TextMeshProUGUI _title;
        TextMeshProUGUI _status;
        HappiButton _close;
        readonly List<HappiButton> _tabs = new List<HappiButton>();
        readonly List<HappiButton> _pageButtons = new List<HappiButton>();
        readonly List<SettingsPage> _shownPages = new List<SettingsPage>(SettingsMenu.MaxPages);
        readonly SettingsPage[] _pageFor = new SettingsPage[SettingsMenu.TabCount];
        float _pagesY;
        float _pagesX;
        float _pagesW;
        float _pagesH;
        ScrollRect _scroll;
        RectTransform _viewport;
        RectTransform _body;
        RectTransform _frameRoot;
        readonly List<TMP_InputField> _fields = new List<TMP_InputField>();
        readonly PrivacyConfirm _confirm = new PrivacyConfirm();
        SettingsTab _tab = SettingsTab.Her;
        SettingsPage _page = SettingsPage.HerPersona;
        bool _attached;
        bool _open;
        bool _rebuild;
        string _openDropdown;
        bool _building;
        bool _focusClaimed;
        bool _framePreview;
        bool _frameFit = true;
        string _webSearchQuery = string.Empty;
        string _webSearchStatus = string.Empty;
        float _y;
        float _s = 1f;
        float _width;
        string _exportFolder = string.Empty;
        string _signature;
        TextMeshProUGUI _videoStatus;
        RectTransform _readingTrack;
        RectTransform _readingFill;
        TextMeshProUGUI _readingLabel;
        float _readingStartedAt;
        int _shownReadingSeconds = -1;
        float _layoutW = -1f;
        float _layoutH = -1f;

        public bool IsOpen => _open;

        public bool KeepFrameRateActive => _attached && (_open || _focusClaimed);

        public void Attach(SettingsPanel settings, ChatPanel chat)
        {
            if (_attached || settings == null)
                return;
            _settings = settings;
            _chat = chat;
            _theme = HappiTheme.Current;
            BuildChrome();
            _settings.OpenOverride = OnOpenRequested;
            _attached = true;
            Close();
            Debug.Log("New UI: settings view on");
        }

        public void Detach()
        {
            if (!_attached)
                return;
            _attached = false;
            if (_settings != null && _settings.OpenOverride == (Func<bool>)OnOpenRequested)
                _settings.OpenOverride = null;
            ReleaseFocus();
            if (_canvasObject != null)
                Destroy(_canvasObject);
            _canvasObject = null;
            _open = false;
            Debug.Log("New UI: settings view off");
        }

        void OnDestroy()
        {
            Detach();
        }

        /// <summary>F12 toggles; the tray, the wheel and "Settings" buttons open.</summary>
        bool OnOpenRequested()
        {
            if (!_attached)
                return false;
            if (_open && Input.GetKeyDown(HotkeyCatalog.SettingsKey))
                Close();
            else
                Open();
            return true;
        }

        /// <summary>Open a visible settings page from a shortcut.</summary>
        public bool TryOpenPage(SettingsPage page)
        {
            if (!_attached)
                return false;

            var tab = SettingsMenu.TabOf(page);
            var developer = S != null && S.ShowDeveloperSettings;
            if (!SettingsMenu.Visible(tab, IntimateExpressions.Active, developer).Contains(page))
                return false;

            _tab = tab;
            _pageFor[(int)tab] = page;
            LeavePage();
            Open();
            return true;
        }

        public void Open()
        {
            var tray = GetComponent<CompanionTray>();
            if (tray != null)
                tray.ShowCompanion();
            _open = true;
            _canvasObject.SetActive(true);
            var typing = GetComponent<ITypingFocus>();
            if (typing != null)
                typing.FocusForTyping();
            _rebuild = true;
        }

        public void Close()
        {
            _open = false;
            _confirm.Cancel();
            _framePreview = false;
            BlurFields();
            ReleaseFocus();
            if (_canvasObject != null)
                _canvasObject.SetActive(false);
        }

        void Update()
        {
            if (!_attached || !_open || _canvasObject == null)
                return;

            HappiUiKit.FollowCamera(_canvas, PlaneDistance);
            Layout();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_framePreview)
                {
                    _framePreview = false;
                    _rebuild = true;
                }
                else if (_confirm.Escape())
                    _rebuild = true;
                else if (AnyFieldFocused())
                    BlurFields();
                else
                {
                    Close();
                    return;
                }
            }

            SyncFocus();
            var signature = Signature();
            // Not while a field has focus or a slider is being dragged (the Size slider changes the window).
            if (signature != _signature && !AnyFieldFocused() && !Input.GetMouseButton(0))
            {
                _signature = signature;
                _rebuild = true;
            }

            if (_videoStatus != null)
            {
                var video = GetComponent<VideoAvatar>();
                var text = HappiUiKit.Literal(video != null ? video.StatusText : "The video avatar is not running.");
                if (_videoStatus.text != text)
                    _videoStatus.text = text;
            }

            AnimateReadingBar();

            var status = _settings.StatusMessage;
            var shown = HappiUiKit.Literal(status);
            if (_status.text != shown)
                _status.text = shown;
        }

        void LateUpdate()
        {
            if (!_attached || !_open || !_rebuild)
                return;
            _rebuild = false;
            RebuildBody();
        }

        /// <summary>Live values that other places can change (F8, F6, the tray, Ctrl+scroll, the wheel, a screen look).</summary>
        string Signature()
        {
            var s = _settings.CurrentSettings;
            if (s == null)
                return string.Empty;
            var look = _settings.ScreenController;
            return s.AlwaysOnTop + "|" + Mathf.RoundToInt(s.Scale * 100f) + "|" + s.Muted + "|" + s.Paused + "|" + s.HideTaskbarButton + "|" + s.StartWithWindows
                + "|" + s.AvatarSource + "|" + s.AvatarMode + "|" + s.PersonaId + "|" + s.PrivateMode + "|" + s.ProviderId + "|" + IntimateExpressions.Active
                + "|" + IntimateExpressions.Undressed
                + "|" + s.PrivateEndpointType + "|" + s.PrivateBaseUrl + "|" + s.PrivateModel
                + "|" + _settings.PrivateApiKeySaved + "|" + _settings.PrivateEndpointTestRunning
                + "|" + s.PisaraCloud + "|" + s.PisaraCloudBaseUrl + "|" + s.PisaraCloudModel + "|" + s.MemoryEmbeddings
                + "|" + _settings.PisaraApiKeySaved + "|" + _settings.PisaraApiKeyFromEnvironment
                + "|" + (look != null ? look.Revision : 0) + "|" + _settings.ConnectionTestRunning + "|" + s.ShowMoodDebug + "|" + s.VoiceNarration
                + "|" + _settings.PisaraRevision + "|" + _settings.TemperamentRevision
                + "|" + (_page == SettingsPage.SensesGames && _settings.Games != null ? _settings.Games.Revision : 0)
                + "|" + s.ShowDeveloperSettings;
        }

        // ---- Chrome ------------------------------------------------------------------------

        void BuildChrome()
        {
            _canvasObject = HappiUiKit.CreateCanvas(transform, "HappiSettingsCanvas", 80, out _canvas);
            var panel = HappiUiKit.Panel(_canvasObject.transform, "HappiSettings", _theme.PanelBackground, true);
            _panel = panel.rectTransform;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = _theme.Border;
            outline.effectDistance = new Vector2(1f, -1f);
            _title = HappiUiKit.Text(_panel, "Title", "Happi · Settings", _theme.FontTitle, _theme.Text, TextAlignmentOptions.MidlineLeft, true);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _close = HappiUiKit.Button(_panel, "Close", "×", _theme.FontBody, Close);
            for (var i = 0; i < SettingsMenu.TabCount; i++)
            {
                var index = i;
                _tabs.Add(HappiUiKit.Button(_panel, "Tab " + SettingsMenu.TabLabels[i], SettingsMenu.TabLabels[i], _theme.FontSmall, () =>
                {
                    _tab = (SettingsTab)index;
                    LeavePage();
                }));
            }

            for (var i = 0; i < SettingsMenu.MaxPages; i++)
            {
                var index = i;
                _pageButtons.Add(HappiUiKit.Button(_panel, "Page " + i, string.Empty, _theme.FontSmall, () =>
                {
                    if (index >= _shownPages.Count)
                        return;
                    _pageFor[(int)_tab] = _shownPages[index];
                    LeavePage();
                }));
            }

            for (var t = 0; t < SettingsMenu.TabCount; t++)
                _pageFor[t] = SettingsMenu.First((SettingsTab)t);

            // JUH-182: like the Profile panel, the body is clear and each topic is its own box.
            var scrollImage = HappiUiKit.Panel(_panel, "Body", new Color(0f, 0f, 0f, 0f), true);
            _scroll = scrollImage.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30f;
            _viewport = HappiUiKit.Rect(scrollImage.transform, "Viewport");
            HappiUiKit.Stretch(_viewport, 6f, 6f, 6f, 6f);
            _viewport.gameObject.AddComponent<RectMask2D>();
            _scroll.viewport = _viewport;
            _body = HappiUiKit.Rect(_viewport, "Content");
            _body.anchorMin = new Vector2(0f, 1f);
            _body.anchorMax = new Vector2(1f, 1f);
            _body.pivot = new Vector2(0.5f, 1f);
            _body.sizeDelta = new Vector2(0f, 8f);
            _body.anchoredPosition = Vector2.zero;
            _scroll.content = _body;
            _status = HappiUiKit.Text(_panel, "Status", string.Empty, _theme.FontSmall, _theme.TextMuted, TextAlignmentOptions.MidlineLeft, false);
            _status.fontStyle = FontStyles.Italic;
        }

        void Layout()
        {
            if (Mathf.Approximately(_layoutW, Screen.width) && Mathf.Approximately(_layoutH, Screen.height))
                return;
            _layoutW = Screen.width;
            _layoutH = Screen.height;
            _s = Mathf.Min(_theme.UiScale(Screen.dpi), Mathf.Max(1f, Screen.width / 470f));
            var s = _s;
            var margin = 8f;
            var width = Mathf.Min(Screen.width - 2f * margin, _theme.SettingsWidth * s);
            var height = Mathf.Min(Screen.height - 2f * margin, _theme.SettingsMaxHeight * s);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = new Vector2(width, height);
            var pad = _theme.Padding * s;
            var gap = _theme.Spacing * s;
            var row = 26f * s;
            var inner = width - 2f * pad;
            _title.fontSize = _theme.FontTitle * s;
            HappiUiKit.Place(_close.Rect, width - pad - row, pad, row, row);
            _close.Label.fontSize = _theme.FontBody * s;
            HappiUiKit.Place(_title.rectTransform, pad, pad, inner - row - gap, row);
            var y = pad + row + gap;
            // JUH-182: one row of eight top tabs, then a row of sub-tabs.
            var columns = _tabs.Count;
            var tabW = (inner - gap * 0.5f * (columns - 1)) / columns;
            for (var i = 0; i < _tabs.Count; i++)
            {
                HappiUiKit.Place(_tabs[i].Rect, pad + i * (tabW + gap * 0.5f), y, tabW, row);
                _tabs[i].Label.fontSize = _theme.FontSmall * s;
            }

            y += row + gap * 0.5f;
            _pagesX = pad;
            _pagesY = y;
            _pagesW = inner;
            _pagesH = 22f * s;
            PlacePages();
            y += _pagesH + gap;
            var statusH = 20f * s;
            HappiUiKit.Place((RectTransform)_scroll.transform, pad, y, inner, height - y - pad - statusH - gap);
            HappiUiKit.Place(_status.rectTransform, pad, height - pad - statusH, inner, statusH);
            _status.fontSize = _theme.FontSmall * s;
            _width = inner - 12f;
            _rebuild = true;
        }

        // ---- Body --------------------------------------------------------------------------

        void RebuildBody()
        {
            if (_body == null)
                return;
            _building = true;
            BlurFields();
            _readingTrack = null;
            _readingFill = null;
            _readingLabel = null;
            for (var i = _body.childCount - 1; i >= 0; i--)
                Destroy(_body.GetChild(i).gameObject);
            if (_frameRoot != null)
                Destroy(_frameRoot.gameObject);
            _frameRoot = null;
            _fields.Clear();
            _videoStatus = null;
            _y = 4f;
            _inCard = false;
            _card = null;
            var adultOpen = IntimateExpressions.Active;
            var developer = S != null && S.ShowDeveloperSettings;
            _page = SettingsMenu.Resolve(_tab, _pageFor[(int)_tab], adultOpen, developer);
            _pageFor[(int)_tab] = _page;
            for (var i = 0; i < _tabs.Count; i++)
            {
                var selected = i == (int)_tab;
                var colors = _tabs[i].Button.colors;
                colors.normalColor = selected ? _theme.Selected : _theme.Button;
                _tabs[i].Button.colors = colors;
                _tabs[i].Label.color = i == (int)SettingsTab.Adult ? _theme.Intimate : _theme.Text;
            }

            _shownPages.Clear();
            _shownPages.AddRange(SettingsMenu.Visible(_tab, adultOpen, developer));
            PlacePages();

            try
            {
                if (!BuildPendingConfirm())
                    BuildPage(_page);
            }
            catch (Exception ex)
            {
                Note("This page could not be drawn (" + ex.GetType().Name + "). Try another one.");
                Debug.LogWarning("New UI: settings page " + _page + " failed: " + ex.Message);
            }

            CloseCard();
            _body.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _y + 8f);
            _signature = Signature();
            _building = false;
        }

        /// <summary>A tab or sub-tab was picked: close what was open on the old page.</summary>
        void LeavePage()
        {
            _openDropdown = null;
            _confirm.Cancel();
            _framePreview = false;
            _rebuild = true;
            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>The sub-tab row: one button per page the tab shows now, the open one highlighted.</summary>
        void PlacePages()
        {
            if (_pageButtons.Count == 0 || _pagesW <= 0f)
                return;
            var n = Mathf.Max(1, _shownPages.Count);
            var gap = _theme.Spacing * _s * 0.5f;
            var w = (_pagesW - gap * (n - 1)) / n;
            for (var i = 0; i < _pageButtons.Count; i++)
            {
                var button = _pageButtons[i];
                var on = i < _shownPages.Count;
                button.Rect.gameObject.SetActive(on);
                if (!on)
                    continue;
                HappiUiKit.Place(button.Rect, _pagesX + i * (w + gap), _pagesY, w, _pagesH);
                button.Label.text = HappiUiKit.Literal(SettingsMenu.Label(_shownPages[i]));
                button.Label.fontSize = _theme.FontSmall * _s * 0.95f;
                var selected = _shownPages[i] == _page;
                var colors = button.Button.colors;
                colors.normalColor = selected ? _theme.Hover : HappiTheme.Darken(_theme.Button, 0.15f);
                colors.selectedColor = colors.normalColor;
                button.Button.colors = colors;
                button.Label.color = selected ? _theme.HoverText : (_tab == SettingsTab.Adult ? _theme.Intimate : _theme.TextMuted);
            }
        }

        void BuildPage(SettingsPage page)
        {
            switch (page)
            {
                case SettingsPage.HerPersona: BuildPersona(); break;
                case SettingsPage.HerPersonality: BuildPersonality(); break;
                case SettingsPage.HerTalk: BuildHowSheTalks(); break;
                case SettingsPage.HerLife: BuildHerLife(); break;
                case SettingsPage.HerMemories: BuildMemories(); break;
                case SettingsPage.AiMode: BuildAiMode(); break;
                case SettingsPage.AiChat: BuildAiChat(); break;
                case SettingsPage.AiPrivate: BuildAiPrivate(); break;
                case SettingsPage.AiPisara: BuildAiPisara(); break;
                case SettingsPage.AiPersona: BuildAiPersona(); break;
                case SettingsPage.VoiceSpeaking: BuildVoice(); break;
                case SettingsPage.VoiceEngine: BuildVoiceEngine(); break;
                case SettingsPage.VoicePersona: BuildVoicePersona(); break;
                case SettingsPage.VoiceListening: BuildListening(); break;
                case SettingsPage.VoiceGames: BuildTalkWhileGaming(); break;
                case SettingsPage.LookAvatar: BuildLook(); break;
                case SettingsPage.LookVideo: BuildVideoPage(); break;
                case SettingsPage.Look3d: BuildLook3d(); break;
                case SettingsPage.LookWindow: BuildWindow(); break;
                case SettingsPage.ChatReplies: BuildChat(); break;
                case SettingsPage.ChatText: BuildChatText(); break;
                case SettingsPage.ChatHistory: BuildChatHistory(); break;
                case SettingsPage.SensesScreen: BuildScreen(); break;
                case SettingsPage.SensesGames: BuildGames(); break;
                case SettingsPage.SensesTools: BuildTools(); break;
                case SettingsPage.AdultStatus: BuildAdult(); break;
                case SettingsPage.AdultHer: BuildInnocence(); break;
                case SettingsPage.AdultSecrets: BuildIntimateSecrets(); break;
                case SettingsPage.AdultTalk: BuildIntimateTalk(); break;
                case SettingsPage.AppKeys: BuildKeys(); break;
                case SettingsPage.AppData: BuildData(); break;
                case SettingsPage.AppDeveloper: BuildDeveloper(); break;
                default: BuildPersona(); break;
            }
        }

        SettingsPresenter P => _settings.Presenter;

        CompanionSettings S => _settings.CurrentSettings;

        void Change(Action<CompanionSettings> change)
        {
            P.Change(change);
            _rebuild = true;
        }

        // ---- Persona: who she is, her life, her memories (JUH-121) ---------------------------

        void BuildPersona()
        {
            var catalog = _settings.PersonaCatalogNow();
            Section("Personas");
            Note("Each persona keeps her own chat, memories, mood and relationship.");
            // JUH-154: a grid of persona cards instead of the plain list; the setup name stays a row.
            var choices = _settings.Personas.Choices();
            if (choices.Count > 0 && choices[0].IsSetupName)
                Choice(choices[0].Label, choices[0].Active, () => { _settings.Personas.Choose(string.Empty); _rebuild = true; });
            BuildPersonaGrid();

            // JUH-150: new, template, duplicate and import open the Persona Studio.
            Button("New or import a persona…", () => OpenStudio(null));
            PersonaEntry current;
            if (!ActivePersona(catalog, out current))
                return;

            var persona = current.Persona;
            var id = persona.Id;
            Section("Edit " + (string.IsNullOrEmpty(persona.DisplayName) ? id : persona.DisplayName));
            Button("Open in Persona Studio", () => OpenStudio(id));
            if (current.BuiltIn)
                Note(current.UserFile ? "Editing your saved copy." : "Editing saves a copy. The built-in file stays.");
            Field("Display name", persona.DisplayName, false, v => Save(id, p => p.DisplayName = v));
            if (string.IsNullOrWhiteSpace(persona.Description))
                Area("Personality", persona.Personality, 80f, PersonaDefinition.MaxPersonalityLength, v => Save(id, p => p.Personality = v));
            else
                Note("Her description (" + persona.Description.Length.ToString("N0", CultureInfo.InvariantCulture) + " characters) is edited in the Persona Studio.");
            // JUH-155: how she talks is part of her description now.
            if (!string.IsNullOrWhiteSpace(persona.SpeakingStyle))
                Note("Her speaking style moves into her description when you open her in the Persona Studio.");
            if (!string.IsNullOrWhiteSpace(persona.Scenario))
                Note("She has a custom scenario. Edit it in the Persona Studio.");
            Field("Portrait folder", persona.PortraitFolder, false, v => Save(id, p => p.PortraitFolder = v));
            Note("Empty uses the standard portraits folder. Her voice, model and 3D avatar are on the Voice, AI and Look tabs, under This persona and 3D model.");

            Section("Export");
            BuildExport(persona);

            if (!string.Equals(current.Persona.Id, PersonaDefaults.HappiId, StringComparison.OrdinalIgnoreCase))
            {
                Section("Delete");
                Button("Delete persona", () => { _settings.DeletePersonaById(current.Persona.Id, out _); _rebuild = true; }, true);
            }
        }

        /// <summary>JUH-182: Her → Personality. What the reading found, the relationship, and her temperament.</summary>
        void BuildPersonality()
        {
            PersonaEntry current;
            if (!ActivePersona(_settings.PersonaCatalogNow(), out current))
                return;
            var id = current.Persona.Id;

            // JUH-118: what the model read from her description, then the temperament picker.
            Section("What the reading found");
            var view = _settings.PisaraView();
            Note(view.TemperamentLine);
            Note(view.RelationshipLine);
            Note(view.Status);
            Button(view.Reading ? "Re-read (reading...)" : "Re-read", () => { _settings.RereadPersona(); _rebuild = true; });
            if (view.Reading)
                BuildReadingProgress(view.ReadingStartedAt);
            var sheet = _settings.ActiveSheet();
            if (sheet != null && sheet.relationship != null)
            {
                Section("Relationship");
                BuildRelationshipCorrection();
            }

            Section("Temperament");
            Note("How quickly her moods build and fade. Auto takes it from her description.");
            Choice("Auto", !view.Manual, () => { _settings.SetPersonaTemperamentAuto(id); _rebuild = true; });
            for (var i = 0; i < PersonaTemperaments.Ids.Length; i++)
            {
                var preset = PersonaTemperaments.Get(PersonaTemperaments.Ids[i]);
                var stored = PersonaTemperaments.Normalize(preset.Id);
                Choice(preset.Name, view.Manual && !view.Custom && Same(PersonaTemperaments.Normalize(view.TemperamentId), stored), () => { _settings.SetPersonaTemperament(id, stored); _rebuild = true; });
            }

            Section("Custom temperament");
            Note("50 is the middle. Moving a slider overrides Auto and the presets.");
            var editing = (view.Axes ?? TemperamentAxes.Neutral()).Clone();
            for (var axis = 0; axis < TemperamentAxes.Count; axis++)
            {
                var index = axis;
                Slider(v => TemperamentAxes.SliderLabel(index, v), editing.Value(index), TemperamentAxes.Min, TemperamentAxes.Max, true, v =>
                {
                    editing.Set(index, v);
                    _settings.SetPersonaTemperamentAxes(id, editing);
                });
            }
        }

        /// <summary>JUH-117: one switch per Pisara part. Off freezes that part; nothing is deleted.</summary>
        void BuildHerLife()
        {
            Section("Relationship changes");
            Toggle("Relationship changes", S.PisaraRelationship, v => Change(s => s.PisaraRelationship = v));
            Note("How your messages land changes how much she likes you and how well you know each other. Off freezes both.");
            Section("Life while you are away");
            Toggle("Life while you are away", S.PisaraAwayEvents, v => Change(s => s.PisaraAwayEvents = v));
            Note("Things she is waiting for turn out, well or badly, while you are gone. Off leaves them open.");
            Section("Her own world");
            Toggle("Her own world", S.PisaraWorld, v => Change(s => s.PisaraWorld = v));
            Note("Her backstory and recent days are added to her memories, and the first chat opens mid-scene. Off stops adding; existing memories stay.");
        }

        void OpenStudio(string id)
        {
            var studio = GetComponent<PersonaStudioView>();
            if (studio == null)
            {
                _settings.NewPersona();
                _rebuild = true;
                return;
            }

            Close();
            if (string.IsNullOrEmpty(id))
                studio.OpenNew();
            else
                studio.OpenEdit(id);
        }

        // ---- JUH-154: the persona grid ---------------------------------------------------------

        string _gridMenuId;
        string _gridDeleteId;

        void BuildPersonaGrid()
        {
            var cards = _settings.PersonaCards();
            var catalog = _settings.PersonaCatalogNow();
            var width = W;
            var columns = PersonaGrid.Columns(width, _s);
            var gap = _theme.Spacing * _s;
            var cardW = (width - gap * (columns - 1)) / columns;
            var cardH = 132f * _s;
            for (var row = 0; row < PersonaGrid.Rows(cards.Count, columns); row++)
            {
                var line = HappiUiKit.Rect(_body, "PersonaRow");
                Place(line, cardH);
                PersonaCard menuCard = null;
                for (var column = 0; column < columns; column++)
                {
                    var index = row * columns + column;
                    if (index >= cards.Count)
                        break;
                    var card = cards[index];
                    BuildPersonaCard(line, card, catalog.Find(card.Id), column * (cardW + gap), cardW, cardH);
                    if (string.Equals(card.Id, _gridMenuId, StringComparison.OrdinalIgnoreCase))
                        menuCard = card;
                }

                if (menuCard != null)
                {
                    BuildPersonaMenu(menuCard);
                    CloseCard();
                }
            }
        }

        void BuildPersonaCard(RectTransform line, PersonaCard card, PersonaDefinition persona, float x, float width, float height)
        {
            var id = card.Id;
            var button = HappiUiKit.Button(line, "Card " + id, string.Empty, _theme.FontSmall * _s, () =>
            {
                if (_building)
                    return;
                _gridMenuId = null;
                _gridDeleteId = null;
                _settings.Personas.Choose(id);
                _rebuild = true;
            });
            HappiUiKit.Place(button.Rect, x, 0f, width, height);
            if (card.Active)
            {
                var colors = button.Button.colors;
                colors.normalColor = _theme.Selected;
                button.Button.colors = colors;
                var ring = button.Rect.gameObject.AddComponent<Outline>();
                ring.effectColor = _theme.Hover;
                ring.effectDistance = new Vector2(2f, -2f);
            }

            var pad = 6f * _s;
            var size = Mathf.Min(64f * _s, height - 52f * _s);
            Image frame;
            var picture = PersonaPictureCache.Frame(button.Rect, "Picture", _theme, out frame);
            HappiUiKit.Place(frame.rectTransform, (width - size) * 0.5f, pad, size, size);
            var texture = PersonaPictureCache.Get(_settings.PersonasFolderPath, persona);
            PersonaPictureCache.Show(frame, picture, texture, card.Round);
            if (texture == null)
            {
                // No picture: her initial in a circle.
                frame.gameObject.SetActive(true);
                frame.sprite = card.Round ? HappiUiKit.Circle() : HappiUiKit.Rounded();
                picture.color = new Color(1f, 1f, 1f, 0f);
                var initial = HappiUiKit.Text(frame.rectTransform, "Initial", HappiUiKit.Literal(card.Name.Length > 0 ? card.Name.Substring(0, 1).ToUpperInvariant() : "?"),
                    _theme.FontTitle * 1.3f * _s, _theme.Text, TextAlignmentOptions.Center, true);
                HappiUiKit.Stretch(initial.rectTransform, 0f, 0f, 0f, 0f);
            }

            var name = HappiUiKit.Text(button.Rect, "Name", HappiUiKit.Literal(card.Name), _theme.FontBody * _s, _theme.Text, TextAlignmentOptions.Center, true);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            HappiUiKit.Place(name.rectTransform, pad, pad + size + 2f * _s, width - 2f * pad, 20f * _s);
            var tagline = HappiUiKit.Text(button.Rect, "Tagline", HappiUiKit.Literal(card.Tagline), _theme.FontSmall * 0.9f * _s, _theme.TextMuted, TextAlignmentOptions.Top, false);
            tagline.overflowMode = TextOverflowModes.Ellipsis;
            HappiUiKit.Place(tagline.rectTransform, pad, pad + size + 22f * _s, width - 2f * pad, height - size - 26f * _s - pad);

            var more = HappiUiKit.Button(button.Rect, "More", "\u2699", _theme.FontBody * _s, () =>
            {
                if (_building)
                    return;
                _gridMenuId = string.Equals(_gridMenuId, id, StringComparison.OrdinalIgnoreCase) ? null : id;
                _gridDeleteId = null;
                _rebuild = true;
            });
            var moreSize = 24f * _s;
            HappiUiKit.Place(more.Rect, width - moreSize - 3f * _s, 3f * _s, moreSize, moreSize);
            var gearFont = HappiFonts.For("Segoe UI Symbol");
            if (gearFont != null)
            {
                more.Label.font = gearFont;
                more.Label.fontStyle = FontStyles.Normal;
            }
            else
            {
                more.Label.text = "...";
            }

            // JUH-153: deletion is one click from the card, with the same confirmation the menu uses.
            if ((card.Actions & PersonaCardActions.Delete) != 0)
            {
                var delete = HappiUiKit.Button(button.Rect, "Delete", "\u00d7", _theme.FontBody * _s, () =>
                {
                    if (_building)
                        return;
                    _gridMenuId = id;
                    _gridDeleteId = id;
                    _rebuild = true;
                });
                HappiUiKit.Place(delete.Rect, width - moreSize * 2f - 7f * _s, 3f * _s, moreSize, moreSize);
            }
        }

        /// <summary>The "⋯" menu of one card, shown under its row: Open in Studio, Duplicate, Delete.</summary>
        void BuildPersonaMenu(PersonaCard card)
        {
            var id = card.Id;
            if (string.Equals(_gridDeleteId, id, StringComparison.OrdinalIgnoreCase))
            {
                Note("Delete " + card.Name + "? This can't be undone.");
                Button("Confirm delete", () =>
                {
                    string message;
                    _settings.DeletePersonaById(id, out message);
                    _gridDeleteId = null;
                    _gridMenuId = null;
                    _rebuild = true;
                }, true);
                Button("Cancel", () => { _gridDeleteId = null; _rebuild = true; });
                return;
            }

            Section(card.Name);
            if ((card.Actions & PersonaCardActions.OpenInStudio) != 0)
                Button("Open in Studio", () => { _gridMenuId = null; OpenStudio(id); });
            if ((card.Actions & PersonaCardActions.Duplicate) != 0)
                Button("Duplicate", () =>
                {
                    string message;
                    _settings.DuplicatePersona(id, out message);
                    _gridMenuId = null;
                    _rebuild = true;
                });
            // JUH-153: export from the card, in the format chosen under the active persona; never her 18+ notes.
            Button("Export…", () =>
            {
                string message;
                _settings.ExportPersona(id, _exportFormat, false, out message);
                _gridMenuId = null;
                _rebuild = true;
            });
            if ((card.Actions & PersonaCardActions.Delete) != 0)
                Button(card.DeleteLabel, () => { _gridDeleteId = id; _rebuild = true; }, true);
            Button("Close", () => { _gridMenuId = null; _rebuild = true; });
        }

        // JUH-153: export the active persona as a character card, a card JSON or a zip.
        CardExportFormat _exportFormat = CardExportFormat.CardPng;
        bool _exportAdult;

        void BuildExport(PersonaDefinition persona)
        {
            Choice("Export as a character card (PNG)", _exportFormat == CardExportFormat.CardPng, () => { _exportFormat = CardExportFormat.CardPng; _rebuild = true; });
            Choice("Export as card JSON", _exportFormat == CardExportFormat.Json, () => { _exportFormat = CardExportFormat.Json; _rebuild = true; });
            Choice("Export as a zip (persona file + picture)", _exportFormat == CardExportFormat.Zip, () => { _exportFormat = CardExportFormat.Zip; _rebuild = true; });
            if (!string.IsNullOrWhiteSpace(persona.AdultNotes))
                Toggle("Include 18+ notes", _exportAdult, v => { _exportAdult = v; _rebuild = true; });
            var id = persona.Id;
            Button("Export…", () =>
            {
                string message;
                _settings.ExportPersona(id, _exportFormat, _exportAdult, out message);
                _rebuild = true;
            });
            Note("A card works in SillyTavern and Chub. Her 18+ notes are never exported unless you tick Include 18+ notes.");
        }

        /// <summary>The active persona's catalog entry. False, with a note, for the setup name or a missing file.</summary>
        bool ActivePersona(PersonaCatalog catalog, out PersonaEntry current)
        {
            current = null;
            var active = S.PersonaId ?? string.Empty;
            if (active.Length == 0)
            {
                Note("No persona is picked: she uses the name from setup.");
                return false;
            }

            current = catalog.FindEntry(active);
            if (current == null || current.Persona == null)
            {
                Note("The selected persona file is missing.");
                return false;
            }

            return true;
        }

        void BuildMemories()
        {
            var memories = _settings.Memories;
            Section("Memories");
            Note(memories.Summary);
            Note("Stored on this PC under ProjectLeia. Memories made by the local model in private mode are never sent to a cloud model; a cloud Pisara reading's seeded memories are not private.", true);
            // JUH-117: a fresh start with this persona; her persona and her own life stay.
            Button(StartOverText.Button(_settings.StartOverName), () => { _confirm.Request(PrivacyConfirmKind.StartOver); _rebuild = true; }, true);
            Note("Clears this persona's chat, what she remembers about you, her mood and your relationship. Her persona and her own life stay.");
            Note("Complete wipes only. Selective memory deletion is intentionally not available in this build.");

            // JUH-141: meaning as well as words; local Ollama only.
            Section("Smarter memory search");
            Toggle("Smarter memory search", S.MemoryEmbeddings, v => Change(s => s.MemoryEmbeddings = v));
            Note("She also finds memories by meaning, not only by the same words (\"my dad\" finds \"his father\"). Uses the small local model nomic-embed-text (about 274 MB) in Ollama on this PC; nothing leaves this PC. Install it once with: ollama pull nomic-embed-text. Without it she uses words only.");
        }

        /// <summary>A pending "are you sure" (start over, wipe, clear history) replaces the tab until it is answered.</summary>
        bool BuildPendingConfirm()
        {
            if (_confirm.Pending == PrivacyConfirmKind.StartOver)
            {
                Note(PrivacyConfirm.StartOverPrompt(_settings.StartOverName));
                Button("Confirm start over", () =>
                {
                    _confirm.Cancel();
                    _settings.StartOverConfirmed();
                    _rebuild = true;
                }, true);
                Button("Cancel", () => { _confirm.Cancel(); _rebuild = true; });
                return true;
            }

            if (_confirm.Pending == PrivacyConfirmKind.WipeAll || _confirm.Pending == PrivacyConfirmKind.ClearHistory)
            {
                Note(PrivacyConfirm.Prompt(_confirm.Pending, 0, null));
                var kind = _confirm.Pending;
                Button(kind == PrivacyConfirmKind.WipeAll ? "Confirm wipe" : "Confirm clear", () =>
                {
                    _confirm.Cancel();
                    if (kind == PrivacyConfirmKind.WipeAll)
                        _settings.WipeAllConfirmed();
                    else
                        _settings.ClearChatHistoryConfirmed();
                    _rebuild = true;
                }, true);
                Button("Cancel", () => { _confirm.Cancel(); _rebuild = true; });
                return true;
            }

            return false;
        }

        // ---- AI: Private mode first, then where her replies come from -----------------------

        /// <summary>JUH-182: AI → Mode. Private mode first, because it decides the rest.</summary>
        void BuildAiMode()
        {
            Section("Private mode");
            Toggle("Private mode", S.PrivateMode, v => { Change(s => s.PrivateMode = v); _rebuild = true; });
            Note("On: chat, voice, the voice agent, session memories, memory capture, innocence and 18+ secrets stay local. The Pisara cloud reading can still use its own service when that is on. Off by default. The chat box has a Private / Public switch too.", true);
            // JUH-177: what public mode may know about a private chapter. Its text never leaves this PC.
            Section("What public mode may know");
            Note("About a private chapter. What was said in private never leaves this PC.");
            Choice("Nothing", S.PrivateChapterHint == (int)PrivateChapterHint.Nothing, () => Change(s => s.PrivateChapterHint = (int)PrivateChapterHint.Nothing));
            Choice("That it happened", S.PrivateChapterHint == (int)PrivateChapterHint.ThatItHappened, () => Change(s => s.PrivateChapterHint = (int)PrivateChapterHint.ThatItHappened));
            Choice("What kind of moment (personal, romantic, intimate)", S.PrivateChapterHint == (int)PrivateChapterHint.WhatKind, () => Change(s => s.PrivateChapterHint = (int)PrivateChapterHint.WhatKind));
            Section("Active now");
            Label(P.ActiveModelText);
        }

        /// <summary>JUH-182: AI → Pisara. The model for character-sheet readings, and its key (moved from Keys).</summary>
        void BuildAiPisara()
        {
            // JUH-126: the reading can use its own cloud service; chat is unchanged.
            Section("Pisara model");
            Toggle("Use a cloud model for Pisara readings", S.PisaraCloud, v =>
            {
                Change(s => s.PisaraCloud = v);
                _rebuild = true;
            });
            Note("For reading her description into a character sheet only. Chat, session memories and memory capture keep their own model.");
            if (S.PisaraCloud)
            {
                Field("Pisara base URL", S.PisaraCloudBaseUrl, false, v => Change(s => s.PisaraCloudBaseUrl = v));
                Field("Pisara model", S.PisaraCloudModel, false, v => Change(s => s.PisaraCloudModel = v));
                Note("Empty uses the cloud AI API's base URL and model. The Pisara key is below.");
                Note("Her description goes to this cloud service for the reading. That includes the normal world and seeded memories. The request is built with 18+ off; innocence, 18+ secrets and everything else 18+ stay on the local model.", true);
                if (S.PrivateMode)
                    Note("Private mode does not move this reading: it still uses the Pisara cloud service when it is on. Chat, voice, session memories and the 18+ parts stay local.", true);
            }

            BuildPisaraKey();
        }

        /// <summary>
        /// JUH-182: AI → Chat model, the public choice. It can be set up while Private mode is on
        /// (it is kept for when it is off); only its connection test waits for public mode.
        /// </summary>
        void BuildAiChat()
        {
            if (S.PrivateMode)
            {
                Section("Private mode is on");
                Note("Chat uses the private model now. What you set here is kept for when Private mode is off.");
            }

            {
                Section("Chat provider");
                var grok = S.ProviderId == ChatDefaults.GrokId;
                Choice(ProviderLabel(ChatDefaults.CloudName, grok, false), grok, () => { P.SetProvider(ChatDefaults.GrokId); _rebuild = true; });
                Choice(ProviderLabel("Local Ollama", !grok, false), !grok, () => { P.SetProvider(ChatDefaults.LocalId); _rebuild = true; });
                Note("The cloud AI API runs on the provider's servers: xAI Grok by default, or Claude, DeepSeek or any OpenAI-compatible service. Local Ollama runs on this PC.");

                if (grok)
                {
                    Section(ChatDefaults.CloudName);
                    Field("Model", S.Model, false, v => Change(s => s.Model = v));
                    Field("Base URL", S.BaseUrl, false, v => Change(s => s.BaseUrl = v));
                    Note("xAI Grok by default (https://api.x.ai/v1). Claude: https://api.anthropic.com/v1 with a Claude model name. DeepSeek: https://api.deepseek.com/v1. Any other OpenAI-compatible API works too (OpenAI, OpenRouter, Groq).");
                    Note("Her cloud voice is an xAI feature: with another API she speaks with the local voice.");
                    Section("Can it see pictures?");
                    Note("For screen looks.");
                    var vision = VisionCapability.NormalizeMode(S.CloudVision);
                    Choice("Auto: known vision models", vision == VisionCapability.ModeAuto, () => Change(s => s.CloudVision = VisionCapability.ModeAuto));
                    Choice("Yes: this model reads pictures", vision == VisionCapability.ModeOn, () => Change(s => s.CloudVision = VisionCapability.ModeOn));
                    Choice("No: never send it a picture", vision == VisionCapability.ModeOff, () => Change(s => s.CloudVision = VisionCapability.ModeOff));
                    Note(VisionCapability.CloudModelAcceptsImages(S.Model)
                        ? "\"" + S.Model + "\" will get screen looks, after you allow them on Senses → Screen and Games."
                        : "\"" + S.Model + "\" won't get pictures. Known vision models include grok-4.7, deepseek-flash, Claude, gpt-4o, gpt-5 and Gemini.");
                    Section("Cloud AI API key");
                    Note(ApiKeyMask.Label(_settings.ApiKeySaved) + ".");
                    var key = Field("Paste a new API key for this service", string.Empty, true, null);
                    Button("Save key", () => { _settings.SaveApiKey(key != null ? key.text : string.Empty); if (key != null) key.text = string.Empty; _rebuild = true; });
                    Button("Remove saved key", () => { _settings.RemoveApiKey(); _rebuild = true; });
                    if (_settings.ApiKeyFromEnvironment)
                        Note("XAI_API_KEY is set and overrides the saved key. Restart the app without that variable to use the saved key.");
                    if (!S.PrivateMode)
                        Button(_settings.ConnectionTestRunning ? "Testing..." : "Test connection", () => { _settings.TestConnectionNow(); _rebuild = true; });
                }
                else
                {
                    BuildLocalOllama(!S.PrivateMode);
                }
            }
        }

        /// <summary>
        /// JUH-182: AI → Private model, used while Private mode is on. It can be set up in public
        /// mode too; the Ollama test runs only in Private mode, because it tests the active model.
        /// </summary>
        void BuildAiPrivate()
        {
            if (!S.PrivateMode)
            {
                Section("Private mode is off");
                Note("This model is used while Private mode is on. You can set it up now.");
            }

            {
                Section("Private endpoint");
                Note("Your normal provider choice is remembered for when Private mode is off.");
                var custom = PrivateEndpointTypes.IsCustom(S.PrivateEndpointType);
                Choice("Local Ollama", !custom, () =>
                {
                    Change(s => s.PrivateEndpointType = PrivateEndpointTypes.Local);
                    _rebuild = true;
                });
                Choice("Custom server", custom, () =>
                {
                    Change(s => s.PrivateEndpointType = PrivateEndpointTypes.Custom);
                    _rebuild = true;
                });

                if (!custom)
                {
                    BuildLocalOllama(S.PrivateMode);
                }
                else
                {
                    Section("Custom server");
                    Field("Custom base URL", S.PrivateBaseUrl, false, v => Change(s => s.PrivateBaseUrl = v));
                    Note("The OpenAI-compatible base URL, for example https://gpu.example.com/v1. vLLM and Ollama both work.");
                    Field("Custom model", S.PrivateModel, false, v => Change(s => s.PrivateModel = v));
                    if (string.IsNullOrWhiteSpace(S.PrivateBaseUrl))
                        Note("Enter your server's base URL above.");
                    else
                    {
                        var warning = PrivateEndpointRules.HttpWarning(S.PrivateBaseUrl);
                        if (!string.IsNullOrEmpty(warning))
                            Note(warning);
                        var addressError = PrivateEndpointRules.ValidateAddress(S.PrivateBaseUrl);
                        if (!string.IsNullOrEmpty(addressError))
                            Note(addressError);
                    }
                    if (string.IsNullOrWhiteSpace(S.PrivateModel))
                        Note("Enter the model name your server expects.");

                    Section("Custom server API key (optional)");
                    Note(_settings.PrivateApiKeySaved
                        ? "A private API key is saved in the encrypted local store."
                        : "No private API key is saved. Most self-hosted servers do not need one.");
                    var privateKey = Field("Paste a private API key", string.Empty, true, null);
                    Button("Save private key", () =>
                    {
                        _settings.SavePrivateEndpointKey(privateKey != null ? privateKey.text : string.Empty);
                        if (privateKey != null)
                            privateKey.text = string.Empty;
                        _rebuild = true;
                    });
                    Button("Remove private key", () => { _settings.RemovePrivateEndpointKey(); _rebuild = true; });
                    Button(_settings.PrivateEndpointTestRunning ? "Testing..." : "Test custom connection", () => { _settings.TestPrivateEndpointConnection(); _rebuild = true; });
                    Note("A self-hosted proxy that forwards to a commercial cloud API is not Private mode.");
                }
            }
        }

        /// <summary>JUH-182: AI → This persona. Moved here from the persona editor (JUH-121).</summary>
        void BuildAiPersona()
        {
            PersonaEntry current;
            if (!ActivePersona(_settings.PersonaCatalogNow(), out current))
                return;
            var persona = current.Persona;
            var id = persona.Id;
            Section("Model for this persona");
            Note("Overrides the Chat model page while " + (string.IsNullOrEmpty(persona.DisplayName) ? id : persona.DisplayName) + " is active.");
            Choice(ChatDefaults.CloudName, Same(persona.ProviderId, ChatDefaults.GrokId), () => Save(id, p => p.ProviderId = ChatDefaults.GrokId));
            Choice("Local Ollama", Same(persona.ProviderId, ChatDefaults.LocalId), () => Save(id, p => p.ProviderId = ChatDefaults.LocalId));
            Choice("Same as above", string.IsNullOrEmpty(persona.ProviderId), () => Save(id, p => p.ProviderId = string.Empty));
            Field("Model name (empty: same as above)", persona.Model, false, v => Save(id, p => p.Model = v));
        }

        void BuildLocalOllama(bool test)
        {
            Section("Local Ollama");
            Field("Local model", S.LocalModel, false, v => Change(s => s.LocalModel = v));
            Field("Local address", S.LocalBaseUrl, false, v => Change(s => s.LocalBaseUrl = v));
            Note("Default: Ollama at http://127.0.0.1:11434/v1. Nothing leaves this PC.");
            if (test)
                Button(_settings.ConnectionTestRunning ? "Testing..." : "Test local connection", () => { _settings.TestConnectionNow(); _rebuild = true; });
        }

        static string ProviderLabel(string name, bool remembered, bool overridden)
        {
            if (!remembered)
                return name;
            return overridden ? name + " (remembered)" : name + " (active)";
        }

        // ---- Voice -------------------------------------------------------------------------

        /// <summary>JUH-182: Voice → Speaking.</summary>
        void BuildVoice()
        {
            Section("Speaking");
            Note(VoiceRoute.UsesCloudTts(S.PrivateMode, S.ProviderId, S.BaseUrl)
                ? "Voice follows chat. Cloud replies from xAI use the xAI cloud voice."
                : "Voice follows chat. Local and private replies use Piper, Kokoro or Chatterbox and are not sent to cloud TTS.");
            Toggle("Speak replies", S.TtsEnabled, v => Change(s => s.TtsEnabled = v));
            Toggle("Voice narration", S.VoiceNarration, v => Change(s => s.VoiceNarration = v));
            Note("Off: lines between *asterisks* are shown but not spoken.");
            Section("Volume");
            Slider(v => "Volume " + Mathf.RoundToInt(v * 100f) + "%", S.Volume, 0f, 1f, false, v => P.Change(s => s.Volume = v));
            Toggle("Muted", S.Muted, v => Change(s => s.Muted = v));
            Note("The same switch as F6.");
        }

        /// <summary>JUH-182: Voice → Engine. Only the chosen local engine's files are shown.</summary>
        void BuildVoiceEngine()
        {
            var piper = string.Equals(S.TtsProvider, "piper", StringComparison.OrdinalIgnoreCase);
            var kokoro = string.Equals(S.TtsProvider, "kokoro", StringComparison.OrdinalIgnoreCase);
            var chatterbox = string.Equals(S.TtsProvider, "chatterbox", StringComparison.OrdinalIgnoreCase);
            Section("Local voice engine");
            Note("Speaks on this PC with no API key. Private mode always uses it.");
            Choice("Piper", piper, () => Change(s => s.TtsProvider = "piper"));
            Choice("Kokoro", kokoro, () => Change(s => s.TtsProvider = "kokoro"));
            Choice("Chatterbox", chatterbox, () => Change(s => s.TtsProvider = "chatterbox"));
            // The old switches' "off": only the cloud voice (SpeechPolicy keeps her quiet in Private mode then).
            Choice("None: cloud voice only", !piper && !kokoro && !chatterbox, () => Change(s => s.TtsProvider = "xai"));
            if (!piper && !kokoro && !chatterbox)
                Note("She speaks only with the cloud voice, and stays quiet in Private mode and with a local model.");

            if (kokoro)
            {
                Section("Kokoro");
                Dropdown("Kokoro voice", S.KokoroVoice, KokoroVoiceOptions(), v => v, v => Change(s => s.KokoroVoice = v));
                Field("Kokoro python", S.KokoroPython, false, v => Change(s => s.KokoroPython = v));
                Field("Kokoro script", S.KokoroScript, false, v => Change(s => s.KokoroScript = v));
                Field("Kokoro model", S.KokoroModel, false, v => Change(s => s.KokoroModel = v));
                Field("Kokoro voices", S.KokoroVoices, false, v => Change(s => s.KokoroVoices = v));
                // JUH-168: Kokoro runs on the processor; while a game is in front it gets fewer cores.
                Section("Kokoro in games");
                Toggle("Kokoro: fewer cores while a game is in front", S.KokoroGameLimit, v => Change(s => s.KokoroGameLimit = v));
                if (S.KokoroGameLimit)
                    Slider(v => "Kokoro cores in games: " + Mathf.RoundToInt(v), S.KokoroGameCores, 1f, Mathf.Min(KokoroCpuLimit.MaxCores, Mathf.Max(1, SystemInfo.processorCount)), true,
                        v => Change(s => s.KokoroGameCores = Mathf.RoundToInt(v)));
                Note("Kokoro runs on the processor and, left alone, keeps almost every core busy for each sentence. While a game is in front she uses only this many, at below-normal priority, so the PC's power draw doesn't jump on top of the game. With 2 she still speaks about twice as fast as real time, a second or two later per sentence. It needs the kokoro_server.py from this version (docs/OFFLINE_TTS.md).");
            }
            else if (chatterbox)
            {
                Section("Chatterbox");
                Dropdown("Chatterbox voice", S.ChatterboxVoice, ChatterboxVoiceOptions(false, S.ChatterboxVoice), v => v, v => Change(s => s.ChatterboxVoice = v));
                Field("Chatterbox device (auto/cuda/cpu)", S.ChatterboxDevice, false, v => Change(s => s.ChatterboxDevice = v));
                Field("Chatterbox voices folder", S.ChatterboxVoices, false, v => Change(s => s.ChatterboxVoices = v));
                Field("Chatterbox python", S.ChatterboxPython, false, v => Change(s => s.ChatterboxPython = v));
                Field("Chatterbox script", S.ChatterboxScript, false, v => Change(s => s.ChatterboxScript = v));
                Note("Slower than Kokoro but more expressive. It uses the GPU when device is cuda or auto; if a game is using most of the GPU, pick cpu or use Kokoro.");
            }
            else if (piper)
            {
                Section("Piper");
                Dropdown("Piper voice model", S.PiperVoice, PiperVoiceOptions(), FileLabel, v => Change(s => s.PiperVoice = v));
                Field("Piper exe", S.PiperExe, false, v => Change(s => s.PiperExe = v));
            }

            Section("Cloud voice (xAI)");
            Field("Cloud voice (xAI)", S.VoiceId, false, v => Change(s => s.VoiceId = v));
            Note("Used only in public mode with xAI as the cloud AI API.");
            Section("Language");
            Field("TTS language", S.TtsLanguage, false, v => Change(s => s.TtsLanguage = v));
        }

        /// <summary>JUH-182: Voice → This persona. Moved here from the persona editor (JUH-121).</summary>
        void BuildVoicePersona()
        {
            PersonaEntry current;
            if (ActivePersona(_settings.PersonaCatalogNow(), out current))
            {
                var persona = current.Persona;
                var id = persona.Id;
                Section("Voice for this persona");
                Field("Piper voice id (local)", persona.PiperVoiceId, false, v => Save(id, p => p.PiperVoiceId = v));
                Field("Kokoro voice id (local)", persona.KokoroVoiceId, false, v => Save(id, p => p.KokoroVoiceId = v));
                Dropdown("Chatterbox voice id (local)", persona.ChatterboxVoiceId, ChatterboxVoiceOptions(true, persona.ChatterboxVoiceId),
                    v => string.IsNullOrEmpty(v) ? "(same as the Engine page)" : v,
                    v => Save(id, p => p.ChatterboxVoiceId = v));
                Field("Cloud voice id (xAI)", persona.CloudVoiceId, false, v => Save(id, p => p.CloudVoiceId = v));
                Note("Empty keeps the Engine page's voice.");
            }
        }

        /// <summary>JUH-182: Voice → Listening. Push-to-talk and the speech model.</summary>
        void BuildListening()
        {
            Section("Push-to-talk");
            Toggle("Push-to-talk", S.SttEnabled, v => Change(s => s.SttEnabled = v));
            Field("Push-to-talk key", S.PushToTalkKey, false, v => Change(s => s.PushToTalkKey = v));
            Note("Taken keys (F6, F7, F8, F10, F12, T, R, Esc) stay on V.");
            Section("Speech recognition");
            Field("Whisper model", S.WhisperModel, false, v =>
            {
                Change(s => s.WhisperModel = v);
                _settings.SetStatusMessage("The Whisper model applies on the next recording. The .bin file is not part of the app.");
            });
            Field("Speech language", S.SttLanguage, false, v => Change(s => s.SttLanguage = v));

            // JUH-165: where the Whisper model is, a file of your own, and a download when it's missing.
            Section("Speech model file");
            Note(_settings.SpeechModelStatus());
            Field("Speech model file (optional)", S.WhisperModelPath, false, v => Change(s => s.WhisperModelPath = v));
            Button("Browse for the speech model…", () =>
            {
                string picked;
                if (OpenFileDialog.TryPick("Choose the Whisper model (.bin)", OpenFileDialog.WhisperModels, out picked))
                {
                    Change(s => s.WhisperModelPath = picked);
                    _rebuild = true;
                }
            });
            if (!string.IsNullOrEmpty(S.WhisperModelPath))
                Button("Clear the chosen file", () => { Change(s => s.WhisperModelPath = string.Empty); _rebuild = true; });
            Note("Pick a ggml-….bin you already have anywhere on this PC. Empty looks in the build, then in your data folder.");
            if (_settings.SpeechModelDownloading)
            {
                Note("Downloading the speech model… The status line says when it's ready.");
                Button("Cancel download", () => { _settings.CancelSpeechModelDownload(); _rebuild = true; });
            }
            else if (!_settings.SpeechModelFound())
            {
                var big = string.Equals(SttSettings.SanitizeModelFile(S.WhisperModel), SttSettings.DefaultModelFile, StringComparison.OrdinalIgnoreCase);
                Button(big ? "Download speech model (148 MB)" : "Download speech model", () => { _settings.DownloadSpeechModel(); _rebuild = true; });
                Note("It's saved in your data folder, %APPDATA%\\ProjectLeia\\Whisper, so every Happi build finds it. A plain download from Hugging Face; nothing about you is sent.");
            }
        }

        /// <summary>JUH-182: Voice → In games. JUH-162: push-to-talk while a game has the focus.</summary>
        void BuildTalkWhileGaming()
        {
            Section("Mouse button");
            Note("Hold one of these in a game, talk, and let go. Happi only reads the button, so the game still gets it.");
            foreach (var button in new[] { TalkMouseButton.Mouse4, TalkMouseButton.Mouse5, TalkMouseButton.Off })
            {
                var choice = button;
                Choice(GameTalkButtons.Label(choice), GameTalkButtons.Normalize(S.TalkMouseButton) == choice,
                    () => Change(s => s.TalkMouseButton = (int)choice));
            }

            Section("Controller");
            Toggle("Xbox button (Xbox controller)", S.TalkXboxButton, v => Change(s => s.TalkXboxButton = v));
            Toggle("PS button (DualSense)", S.TalkPsButton, v => Change(s => s.TalkPsButton = v));
            Note("The Xbox button also opens the Xbox Game Bar: turn that off in Windows Settings > Gaming > Game Bar. "
                 + "With Steam running, the PS button can open the Steam overlay: turn off its Guide button in Steam's controller settings. "
                 + "A game running as administrator hides its input from Happi, so then run Happi as administrator too.");
        }

        // ---- Look: the avatar ---------------------------------------------------------------

        void BuildLook()
        {
            Section("Avatar");
            var video = AvatarModeNames.IsVideo(S.AvatarMode);
            Choice("3D model", !video, () => { P.SetAvatarMode(AvatarModeNames.ThreeD); _rebuild = true; });
            Choice("2D video", video, () => { P.SetAvatarMode(AvatarModeNames.Video); _rebuild = true; });
            Note(video
                ? "Her clips and their size are on the 2D video page."
                : "Her model is on the 3D model page. 2D video loops idle.mp4 or idle.webm from %APPDATA%\\ProjectLeia\\video-avatar.");
        }

        /// <summary>JUH-182: Look → 2D video.</summary>
        void BuildVideoPage()
        {
            if (!AvatarModeNames.IsVideo(S.AvatarMode))
            {
                Section("2D video is off");
                Note("Pick 2D video on the Avatar page to set up her clips.");
                return;
            }

            BuildVideo();
        }

        /// <summary>JUH-182: Look → 3D model.</summary>
        void BuildLook3d()
        {
            Section("3D avatar");
            Source("Auto", AvatarSourceNames.Auto);
            Source("Local prefab", AvatarSourceNames.Local);
            Source("VRM sample", AvatarSourceNames.Vrm);
            Source("FBX sample", AvatarSourceNames.Fbx);
            Section("Expression portrait");
            Toggle("Show expression portrait", S.ShowExpressionPortrait, v => Change(s => s.ShowExpressionPortrait = v));
            Note("Images load from the persona folder, or %APPDATA%\\ProjectLeia\\portraits.");
            Section("Movement");
            Button("Reset rotation", () => { _settings.ResetAvatarRotation(); _rebuild = true; });
            Toggle("Look at cursor", S.CursorLookAt, v => Change(s => s.CursorLookAt = v));
            Note("Eyes and head follow the desktop cursor, and ease back when it is still or far away.");

            // Moved here from the persona editor (JUH-121).
            PersonaEntry current;
            if (!ActivePersona(_settings.PersonaCatalogNow(), out current))
                return;
            var persona = current.Persona;
            var id = persona.Id;
            Section("3D avatar for this persona");
            Choice("Same as above", string.IsNullOrEmpty(persona.AvatarSource), () => Save(id, p => p.AvatarSource = string.Empty));
            Choice("Auto", Same(persona.AvatarSource, AvatarSourceNames.Auto), () => Save(id, p => p.AvatarSource = AvatarSourceNames.Auto));
            Choice("Local prefab", Same(persona.AvatarSource, AvatarSourceNames.Local), () => Save(id, p => p.AvatarSource = AvatarSourceNames.Local));
            Choice("VRM sample", Same(persona.AvatarSource, AvatarSourceNames.Vrm), () => Save(id, p => p.AvatarSource = AvatarSourceNames.Vrm));
            Choice("FBX sample", Same(persona.AvatarSource, AvatarSourceNames.Fbx), () => Save(id, p => p.AvatarSource = AvatarSourceNames.Fbx));
        }

        void BuildVideo()
        {
            var player = GetComponent<VideoAvatar>();
            Section("Her clips");
            _videoStatus = Note(player != null ? player.StatusText : "The video avatar is not running.");
            Field("Video folder (empty uses the default)", S.VideoAvatarFolder, false, v => Change(s => s.VideoAvatarFolder = v));
            Button("Reload video", () => { var v = GetComponent<VideoAvatar>(); if (v != null) v.Reload(); _settings.SetStatusMessage("Looking for the clip again."); });
            Note("Put idle.mp4 with idle.mask.mp4 in the folder. Optional: idle variations, walk and walk_stop clips with their masks, and video-avatar.json to list them. See docs/VIDEO_AVATAR.md.");
            Section("Size and position");
            Slider(v => "Video size " + Mathf.RoundToInt(v * 100f) + "%", S.VideoAvatarSize, VideoAvatarLayout.MinSize, VideoAvatarLayout.MaxSize, false, v => P.Change(s => s.VideoAvatarSize = v));
            Slider(v => "Video left or right " + Mathf.RoundToInt(v * 100f) + "%", S.VideoAvatarOffsetX, -VideoAvatarLayout.MaxOffsetX, VideoAvatarLayout.MaxOffsetX, false, v => P.Change(s => s.VideoAvatarOffsetX = v));
            Slider(v => "Video up or down " + Mathf.RoundToInt(v * 100f) + "%", S.VideoAvatarOffsetY, -VideoAvatarLayout.MaxOffsetY, VideoAvatarLayout.MaxOffsetY, false, v => P.Change(s => s.VideoAvatarOffsetY = v));
            Button("Reset video size and position", () => Change(s =>
            {
                s.VideoAvatarSize = VideoAvatarLayout.DefaultSize;
                s.VideoAvatarOffsetX = 0f;
                s.VideoAvatarOffsetY = 0f;
            }));
            Note("The window size is on the Window page.");
            Section("Walking");
            Slider(v => v < 0.02f ? "Walk speed: from the walk clip" : "Walk speed " + v.ToString("0.00", CultureInfo.InvariantCulture) + " clip heights per second",
                S.VideoAvatarWalkSpeed, 0f, 1f, false, v => P.Change(s => s.VideoAvatarWalkSpeed = v < 0.02f ? 0f : v));
            Section("Clip sounds");
            Toggle("Play clip sounds", S.PlayClipSounds, v => Change(s => s.PlayClipSounds = v));
            Slider(v => "Clip sound volume " + Mathf.RoundToInt(v * 100f) + "%", S.ClipSoundVolume, 0f, 1f, false, v => P.Change(s => s.ClipSoundVolume = v));
            Note("Clip sounds are her own sounds in clips marked \"sound\": true in video-avatar.json. They follow Mute and stay silent while she speaks.");
        }

        /// <summary>The 2D video test buttons, on the Developer tab (JUH-121).</summary>
        void BuildVideoTests()
        {
            Section("2D video tests");
            if (!AvatarModeNames.IsVideo(S.AvatarMode))
            {
                Note("Switch the avatar to 2D video on Look → Avatar to test clips.");
                return;
            }

            var undressed = IntimateExpressions.Undressed;
            if (!undressed)
            {
                Button("Test: walk left", () => VideoTest(a => a.TestWalk(-1f)));
                Button("Test: walk right (mirrored)", () => VideoTest(a => a.TestWalk(1f)));
                Button("Test: stop", () => VideoTest(a => a.TestStop()));
                Button("Test: back to the front idle", () => VideoTest(a => a.TestFront()));
                var reactions = VideoAvatarPresenter.TestReactions;
                for (var i = 0; i < reactions.Count; i++)
                {
                    var name = reactions[i].Value;
                    Button(reactions[i].Key, () => VideoReactionTest(name));
                }
            }

            if (IntimateExpressions.Active)
            {
                var adult = VideoAvatarPresenter.AdultTestReactions;
                for (var i = 0; i < adult.Count; i++)
                {
                    var name = adult[i].Value;
                    if (undressed && !IntimateExpressions.IsNakedCue(name))
                        continue;
                    Button(adult[i].Key, () => VideoReactionTest(name));
                }

                if (undressed)
                    Button("Test: dress", () => VideoReactionTest("dress"));
            }

            Note("With settings closed and the window focused, the arrow keys do the same: left and right walk, down stops, up returns to the front idle.");
        }

        void VideoTest(Action<VideoAvatar> action)
        {
            var player = GetComponent<VideoAvatar>();
            if (player == null || player.State != VideoAvatarState.Playing)
            {
                _settings.SetStatusMessage(VideoAvatarPresenter.NotPlaying);
                return;
            }

            action(player);
        }

        void VideoReactionTest(string name)
        {
            VideoTest(a =>
            {
                var result = a.RequestReaction(name);
                Debug.Log("Video test: " + name + " -> " + result);
                var status = VideoAvatarPresenter.ReactionStatus(result, name, VideoAvatarDirector.PendingSeconds);
                if (status.Length > 0)
                    _settings.SetStatusMessage(status);
                _rebuild = true;
            });
        }

        void Source(string label, string source)
        {
            Choice(label, S.AvatarSource == source, () => Change(s => s.AvatarSource = source));
        }

        // ---- Chat: the chat box and its history ----------------------------------------------

        // ---- Tools: desktop permissions (JUH-27) -----------------------------------------

        void BuildTools()
        {
            Section("Desktop tools");
            Note("Every capability is off until you grant it. A tool asks the first time it is used, and you can revoke it here at any time.");
            Note("In chat, type /search <query> to ask for current web context. The first search asks for Web search permission.");

            for (var i = 0; i < DesktopToolCatalog.All.Length; i++)
            {
                var kind = DesktopToolCatalog.All[i];
                var state = DesktopToolPermissions.State(S, kind);
                Section(DesktopToolCatalog.Label(kind));
                Label(DesktopToolPermissions.StateLabel(state));
                Note(DesktopToolCatalog.Description(kind));
                Button("Allow " + DesktopToolCatalog.Label(kind), () => SetToolPermission(kind, DesktopToolPermissions.Allowed));
                Button("Ask on first use", () => SetToolPermission(kind, DesktopToolPermissions.Ask));
                Button("Block", () => SetToolPermission(kind, DesktopToolPermissions.Denied));
                Button("Preview first-use prompt", () => DesktopToolPermissions.Request(_settings, kind, "preview", _ => { _rebuild = true; }));
            }

            Section("Web search test");
            Note("Use this to check the search connection and permission. Searches are cached and rate-limited; nothing runs in the background.");
            Field("Search query", _webSearchQuery, false, v => _webSearchQuery = v ?? string.Empty);
            Button("Search now", () =>
            {
                if (string.IsNullOrWhiteSpace(_webSearchQuery))
                {
                    _webSearchStatus = "Type a search query first.";
                    _rebuild = true;
                    return;
                }

                _webSearchStatus = "Searching...";
                _rebuild = true;
                WebSearchTool.Request(_settings, _webSearchQuery, response =>
                {
                    if (!response.Ok)
                    {
                        _webSearchStatus = response.Error;
                    }
                    else
                    {
                        var text = new StringBuilder();
                        text.Append(response.FromCache ? "Cached results" : "Fresh results");
                        text.Append(" for \"").Append(response.Query).Append("\":");
                        for (var i = 0; i < response.Results.Count && i < 3; i++)
                            text.Append("\n• ").Append(response.Results[i].Title);
                        _webSearchStatus = text.ToString();
                    }

                    _rebuild = true;
                });
            });
            if (!string.IsNullOrEmpty(_webSearchStatus))
                Note(_webSearchStatus);

            Section("Tool action log");
            var recent = DesktopToolAuditLog.Recent(8);
            if (recent.Count == 0)
            {
                Note("No tool actions yet.");
            }
            else
            {
                for (var i = 0; i < recent.Count; i++)
                {
                    var entry = recent[i];
                    DateTime at;
                    var when = DateTime.TryParse(entry.atUtc, out at) ? at.ToLocalTime().ToString("g") : string.Empty;
                    Note((when.Length > 0 ? when + " — " : string.Empty)
                        + entry.tool + ": " + entry.action + " — " + (entry.allowed ? "allowed" : "blocked")
                        + (string.IsNullOrEmpty(entry.reason) ? string.Empty : " (" + entry.reason + ")"));
                }
            }

            Button("Clear tool action log", () =>
            {
                DesktopToolAuditLog.Clear();
                _rebuild = true;
            }, true);
        }

        void SetToolPermission(DesktopToolKind kind, int state)
        {
            Change(s => DesktopToolPermissions.SetState(s, kind, state));
        }

        /// <summary>JUH-182: Chat → Replies.</summary>
        void BuildChat()
        {
            Section("Replies");
            var chat = GetComponent<HappiChatView>();
            if (chat != null && chat.Player != null)
            {
                var step = chat.Player.Mode == VisualNovel.VnMode.Step;
                Toggle("Step through replies", step, v =>
                {
                    chat.Player.Mode = v ? VisualNovel.VnMode.Step : VisualNovel.VnMode.Auto;
                    PlayerPrefs.SetInt(HappiChatView.ModePref, v ? 1 : 0);
                    _rebuild = true;
                });
                Note("Step: Next, a click on the dialogue or Space shows one line at a time. Auto plays the whole reply.");
            }

            Section("Spoken text glow");
            Toggle("Spoken text glow", S.SpokenGlow, v =>
            {
                _settings.SaveLayoutSetting(s => s.SpokenGlow = v);
                _rebuild = true;
            });
            Note("The words light up warmly as she says them. Narration glows only when Voice narration is on.");
            if (S.SpokenGlow)
                Slider(v => "Spoken text glow intensity: " + (v < 0.34f ? "subtle" : v < 0.67f ? "medium" : "strong"), S.SpokenGlowIntensity, 0f, 1f, false,
                    v => _settings.SaveLayoutSetting(s => s.SpokenGlowIntensity = v));

            // JUH-122: floating pieces near her when how your message landed moves the relationship.
            Section("Relationship changes");
            Toggle("Show relationship changes while chatting", S.ShowRelationshipFeedback, v => Change(s => s.ShowRelationshipFeedback = v));
            Note("A small heart floats up beside her when a message moves how much she likes you; a critical hit pops out big and gold.");
        }

        /// <summary>JUH-182: Chat → Text and box.</summary>
        void BuildChatText()
        {
            Section("Text style");
            Dropdown("Chat font", S.ChatFontFamily, ChatFontFamilies(), v => v, v =>
            {
                Change(s => s.ChatFontFamily = v);
                ApplyChatFont();
            });
            Dropdown("Chat text size", Mathf.RoundToInt(S.ChatFontSize).ToString(), ChatFontSizes(), v => v + " px", v =>
            {
                int size;
                if (!int.TryParse(v, out size))
                    return;
                Change(s => s.ChatFontSize = size);
                ApplyChatFont();
            });
            Button("Reset chat font and size", () =>
            {
                Change(s =>
                {
                    s.ChatFontFamily = HappiTypeface.Family;
                    s.ChatFontSize = HappiTypeface.ChatSize;
                });
                ApplyChatFont();
            });
            Note("Applies to the dialogue, transcript and message box.");

            Section("Chat box");
            Note("Drag the chat box by its top bar to put it anywhere around her. While you drag, spots appear: 1 left of her head, 2 right of her head (the portrait moves to her left), 3 beside her on the left, 4 beside her on the right, 5 under her, so she stands on it (2D video avatar only). Let go near one to snap there. The box stays with her when she walks.");
            // JUH-127: the box is resized by dragging its edges and corners; the old height slider is gone.
            Note("Drag any edge or corner of the chat box to resize it, like a window. Hold the middle mouse button and drag over the chat to scroll back through the conversation.");
            Button("Reset chat box size", () =>
            {
                _settings.SaveLayoutSetting(s =>
                {
                    s.ChatBoxWidthPx = 0f;
                    s.ChatBoxHeightPx = 0f;
                    s.ChatBoxHeight = 1f;
                });
                _settings.SetStatusMessage("The chat box is back to its automatic size.");
            });
            Button("Reset chat position", () =>
            {
                var view = GetComponent<HappiChatView>();
                if (view != null)
                    view.ResetPosition();
                else
                    _settings.SaveLayoutSetting(s =>
                    {
                        s.ChatBoxSnap = -1;
                        s.ChatBoxOffsetX = 0f;
                        s.ChatBoxOffsetY = 0f;
                        s.ChatBoxX = -1f;
                        s.ChatBoxY = -1f;
                    });
                _settings.SetStatusMessage("The chat box is back in its default place.");
            });

            Section("Portrait");
            Note("Drag the 2D portrait to move it. Let go near the chat box's left or right side to dock it there; anywhere else it stays where you put it, following her.");
            Note("Drag the portrait's edges or corners to resize it. Its largest size is the source image size, so it stays sharp.");
            Button("Reset portrait size", () =>
            {
                _settings.SaveLayoutSetting(s =>
                {
                    s.PortraitWidthPx = 0f;
                    s.PortraitHeightPx = 0f;
                });
                _settings.SetStatusMessage("The portrait is back to its automatic size.");
            });
            Button("Reset portrait position", () =>
            {
                _settings.SaveLayoutSetting(s =>
                {
                    s.PortraitDock = 0;
                    s.PortraitOffsetX = 0f;
                    s.PortraitOffsetY = 0f;
                });
                _settings.SetStatusMessage("The portrait is back beside her head.");
            });
        }

        /// <summary>JUH-182: Chat → History.</summary>
        void BuildChatHistory()
        {
            Section("Chat history");
            Toggle("Save chat history", S.ChatHistory, v => { _settings.SetChatHistory(v); _rebuild = true; });
            Note("Off keeps the transcript for this session only. On saves it on this PC under ProjectLeia.");
            Button("Clear chat history", () => { _confirm.Request(PrivacyConfirmKind.ClearHistory); _rebuild = true; });
        }

        // ---- Window: the overlay window itself ---------------------------------------------

        /// <summary>JUH-182: Look → Window.</summary>
        void BuildWindow()
        {
            Section("On top");
            Toggle("Always on top", S.AlwaysOnTop, v => Change(s => s.AlwaysOnTop = v));
            Note("On top of every window on the desktop. F8 toggles this too.");
            // JUH-164: games keep her on top even with "Always on top" off.
            Toggle("Stay above games", S.StayAboveGames, v => Change(s => s.StayAboveGames = v));
            Note("While a game is in front (borderless or windowed fullscreen, or a game she's watching), she stays on top of it, even with Always on top off. "
                 + "Windows can't draw anything over a game's exclusive fullscreen, so pick borderless or windowed fullscreen in the game.");
            Section("Size");
            Slider(v => "Size " + Mathf.RoundToInt(v * 100f) + "%", S.Scale, OverlayWindowSize.MinScale, OverlayWindowSize.MaxScale, false, v => P.Change(s => s.Scale = v));
            Section("Click-through");
            Button("Click-through: " + SettingsPresenter.ClickThroughLabel(S.ClickThrough), () => { P.CycleClickThrough(); _rebuild = true; });
            Note(SettingsPresenter.ClickThroughHelp(S.ClickThrough));
            Slider(v => "Idle frame rate " + Mathf.RoundToInt(v), S.IdleFrameRate, 1f, OverlayFrameRate.Active, true, v => P.Change(s => s.IdleFrameRate = Mathf.RoundToInt(v)));
            Note("Used a few seconds after the pointer leaves the window.");
            Section("Windows");
            Toggle("Hide taskbar button", S.HideTaskbarButton, v => Change(s => s.HideTaskbarButton = v));
            Toggle("Start with Windows", S.StartWithWindows, v => Change(s => s.StartWithWindows = v));
            Note("Takes effect the next time you sign in.");
            Note("Pause her from the wheel (Pause her) or the tray.");
        }

        // ---- Data: export, wipe, setup -----------------------------------------------------

        void BuildData()
        {
            Section("Export");
            Note("Export writes a decrypted copy, including chat, memories, and the API key if one is saved. Keep that folder private.", true);
            Field("Export folder", _exportFolder, false, v => _exportFolder = v ?? string.Empty);
            Button("Choose export folder", () =>
            {
                string picked;
                if (ExportFolderDialog.TryPick(out picked) && !string.IsNullOrWhiteSpace(picked))
                    _exportFolder = picked.Trim();
                else
                    _settings.SetStatusMessage("Type an export folder. The folder window is unavailable here.");
                _rebuild = true;
            });
            Button("Export decrypted folder", () => _settings.ExportLocalData(_exportFolder, false));
            Button("Export decrypted zip", () => _settings.ExportLocalData(_exportFolder, true));

            Section("Start again");
            Button("Run setup again", () => { Close(); _settings.RequestSetupReplay(); });
            Note("Opens the first-run setup again. Skip on a step uses that step's default. Finish saves.");
            Button("Wipe all local data", () => { _confirm.Request(PrivacyConfirmKind.WipeAll); _rebuild = true; }, true);

            Section("Developer");
            Toggle("Show developer settings", S.ShowDeveloperSettings, v => { Change(s => s.ShowDeveloperSettings = v); _layoutW = -1f; _rebuild = true; });
            Note("Adds a Developer page under App, with test buttons and debug readouts.");
        }

        // ---- 18+: where the gate stands, and the switch -------------------------------------

        void BuildAdult()
        {
            Section("Is 18+ on?", true);
            Label(IntimateExpressions.Active ? "18+ is on." : "18+ is off.");
            Label((S.PrivateMode ? "Done: " : "Needed: ") + "Private mode (AI tab)");
            Label((IntimateExpressions.FolderPresent ? "Done: " : "Needed: ") + "the adult portraits folder, %APPDATA%\\ProjectLeia\\portraits-adult");
            Label((S.IntimateExpressions ? "Done: " : "Needed: ") + "the switch below");
            // Same condition as before (JUH-104): always listed, works only with Private mode and the adult folder.
            Section("Intimate expressions", true);
            Toggle("Intimate expressions (18+)", S.IntimateExpressions, v => { Change(s => s.IntimateExpressions = v); _rebuild = true; });
            Note("Adult drawn expressions from %APPDATA%\\ProjectLeia\\portraits-adult. Works only in Private mode (local model). Off by default; they are not shown or offered to the model.");
            Note(IntimateExpressions.Active
                ? "Her innocence, libido, 18+ secrets and intimate voice are on the other pages of this tab."
                : "Her innocence, libido, 18+ secrets and intimate voice appear on this tab once 18+ is on.");
        }

        // ---- Keys --------------------------------------------------------------------------

        /// <summary>JUH-182: moved from Keys to AI → Pisara.</summary>
        void BuildPisaraKey()
        {
            Section("Pisara API key");
            Note(ApiKeyMask.Label(_settings.PisaraApiKeySaved) + ".");
            var pisaraKey = Field("Paste a new Pisara API key", string.Empty, true, null);
            Button("Save Pisara key", () =>
            {
                _settings.SavePisaraApiKey(pisaraKey != null ? pisaraKey.text : string.Empty);
                if (pisaraKey != null)
                    pisaraKey.text = string.Empty;
                _rebuild = true;
            });
            Button("Remove Pisara key", () => { _settings.RemovePisaraApiKey(); _rebuild = true; });
            if (_settings.PisaraApiKeyFromEnvironment)
                Note("PISARA_API_KEY is set and overrides the saved Pisara key.");
            Note("If no Pisara key is set, the cloud AI API key is used for Pisara readings when both use the same service.");
            Note("Set the Pisara base URL too when this key is for another service.");
        }

        /// <summary>JUH-182: App → Keys. The hotkeys and the mouse, read only.</summary>
        void BuildKeys()
        {
            Section("Keyboard");
            Note(HotkeyCatalog.RebindNote);
            var entries = HotkeyCatalog.Current(S.PushToTalkKey);
            for (var i = 0; i < entries.Length; i++)
                Label(entries[i].Keys + "   " + entries[i].Action);
            Section("Mouse");
            Label("Right-click her   Radial wheel");
            Label("Right-click head   Menu");
            Label("Shift + right-click her   Menu");
            Label("Right-click hand, lips or face   Quick touch");
            Label("Space / click (Step mode)   Next chat line");
        }

        // ---- Developer (hidden unless "Show developer settings" is on) -----------------------

        void BuildDeveloper()
        {
            Section("Readouts");
            Toggle("Show debug info", S.ShowDebugInfo, v => Change(s => s.ShowDebugInfo = v));
            Note("Top-left: topmost, frame rate, emotion, key hints, rotate, size and zoom. The hotkeys work either way.");
            Toggle("Show mood readout", S.ShowMoodDebug, v => Change(s => s.ShowMoodDebug = v));
            Note("Her current mood and the three strongest moods, top left. Mood changes are also in Player.log.");
            Section("Generation");
            Field("Timeout seconds", S.TimeoutSeconds.ToString("0.###", CultureInfo.InvariantCulture), false, v =>
            {
                float parsed;
                if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                    Change(s => s.TimeoutSeconds = parsed);
                else
                    _rebuild = true;
            });
            Note("How long one reply may take before the request is cancelled.");
            BuildVideoTests();
        }

        bool _correctingRelationship;

        /// <summary>
        /// JUH-116: Innocent to Perverted. Only while the 18+ gate is open; nothing about it is
        /// shown otherwise. Every change applies at once.
        /// </summary>
        /// <summary>
        /// JUH-171: how she talks, the last Pisara reading. Every field is editable; what the user
        /// writes wins, and an emptied field goes back to the reading. Lists are split by ";".
        /// The 18+ part shows only while the gate is open.
        /// </summary>
        void BuildHowSheTalks()
        {
            var keeper = _settings.Voice;
            if (keeper == null || !keeper.IsLoaded)
            {
                Note("Not read yet.");
                return;
            }

            Section("The reading");
            Note("Read last, from her description and everything the other readings found. Your text wins; empty a field to go back to the reading. Lists are split by \";\".");
            Note(keeper.EverydayStatus);
            Button(keeper.ReadingEveryday || keeper.ReadingIntimate ? "Read again (reading...)" : "Read again", () => { _settings.ReadVoiceAgain(); _rebuild = true; });
            Button("Use the reading everywhere", () => { keeper.UseReading(); _rebuild = true; });

            var file = keeper.File;
            var read = file.everyday;
            var user = file.UserEveryday;
            Section("Her voice");
            VoiceField("Overall", user.summary, read.summary, v => keeper.EditEveryday(u => u.summary = v));
            VoiceField("Register", user.register, read.register, v => keeper.EditEveryday(u => u.register = v));
            VoiceField("Rhythm", user.rhythm, read.rhythm, v => keeper.EditEveryday(u => u.rhythm = v));
            VoiceField("Words", user.vocabulary, read.vocabulary, v => keeper.EditEveryday(u => u.vocabulary = v));
            VoiceList("Habits", user.habits, read.habits, VoiceSheet.MaxItems, v => keeper.EditEveryday(u => u.habits = v));
            VoiceList("Never", user.never, read.never, VoiceSheet.MaxItems, v => keeper.EditEveryday(u => u.never = v));
            Section("By mood");
            VoiceField("When happy", user.happy, read.happy, v => keeper.EditEveryday(u => u.happy = v));
            VoiceField("When annoyed", user.annoyed, read.annoyed, v => keeper.EditEveryday(u => u.annoyed = v));
            VoiceField("When flustered", user.flustered, read.flustered, v => keeper.EditEveryday(u => u.flustered = v));
            VoiceField("When sad", user.sad, read.sad, v => keeper.EditEveryday(u => u.sad = v));
            VoiceField("When affectionate", user.affectionate, read.affectionate, v => keeper.EditEveryday(u => u.affectionate = v));
            Section("By closeness");
            VoiceField("With a stranger", user.stranger, read.stranger, v => keeper.EditEveryday(u => u.stranger = v));
            VoiceField("With someone close", user.close, read.close, v => keeper.EditEveryday(u => u.close = v));
            // Her own lines per mood: chat shows two at a time, rotating; a used line retires and
            // Pisara writes new ones. Lines you write here never retire.
            Section("Her lines");
            Note("Up to 3 per mood. Chat shows 2 at a time, rotating. The model's lines retire after a few replies and new ones are written; yours stay.");
            for (var k = 0; k < VoiceLines.Keys.Length; k++)
            {
                var key = VoiceLines.Keys[k];
                VoiceList("Lines: " + key, user.lines.Get(key), read.lines.Get(key), VoiceSheet.MaxLinesPerMood, v => keeper.EditEveryday(u => u.lines.Set(key, v)));
            }
        }

        /// <summary>JUH-182: 18+ → How she talks. Only while the gate is open.</summary>
        void BuildIntimateTalk()
        {
            var keeper = _settings.Voice;
            if (!IntimateExpressions.Active || keeper == null || !keeper.IsLoaded)
            {
                Note("Not read yet.");
                return;
            }

            var file = keeper.File;
            Section("How she talks in intimate scenes", true);
            Note("Read on the local model only, again when her innocence moves into a new band. " + keeper.IntimateStatus);
            var readIntimate = file.intimate;
            var userIntimate = file.UserIntimate;
            Button(keeper.ReadingEveryday || keeper.ReadingIntimate ? "Read again (reading...)" : "Read again", () => { _settings.ReadVoiceAgain(); _rebuild = true; });
            Section("Her voice", true);
            VoiceField("Holding back", userIntimate.holdingBack, readIntimate.holdingBack, v => keeper.EditIntimate(u => u.holdingBack = v));
            VoiceField("Letting go", userIntimate.lettingGo, readIntimate.lettingGo, v => keeper.EditIntimate(u => u.lettingGo = v));
            VoiceList("Habits", userIntimate.habits, readIntimate.habits, VoiceSheet.MaxItems, v => keeper.EditIntimate(u => u.habits = v));
            VoiceList("Never", userIntimate.never, readIntimate.never, VoiceSheet.MaxItems, v => keeper.EditIntimate(u => u.never = v));
            Section("Her lines", true);
            VoiceList("Lines: holding back", userIntimate.lines.Get("holdingBack"), readIntimate.lines.Get("holdingBack"), VoiceSheet.MaxLinesPerMood,
                v => keeper.EditIntimate(u => u.lines.Set("holdingBack", v)));
            VoiceList("Lines: letting go", userIntimate.lines.Get("lettingGo"), readIntimate.lines.Get("lettingGo"), VoiceSheet.MaxLinesPerMood,
                v => keeper.EditIntimate(u => u.lines.Set("lettingGo", v)));
        }

        /// <summary>Shows the user's text, or the reading's; typing the reading's text back keeps no override.</summary>
        void VoiceField(string caption, string user, string read, Action<string> set)
        {
            var shown = string.IsNullOrEmpty(user) ? read : user;
            Field(caption, shown, false, v =>
            {
                var typed = VoiceText.Line(v);
                set(string.Equals(typed, read, StringComparison.Ordinal) ? string.Empty : typed);
            });
        }

        void VoiceList(string caption, string[] user, string[] read, int max, Action<string[]> set)
        {
            var readText = VoiceText.Join(read);
            var shown = user != null && user.Length > 0 ? VoiceText.Join(user) : readText;
            Field(caption, shown, false, v =>
            {
                var items = VoiceText.Split(v, max, max == VoiceSheet.MaxLinesPerMood ? VoiceSheet.MaxLine : VoiceSheet.MaxItem);
                set(string.Equals(VoiceText.Join(items), readText, StringComparison.Ordinal) ? new string[0] : items);
            });
        }

        void BuildInnocence()
        {
            if (!IntimateExpressions.Active)
                return;
            var keeper = _settings.Innocence;
            if (keeper == null || !keeper.IsLoaded)
            {
                Note("Not read yet.");
                return;
            }

            var state = keeper.State;
            Section("Innocent to Perverted", true);
            Slider(v => "Innocence " + Mathf.RoundToInt(v) + ": " + InnocenceMath.BandName(v) + " (0 innocent, 100 perverted)",
                state.Current, InnocenceMath.Min, InnocenceMath.Max, true, v => _settings.SetInnocence(v));
            Note(state.UserSet ? "Your setting. A new reading of her description does not change it." : keeper.ReadingLine());
            if (Mathf.Abs(state.Current - state.Baseline) >= 1f)
                Note("Intimate moments have moved her to " + Mathf.RoundToInt(state.Current) + ". Without them she slowly drifts back toward "
                    + Mathf.RoundToInt(state.Baseline) + ".");
            Note("It changes how often she makes the first move, how comfortable she is undressed, and how intimate moments make her feel.");
            if (state.UserSet)
                Button("Use the reading", () => { _settings.UseInnocenceReading(); _rebuild = true; });
            Button(keeper.Reading ? "Read again (reading...)" : "Read again", () => { _settings.ReadInnocenceAgain(); _rebuild = true; });

            // JUH-170: libido is read with innocence; desire moves on its own and is only shown.
            var desire = _settings.Desire;
            if (desire != null && desire.IsLoaded)
            {
                Section("Libido and desire", true);
                var read = desire.ReadLibido();
                Slider(v => "Libido " + Mathf.RoundToInt(v) + ": " + DesireMath.LibidoName(v) + " (0 rarely wants it, 100 relentless)",
                    desire.Libido, DesireMath.Min, DesireMath.Max, true, v => _settings.SetLibido(v));
                Note(desire.State.LibidoUserSet
                    ? "Your setting. A new reading of her description does not change it."
                    : read >= 0f ? "Read from her description with her innocence: " + Mathf.RoundToInt(read) + " (" + DesireMath.LibidoName(read) + ")."
                    : "Not read yet; she runs on 50 until the next reading of her innocence.");
                if (desire.State.LibidoUserSet)
                    Button("Use the reading", () => { _settings.UseLibidoReading(); _rebuild = true; });
                Note("Desire now: " + Mathf.RoundToInt(desire.Desire) + " (" + DesireMath.BandName(desire.Desire) + ")"
                    + (desire.Spent ? ", spent after a release" : string.Empty)
                    + ". It moves on its own: it builds over time toward a level her libido sets, rises in sexual moments and with her kinks, and a climax spends it. You cannot set it.");
            }
        }

        /// <summary>JUH-182: 18+ → Secrets. JUH-125: read on the local model only; the Profile panel reveals what is in them.</summary>
        void BuildIntimateSecrets()
        {
            var secrets = _settings.IntimateSecrets;
            if (!IntimateExpressions.Active || secrets == null || !secrets.IsLoaded)
            {
                Note("Not read yet.");
                return;
            }

            {
                Section("Her 18+ secrets", true);
                Note("Kinks, sensitive areas and what she is shy to do, read from her description on the local model. You find them in intimate moments; Profile → Personality shows what you found.");
                var status = secrets.StatusLine();
                if (status.Length > 0)
                    Note(status);
                Button(secrets.Reading ? "Read her secrets again (reading...)" : "Read her secrets again", () => { _settings.ReadIntimateSecretsAgain(); _rebuild = true; });
            }
        }

        /// <summary>A light-blue indeterminate bar while the model writes her character sheet.</summary>
        void BuildReadingProgress(float startedAt)
        {
            _readingStartedAt = startedAt > 0f ? startedAt : Time.unscaledTime;
            _shownReadingSeconds = -1;
            _readingLabel = HappiUiKit.Text(_body, "ReadingLabel",
                "Reading her description with the model...", _theme.FontSmall * _s, _theme.Text,
                TextAlignmentOptions.MidlineLeft, false);
            Place(_readingLabel.rectTransform, 20f * _s);

            _readingTrack = HappiUiKit.Panel(_body, "ReadingTrack", _theme.PanelInset, false).rectTransform;
            Place(_readingTrack, 14f * _s);

            var fill = HappiUiKit.Panel(_readingTrack, "ReadingFill", _theme.Hover, false);
            _readingFill = fill.rectTransform;
            _readingFill.anchorMin = new Vector2(0f, 0f);
            _readingFill.anchorMax = new Vector2(0f, 1f);
            _readingFill.pivot = new Vector2(0f, 0.5f);
            _readingFill.anchoredPosition = new Vector2(4f * _s, 0f);
            _readingFill.sizeDelta = new Vector2(20f * _s, -6f * _s);

            Note("A large local model can take several minutes.");
        }

        void AnimateReadingBar()
        {
            if (_readingTrack == null || _readingFill == null)
                return;
            var width = _readingTrack.rect.width - 8f * _s;
            if (width <= 1f)
                return;

            var phase = Mathf.Repeat(Time.unscaledTime * 0.55f, 1f);
            var fillWidth = Mathf.Lerp(width * 0.22f, width * 0.62f, Mathf.Sin(phase * Mathf.PI));
            _readingFill.sizeDelta = new Vector2(fillWidth, -6f * _s);
            _readingFill.anchoredPosition = new Vector2(4f * _s + (width - fillWidth) * phase, 0f);

            var seconds = Mathf.Max(0, Mathf.FloorToInt(Time.unscaledTime - _readingStartedAt));
            if (seconds != _shownReadingSeconds)
            {
                _shownReadingSeconds = seconds;
                _readingLabel.text = "Reading her description with the model... "
                    + (seconds / 60).ToString("0") + ":" + (seconds % 60).ToString("00");
            }
        }

        /// <summary>
        /// JUH-117: the reading is a guess, so the user can correct who she is to them. Every
        /// click applies at once and is locked against later readings.
        /// </summary>
        void BuildRelationshipCorrection()
        {
            var sheet = _settings.ActiveSheet();
            if (sheet == null || sheet.relationship == null)
                return;

            if (!_correctingRelationship)
            {
                Button("Correct the relationship", () => { _correctingRelationship = true; _rebuild = true; });
                if (sheet.relationshipLocked)
                    Button("Use the reading again", () => { _settings.UseReadingForRelationship(); _rebuild = true; });
                return;
            }

            var r = sheet.relationship;
            Note("Who is she to you? This sets where your relationship stands now; replies move it from there.");
            for (var i = 0; i < RelationshipRoles.All.Length; i++)
            {
                var role = RelationshipRoles.All[i];
                Choice(role.Replace('_', ' '), r.role == role,
                    () => _settings.CorrectPersonaRelationship(role, r.label, r.affection, r.familiarity, r.sharedHistory));
            }

            Field("Her word for it (stepsister, coworker...)", r.label, false,
                v => _settings.CorrectPersonaRelationship(r.role, v, r.affection, r.familiarity, r.sharedHistory));
            Note("Affection " + Mathf.RoundToInt(r.affection) + " (-100 to 100)");
            Button("Affection -" + (int)PisaraCorrections.Step,
                () => _settings.CorrectPersonaRelationship(r.role, r.label, r.affection - PisaraCorrections.Step, r.familiarity, r.sharedHistory));
            Button("Affection +" + (int)PisaraCorrections.Step,
                () => _settings.CorrectPersonaRelationship(r.role, r.label, r.affection + PisaraCorrections.Step, r.familiarity, r.sharedHistory));
            Note("Familiarity " + Mathf.RoundToInt(r.familiarity) + " (0 to 100)");
            Button("Familiarity -" + (int)PisaraCorrections.Step,
                () => _settings.CorrectPersonaRelationship(r.role, r.label, r.affection, r.familiarity - PisaraCorrections.Step, r.sharedHistory));
            Button("Familiarity +" + (int)PisaraCorrections.Step,
                () => _settings.CorrectPersonaRelationship(r.role, r.label, r.affection, r.familiarity + PisaraCorrections.Step, r.sharedHistory));
            Toggle("You share a past", r.sharedHistory,
                v => _settings.CorrectPersonaRelationship(r.role, r.label, r.affection, r.familiarity, v));
            Note("A stranger never shares a past. Each role has a least familiarity (family 60, partner 70), so a lower pick stops there.");
            Button("Done", () => { _correctingRelationship = false; _rebuild = true; });
        }

        void Save(string id, Action<PersonaDefinition> change)
        {
            _settings.SavePersona(id, change);
            _rebuild = true;
        }

        static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        // ---- Games: the game companion (JUH-47) --------------------------------------------

        void BuildGames()
        {
            var games = _settings.Games;
            Section("Game companion");
            Note("She watches your game and reacts, like a friend on the couch. Only each game's official data feed is used. Nothing reads or changes the game itself.");
            Toggle("Game companion", S.GameCompanion, v => Change(s => s.GameCompanion = v));
            if (!S.GameCompanion)
            {
                Note("Off. Nothing is read from your games.");
                return;
            }

            if (games == null)
            {
                Note("The game companion isn't available in this session.");
                return;
            }


            Section("Her lines");
            Toggle("Let the cloud AI write her lines", S.GameCloudConsent, v => Change(s => s.GameCloudConsent = v));
            Note(S.GameCloudConsent
                ? "What just happened in your game (a kill, a death, a win) is sent to the cloud AI API with her persona, and she answers in her own words. If that fails, is slow or the hourly cap is used up, she says a pre-written line instead."
                : "She says pre-written lines in her personality. Nothing about your game leaves this PC.", true);
            if (games.Mode != CommentaryMode.ModelFirst && games.ModeReason.Length > 0 && games.ModeReason != CommentaryGate.NoConsent)
                Note(games.ModeReason);
            Note("Her style in games: " + GameVoices.Name(games.Voice) + ". It comes from her personality sliders and her relationship to you.");

            Section("How often");
            var frequency = S.GameFrequency;
            Choice("Rare", frequency == GameFrequencyNames.Rare, () => Change(s => s.GameFrequency = GameFrequencyNames.Rare));
            Choice("Normal", frequency == GameFrequencyNames.Normal, () => Change(s => s.GameFrequency = GameFrequencyNames.Normal));
            Choice("Chatty", frequency == GameFrequencyNames.Chatty, () => Change(s => s.GameFrequency = GameFrequencyNames.Chatty));
            var pace = GameCommentarySession.FrequencyFor(frequency);
            Note("At most one line every " + Mathf.RoundToInt((float)CommentaryPacer.CooldownFor(pace)) + " seconds, sooner for a big moment. Up to "
                 + CommentaryPacer.BudgetFor(pace) + " cloud lines an hour.");
            if (games.Muted)
                Button("Talk again", () => { games.Unmute(); _rebuild = true; });
            else
                Button("Quiet for 10 minutes", () => { games.MuteFor(TimeSpan.FromMinutes(10)); _rebuild = true; });

            GameSection(games, GameTitle.CounterStrike2, "Counter-Strike 2", S.GameCs2, v => Change(s => s.GameCs2 = v));
            GameSection(games, GameTitle.Dota2, "Dota 2", S.GameDota2, v => Change(s => s.GameDota2 = v));

            Section("League of Legends");
            Toggle("Watch League of Legends", S.GameLeague, v => Change(s => s.GameLeague = v));
            if (S.GameLeague)
                Note("No setup needed. She reads Riot's Live Client Data API on this PC during a match. "
                     + (games.LeagueConnected ? "A match is on." : "Waiting for a match."));

            Section("Gacha games");
            Note("These games have no official data feed, and they ban tools that touch the game. Happi only sees the game window, the way OBS or Discord does.");
            Toggle("Watch Genshin Impact", S.GameGenshin, v => Change(s => s.GameGenshin = v));
            Toggle("Watch Honkai: Star Rail", S.GameStarRail, v => Change(s => s.GameStarRail = v));
            Toggle("Watch Zenless Zone Zero", S.GameZzz, v => Change(s => s.GameZzz = v));
            Toggle("Watch Arknights: Endfield", S.GameEndfield, v => Change(s => s.GameEndfield = v));
            Toggle("Watch Neverness to Everness", S.GameNte, v => Change(s => s.GameNte = v));
            Toggle("Let her look at the game", S.GameScreenLooks, v => Change(s => s.GameScreenLooks = v));
            Note("While the game is the window you're using, she sends one picture of it to the cloud vision model every 2 minutes, and when you press "
                 + GameCompanionController.LookHotkey + ". That is about 30 pictures an hour. They are not saved. This needs \"Let the cloud AI write her lines\" above, and she never looks in private mode or with 18+ on.", true);
            Toggle("Live commentary while playing", S.GameLiveCommentary, v => Change(s => s.GameLiveCommentary = v));
            Note("Experimental: while the game is in front, she checks a new frame about every 30 seconds and comments only when the picture actually changed. "
                 + "It uses the same cloud vision model, can stay silent, and never runs in private mode or with 18+ on.", true);
            Toggle("Use game reference and patch context", S.GamePatchContext, v => Change(s => s.GamePatchContext = v));
            Note("When a watched game opens, she may read one public game page (Game8 / official site) and one latest patch/meta search, at most every six hours. It needs the Web permission in Senses → Tools. "
                 + "The results are background context, not a claim that they are current beyond those results.", true);
            if (S.GameScreenLooks && _chat != null && !_chat.CloudSeesImages)
                Note(ChatPanel.GameLineNoVision);
            Note(games.GachaGame != GameTitle.Unknown ? CommentaryPrompt.GameName(games.GachaGame) + " is open." : "No gacha game is open.");
            if (S.GameScreenLooks && games.GachaGame != GameTitle.Unknown)
                Button("Look now", () => { games.LookNow(); _rebuild = true; });
            Toggle("React to my pulls (Genshin, Star Rail, ZZZ)", S.GamePullHistory, v => Change(s => s.GamePullHistory = v));
            Note("She reads the pull history link the game saves when you open your history in the game, and asks HoYoverse's own server for your newest pulls. "
                 + "The link is never sent anywhere else or saved. Endfield and NTE don't offer a safe way to do this.", true);
            if (S.GamePullHistory && GachaHistoryLink.Supports(games.GachaGame))
                Note(games.FollowingPulls ? "Following your pulls." : GameCompanionController.PullLinkMissing);

            Section("Status");
            Note(string.IsNullOrEmpty(games.StatusLine) ? "Ready." : games.StatusLine);
            Field("Port for CS2 and Dota 2", S.GsiPort.ToString(CultureInfo.InvariantCulture), false, v =>
            {
                int port;
                if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
                    Change(s => s.GsiPort = GameFrequencyNames.NormalizePort(port));
            });
            Note("Change it only if another app uses port " + GameFrequencyNames.DefaultGsiPort + ". Then press Set up again for each game.");
        }

        void GameSection(GameCompanionController games, GameTitle game, string name, bool on, Action<bool> changed)
        {
            Section(name);
            Toggle("Watch " + name, on, changed);
            if (!on)
                return;
            var installed = games.IsInstalled(game, S);
            Note(installed
                ? "Set up. Restart the game if it was running when you set it up."
                : "Not set up yet. Set up puts a small config file in the game's own folder so the game sends its data to Happi on this PC.");
            if (game == GameTitle.Dota2)
                Note(GsiInstaller.Dota2LaunchHelp);
            Button(installed ? "Set up again" : "Set up", () => SetUpGame(game));
            if (installed)
                Button("Remove", () => { P.SetStatus(games.Remove(game).Message); _rebuild = true; }, true);
        }

        void SetUpGame(GameTitle game)
        {
            var games = _settings.Games;
            if (games == null)
                return;
            if (string.IsNullOrEmpty(S.GsiToken))
                Change(s => s.GsiToken = GsiConfig.NewToken());
            P.SetStatus(games.Install(game, S).Message);
            _rebuild = true;
        }

        void BuildScreen()
        {
            var look = _settings.ScreenController;
            var screen = _settings.ScreenLook;
            Section("Read a window");
            Note("Off until you turn it on. One window, one frame, then you ask what she sees. This is not continuous game commentary.");
            Toggle("Read a window", S.ScreenLookEnabled, v => { screen.SetEnabled(v); _rebuild = true; });
            if (!S.ScreenLookEnabled)
            {
                Note("Nothing is captured while this is off.");
                return;
            }

            Section("Cloud screen analysis");
            Note("Preview stays on this PC. Allowing cloud screen analysis sends the selected window image to the cloud AI API on the AI tab, if its model reads pictures. The image is not saved. Private mode never sends it and does not switch to a cloud model.", true);
            Toggle("Allow cloud screen analysis", S.ScreenCloudConsent, v => Change(s => s.ScreenCloudConsent = v));
            if (look != null)
                look.RefreshMonitors();
            Section("Whole screen");
            Toggle("Whole screen instead of a window", S.ScreenCaptureWholeMonitor, v => Change(s => s.ScreenCaptureWholeMonitor = v));
            if (S.ScreenCaptureWholeMonitor)
            {
                Toggle("Allow whole-screen capture", S.ScreenWholeMonitorConsent, v => Change(s => s.ScreenWholeMonitorConsent = v));
                Note("Whole-screen capture is a separate consent from window capture. It includes every window on that display. Private mode keeps it local.", true);
                if (S.ScreenWholeMonitorConsent)
                {
                    string note;
                    var monitors = screen.Monitors(out note);
                    if (monitors.Count == 0)
                        Note(note);
                    for (var i = 0; i < monitors.Count; i++)
                    {
                        var index = monitors[i].Index;
                        Choice(monitors[i].Label, monitors[i].Selected, () => { screen.SelectMonitor(index); _rebuild = true; });
                    }
                }
            }

            Section("Automatic looks");
            Toggle("Let her look during chat", S.ScreenAutoLook, v => Change(s => s.ScreenAutoLook = v));
            if (S.ScreenAutoLook)
            {
                Toggle("Pause automatic looks", S.ScreenAutoLookPaused, v => Change(s => s.ScreenAutoLookPaused = v));
                Note(S.ScreenAutoLookPaused
                    ? "Paused. The permission is still granted, but she cannot look until you unpause."
                    : "She may ask for one look while replying, at most once per reply and no more than once every " + Mathf.RoundToInt(ScreenLookController.AutoLookCooldownSeconds) + " seconds. It stays on this PC.");
            }
            else
            {
                Note("Automatic looks are off. She never captures the screen on her own; the Look button still works.");
            }

            Section("Vision model");
            Toggle("Fast screen replies", S.ScreenFastReply, v => Change(s => s.ScreenFastReply = v));
            Note(S.ScreenFastReply
                ? "The vision model writes the screen remark, so the chat model is not reloaded. Faster, with a simpler voice."
                : "The chat model writes the screen remark after the vision model. Slower, but it keeps the full persona.");
            Field("Local vision model", S.LocalVisionModel, false, v => Change(s => s.LocalVisionModel = v));
            Button("Use " + OllamaVision.RecommendedTag, () => Change(s => s.LocalVisionModel = OllamaVision.RecommendedTag));
            Note("The vision model and the chat model are different. Install it yourself with " + OllamaVision.PullCommand + ". A smaller tag is " + OllamaVision.SmallerTag + ".");
            if (look == null)
            {
                Note("Window capture is not available in this session.");
                return;
            }

            var decision = look.CurrentDecision();
            Section(S.ScreenCaptureWholeMonitor ? "Display" : "Window");
            if (decision.Route == ScreenLookRoute.Local)
                Note("What do you see? uses the local vision model with cloud screen analysis off. The image, the observation, the reply, and speech stay on this PC.");
            else if (decision.Route == ScreenLookRoute.Cloud)
                Note("Cloud screen analysis is allowed for the window you select. It does not include the rest of the desktop.");
            else if (!string.IsNullOrEmpty(decision.Message))
                Note(decision.Message);
            Note(string.IsNullOrEmpty(look.StatusText) ? "Ready." : look.StatusText);
            Button("Refresh window list", () => { look.RefreshWindows(); _rebuild = true; });
            if (!S.ScreenCaptureWholeMonitor && look.Windows != null)
            {
                for (var i = 0; i < look.Windows.Count; i++)
                {
                    var target = look.Windows[i];
                    Choice(WindowLabel(target), look.Selected == target, () => { look.Select(target); _rebuild = true; });
                }
            }

            if (S.ScreenCaptureWholeMonitor)
                Note(look.SelectedMonitor != null ? "Selected display: " + look.SelectedMonitor.Label + "." : "Choose a display. Nothing is captured until you do.");
            else
                Note(look.Selected == null ? "No window selected." : "Selected: " + WindowLabel(look.Selected) + ". That name is the window title from Windows.");

            Section("Look");
            if (look.PreviewTexture != null)
            {
                Preview(look.PreviewTexture);
                if (!string.IsNullOrEmpty(look.DeliveryNote))
                    Note(look.DeliveryNote);
                Button("Inspect exact frame", () => { _framePreview = true; _frameFit = true; _rebuild = true; });
            }
            else
            {
                Note("The preview appears here after you pick a window.");
            }

            var lookButton = Button(look.IsBusy ? "Stop look" : "Look at screen", () => { if (look.IsBusy) look.Stop(); else look.RequestLook(); _rebuild = true; });
            lookButton.Interactable = ScreenLookPolicy.LookButtonInteractable(decision.SendButtonEnabled, look.IsBusy);
            Button(look.IsBusy ? "Stop" : "Stop look", () => { look.Stop(); _rebuild = true; });
            Button("Clear window", () => { look.ClearSelection(); if (S.ScreenCaptureWholeMonitor) look.RefreshMonitors(); _rebuild = true; });
            Note("Stop cancels the current frame. Clear window drops the selection and the preview.");
            if (_framePreview && look.PreviewTexture != null)
                ExactFrame(look);
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

        void Preview(Texture texture)
        {
            var rect = HappiUiKit.Rect(_body, "Preview");
            var aspect = texture.height > 0 ? texture.width / (float)texture.height : 1f;
            var height = Mathf.Clamp(W / Mathf.Max(0.2f, aspect), 120f * _s, 320f * _s);
            Place(rect, height);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
        }

        /// <summary>The exact decoded frame that was sent (S-SC-15): Fit or 1:1, never written to disk.</summary>
        void ExactFrame(ScreenLookController look)
        {
            var texture = look.PreviewTexture;
            var root = HappiUiKit.Panel(_panel, "ExactFrame", new Color(0.02f, 0.03f, 0.06f, 0.98f), true);
            _frameRoot = root.rectTransform;
            HappiUiKit.Stretch(_frameRoot, 0f, 0f, 0f, 0f);
            var details = string.IsNullOrEmpty(look.PreviewDetails) ? look.DeliveryNote : look.PreviewDetails;
            var title = HappiUiKit.Text(_frameRoot, "Title", HappiUiKit.Literal("Exact frame sent to the vision model. " + details), _theme.FontSmall * _s, _theme.Text, TextAlignmentOptions.TopLeft, false);
            HappiUiKit.Place(title.rectTransform, 10f, 8f, _panel.rect.width - 180f * _s, 52f * _s);
            var close = HappiUiKit.Button(_frameRoot, "Close", "Close", _theme.FontSmall * _s, () => { _framePreview = false; _rebuild = true; });
            HappiUiKit.Place(close.Rect, _panel.rect.width - 80f * _s, 8f, 70f * _s, 26f * _s);
            var zoom = HappiUiKit.Button(_frameRoot, "Zoom", _frameFit ? "1:1" : "Fit", _theme.FontSmall * _s, () => { _frameFit = !_frameFit; _rebuild = true; });
            HappiUiKit.Place(zoom.Rect, _panel.rect.width - 160f * _s, 8f, 70f * _s, 26f * _s);
            var viewport = HappiUiKit.Rect(_frameRoot, "Viewport");
            HappiUiKit.Stretch(viewport, 10f, 66f * _s, 10f, 10f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = HappiUiKit.Rect(viewport, "Image");
            content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            var raw = content.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewW = Mathf.Max(60f, _panel.rect.width - 20f);
            var viewH = Mathf.Max(60f, _panel.rect.height - 76f * _s);
            var scale = _frameFit ? Mathf.Min(1f, Mathf.Min(viewW / texture.width, viewH / texture.height)) : 1f;
            content.sizeDelta = new Vector2(texture.width * scale, texture.height * scale);
            content.anchoredPosition = _frameFit ? new Vector2(Mathf.Max(0f, (viewW - texture.width * scale) * 0.5f), 0f) : Vector2.zero;
        }

        // ---- Row builders --------------------------------------------------------------------

        // ---- JUH-182: boxes like the Profile panel -----------------------------------------

        RectTransform _card;
        float _cardTop;
        bool _inCard;
        readonly HashSet<string> _expanded = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>A note longer than this shows its first sentence and a "more" link.</summary>
        const int LongNote = 170;

        /// <summary>The inside padding of a box.</summary>
        float Inset => _inCard ? _theme.Padding * _s : 0f;

        /// <summary>The width a row has: the body inside the box that is open.</summary>
        float W => _width - 2f * Inset;

        /// <summary>Opens a box at the current line. Rows placed after it go inside, until <see cref="CloseCard"/>.</summary>
        void OpenCard()
        {
            var bg = HappiUiKit.Panel(_body, "Box", _theme.PanelInset, false);
            var rect = bg.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -_y);
            rect.sizeDelta = new Vector2(-8f, 0f);
            _card = rect;
            _cardTop = _y;
            _inCard = true;
            _y += _theme.Padding * _s;
        }

        void CloseCard()
        {
            if (!_inCard)
                return;
            _y += (_theme.Padding - _theme.Spacing) * _s;
            if (_card != null)
                _card.sizeDelta = new Vector2(-8f, _y - _cardTop);
            _inCard = false;
            _card = null;
            _y += _theme.Spacing * _s * 1.5f;
        }

        /// <summary>A row at the current line, inside the open box (a row outside any box opens one).</summary>
        void Place(RectTransform rect, float height)
        {
            if (!_inCard)
                OpenCard();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -_y);
            rect.sizeDelta = new Vector2(-8f - 2f * Inset, height);
            _y += height + _theme.Spacing * _s;
        }

        /// <summary>A new box with a bold title, like the Profile cards. 18+ boxes have a pink title.</summary>
        void Section(string title, bool intimate = false)
        {
            CloseCard();
            OpenCard();
            var text = HappiUiKit.Text(_body, "Section", HappiUiKit.Literal(title), _theme.FontBody * _s, intimate ? _theme.Intimate : _theme.Text, TextAlignmentOptions.MidlineLeft, true);
            Place(text.rectTransform, 20f * _s);
        }

        TextMeshProUGUI Label(string value)
        {
            var text = HappiUiKit.Text(_body, "Label", HappiUiKit.Literal(value), _theme.FontBody * _s, _theme.Text, TextAlignmentOptions.MidlineLeft, false);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            var height = Mathf.Max(20f * _s, text.GetPreferredValues(text.text, W, 0f).y);
            Place(text.rectTransform, height);
            return text;
        }

        /// <summary>
        /// A short help line. A long one shows its first sentence and a "more" link that opens
        /// the rest (JUH-182); <paramref name="whole"/> keeps it whole, for privacy notes.
        /// </summary>
        TextMeshProUGUI Note(string value, bool whole = false)
        {
            var full = value ?? string.Empty;
            var shown = full;
            var collapsible = !whole && full.Length > LongNote && SettingsNotes.Short(full).Length < full.Length;
            var open = collapsible && _expanded.Contains(full);
            if (collapsible && !open)
                shown = SettingsNotes.Short(full);
            var text = HappiUiKit.Text(_body, "Note", HappiUiKit.Literal(shown), _theme.FontSmall * _s, _theme.TextMuted, TextAlignmentOptions.TopLeft, false);
            text.overflowMode = TextOverflowModes.Overflow;
            var linkW = collapsible ? 46f * _s : 0f;
            var height = Mathf.Max(16f * _s, text.GetPreferredValues(text.text, W - linkW, 0f).y + 2f);
            Place(text.rectTransform, height);
            if (collapsible)
            {
                var rect = text.rectTransform;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x - linkW, rect.sizeDelta.y);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x - linkW * 0.5f, rect.anchoredPosition.y);
                var link = HappiUiKit.Text(_body, "More", open ? "less" : "more", _theme.FontSmall * _s, _theme.Hover, TextAlignmentOptions.TopRight, false);
                var linkRect = link.rectTransform;
                linkRect.anchorMin = linkRect.anchorMax = new Vector2(1f, 1f);
                linkRect.pivot = new Vector2(1f, 1f);
                linkRect.anchoredPosition = new Vector2(-4f - Inset, rect.anchoredPosition.y);
                linkRect.sizeDelta = new Vector2(linkW, 16f * _s);
                link.raycastTarget = true;
                var button = link.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() =>
                {
                    if (_building)
                        return;
                    if (!_expanded.Remove(full))
                        _expanded.Add(full);
                    _rebuild = true;
                });
            }

            return text;
        }

        HappiButton Button(string label, Action action, bool danger = false)
        {
            var button = HappiUiKit.Button(_body, "Button", HappiUiKit.Literal(label), _theme.FontSmall * _s, () =>
            {
                if (!_building && action != null)
                    action();
            });
            button.Label.overflowMode = TextOverflowModes.Ellipsis;
            if (danger)
            {
                var colors = button.Button.colors;
                colors.normalColor = HappiTheme.Darken(_theme.Intimate, 0.35f);
                button.Button.colors = colors;
            }

            Place(button.Rect, _theme.ButtonHeight * _s);
            return button;
        }

        /// <summary>A choice among several: the selected one is marked and highlighted.</summary>
        void Choice(string label, bool selected, Action action)
        {
            var button = Button(selected ? HappiTheme.SelectedMark + StripMarks(label) : StripMarks(label), action);
            if (selected)
            {
                var colors = button.Button.colors;
                colors.normalColor = _theme.Selected;
                button.Button.colors = colors;
            }
        }

        static string StripMarks(string label)
        {
            return (label ?? string.Empty).Replace(" (selected)", string.Empty);
        }

        /// <summary>
        /// A compact dropdown: a button with the current choice, then an inline list of the
        /// options while it is open. Voices come from the folders the user already configured.
        /// </summary>
        void Dropdown(string caption, string value, IReadOnlyList<string> options, Func<string, string> optionLabel, Action<string> changed)
        {
            var shown = ChatterboxVoiceMenu.ClosedLabel(value, optionLabel);
            var button = Button(caption + ": " + shown + "  v", () =>
            {
                _openDropdown = string.Equals(_openDropdown, caption, StringComparison.Ordinal) ? null : caption;
                _rebuild = true;
            });
            if (!string.Equals(_openDropdown, caption, StringComparison.Ordinal))
                return;

            if (options == null || options.Count == 0)
            {
                Note("No voices were found in the configured folder.");
                return;
            }

            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var label = optionLabel(option);
                Choice(label, string.Equals(option, value, StringComparison.OrdinalIgnoreCase), () =>
                {
                    _openDropdown = null;
                    if (changed != null)
                        changed(option);
                    _rebuild = true;
                });
            }
        }

        List<string> PiperVoiceOptions()
        {
            var options = new List<string>();
            var path = !string.IsNullOrWhiteSpace(S.PiperVoice) ? S.PiperVoice : S.PiperExe;
            var folder = string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                var files = Directory.GetFiles(folder, "*.onnx");
                for (var i = 0; i < files.Length; i++)
                    options.Add(files[i]);
            }

            options.Sort(StringComparer.OrdinalIgnoreCase);
            AddCurrent(options, S.PiperVoice);
            return options;
        }

        List<string> KokoroVoiceOptions()
        {
            var options = new List<string>
            {
                "af_heart", "af_bella", "af_nicole", "af_sky",
                "bf_emma", "bf_isabella", "bm_george", "bm_lewis",
                "am_adam", "am_michael", "am_puck", "am_santa",
                "hm_omega", "hm_psi", "jf_alpha", "jm_kumo",
            };
            AddCurrent(options, S.KokoroVoice);
            return options;
        }

        List<string> ChatterboxVoiceOptions(bool includeEmpty, string current)
        {
            var presets = new List<string>();
            var root = S.ChatterboxVoices;
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            {
                var folders = Directory.GetDirectories(root);
                for (var i = 0; i < folders.Length; i++)
                {
                    var name = Path.GetFileName(folders[i]);
                    if (File.Exists(Path.Combine(folders[i], "reference.wav")))
                        presets.Add(name);
                }
            }

            return ChatterboxVoiceMenu.Options(presets, includeEmpty, current);
        }

        static void AddCurrent(List<string> options, string current)
        {
            if (string.IsNullOrWhiteSpace(current))
                return;
            for (var i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i], current, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            options.Add(current);
        }

        static string FileLabel(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : Path.GetFileName(value);
        }

        static IReadOnlyList<string> ChatFontFamilies()
        {
            return new[] { HappiTypeface.Family, "Segoe UI", "Georgia", "Trebuchet MS", "Consolas" };
        }

        static IReadOnlyList<string> ChatFontSizes()
        {
            var sizes = new List<string>();
            for (var size = 10; size <= 24; size++)
                sizes.Add(size.ToString(CultureInfo.InvariantCulture));
            return sizes;
        }

        void ApplyChatFont()
        {
            var chat = GetComponent<HappiChatView>();
            if (chat != null)
                chat.ApplyChatFont();
        }

        /// <summary>A labelled on/off switch.</summary>
        void Toggle(string label, bool value, Action<bool> changed)
        {
            var row = HappiUiKit.Rect(_body, "Toggle");
            Place(row, _theme.ButtonHeight * _s);
            var text = HappiUiKit.Text(row, "Label", HappiUiKit.Literal(label), _theme.FontBody * _s, _theme.Text, TextAlignmentOptions.MidlineLeft, false);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            HappiUiKit.Stretch(text.rectTransform, 0f, 0f, 70f * _s, 0f);
            var pill = HappiUiKit.Button(row, "Switch", value ? "On" : "Off", _theme.FontSmall * _s, () =>
            {
                if (!_building && changed != null)
                    changed(!value);
            });
            pill.Rect.anchorMin = new Vector2(1f, 0f);
            pill.Rect.anchorMax = new Vector2(1f, 1f);
            pill.Rect.pivot = new Vector2(1f, 0.5f);
            pill.Rect.anchoredPosition = Vector2.zero;
            pill.Rect.sizeDelta = new Vector2(62f * _s, 0f);
            if (value)
            {
                var colors = pill.Button.colors;
                colors.normalColor = _theme.Selected;
                pill.Button.colors = colors;
            }
        }

        TMP_InputField Field(string caption, string value, bool password, Action<string> onEnd)
        {
            var label = HappiUiKit.Text(_body, "Caption", HappiUiKit.Literal(caption), _theme.FontSmall * _s, _theme.TextMuted, TextAlignmentOptions.BottomLeft, false);
            Place(label.rectTransform, 15f * _s);
            _y -= _theme.Spacing * _s * 0.6f;
            var field = HappiUiKit.Input(_body, "Field", caption, _theme.FontBody * _s, password);
            Place((RectTransform)field.transform, 28f * _s);
            field.text = password ? string.Empty : value ?? string.Empty;
            field.onEndEdit.AddListener(text =>
            {
                if (_building || onEnd == null)
                    return;
                onEnd(text ?? string.Empty);
            });
            _fields.Add(field);
            return field;
        }

        void Area(string caption, string value, float height, int limit, Action<string> onEnd)
        {
            var field = Field(caption, value, false, onEnd);
            field.lineType = TMP_InputField.LineType.MultiLineNewline;
            field.characterLimit = limit;
            if (field.textComponent != null)
            {
                field.textComponent.textWrappingMode = TextWrappingModes.Normal;
                field.textComponent.alignment = TextAlignmentOptions.TopLeft;
            }

            var rect = (RectTransform)field.transform;
            _y -= rect.sizeDelta.y + _theme.Spacing * _s;
            Place(rect, height * _s);
        }

        void Slider(Func<float, string> label, float value, float min, float max, bool whole, Action<float> changed)
        {
            var caption = HappiUiKit.Text(_body, "SliderLabel", HappiUiKit.Literal(label(value)), _theme.FontBody * _s, _theme.Text, TextAlignmentOptions.MidlineLeft, false);
            Place(caption.rectTransform, 18f * _s);
            var rect = HappiUiKit.Rect(_body, "Slider");
            Place(rect, 18f * _s);
            var background = rect.gameObject.AddComponent<Image>();
            background.sprite = HappiUiKit.Rounded();
            background.type = Image.Type.Sliced;
            background.color = _theme.PanelInset;
            var fillArea = HappiUiKit.Rect(rect, "Fill Area");
            HappiUiKit.Stretch(fillArea, 8f * _s, 6f * _s, 8f * _s, 6f * _s);
            var fill = HappiUiKit.Rect(fillArea, "Fill");
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = HappiUiKit.Rounded();
            fillImage.type = Image.Type.Sliced;
            fillImage.color = _theme.Border;
            fill.sizeDelta = Vector2.zero;
            var handleArea = HappiUiKit.Rect(rect, "Handle Slide Area");
            HappiUiKit.Stretch(handleArea, 8f * _s, 0f, 8f * _s, 0f);
            var handle = HappiUiKit.Rect(handleArea, "Handle");
            handle.sizeDelta = new Vector2(18f * _s, 0f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = HappiUiKit.Circle();
            handleImage.color = _theme.Text;
            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            var colors = slider.colors;
            colors.highlightedColor = _theme.Hover;
            slider.colors = colors;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = whole;
            slider.value = Mathf.Clamp(value, min, max);
            slider.onValueChanged.AddListener(next =>
            {
                if (_building)
                    return;
                caption.text = HappiUiKit.Literal(label(next));
                if (changed != null)
                    changed(next);
            });
        }

        // ---- Focus -------------------------------------------------------------------------

        bool AnyFieldFocused()
        {
            for (var i = 0; i < _fields.Count; i++)
            {
                if (_fields[i] != null && _fields[i].isFocused)
                    return true;
            }

            return false;
        }

        void BlurFields()
        {
            for (var i = 0; i < _fields.Count; i++)
            {
                if (_fields[i] != null && _fields[i].isFocused)
                    _fields[i].DeactivateInputField();
            }
        }

        void SyncFocus()
        {
            var focused = AnyFieldFocused();
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
