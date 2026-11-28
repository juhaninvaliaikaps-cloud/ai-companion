using System;
using System.Collections.Generic;
using AICompanion.Core;
using AICompanion.NewUI.Wheel;
using AICompanion.Overlay;
using AICompanion.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-228, Her Room phase 1b: the room window. The Ribbon's door glyph opens it: a door made
    /// of the wallpaper swings open (about 1.2 s, which also covers loading), and the room grows
    /// out of the doorway. Inside, the player walks by clicking the floor or with the arrow keys,
    /// she strolls on her own and goes to bed at night, and the light follows the computer's
    /// clock. The logic is in Core (RoomLayout, RoomWalker, RoomDoor, RoomClock, RoomWander);
    /// this view only draws it.
    /// JUH-229: the v1 art pack (Resources/HerRoom, placed from room_catalog.json), animated
    /// characters, furniture drawn back to front with them, and picture-layer lighting: the clock
    /// tint, the tinted sky, a sunbeam on the floor, the night shade with the lamp's pool of light,
    /// and the lamp glow (RoomLighting).
    /// JUH-237, phase 2b: her routine (RoomRoutine) instead of the random stroll, her poses on
    /// the furniture, the open wardrobe, her hobby piece in the hobby slot, and clicks on
    /// furniture: left-click and the player uses it (he sits on the rug, the pouf or the desk
    /// chair), right-click and she is asked to.
    /// </summary>
    public sealed class HerRoomView : MonoBehaviour, IOverlayKeepAwake
    {
        const string SidePref = "HerRoom.Side";
        const float TitleHeight = 34f;
        const float Padding = 8f;

        /// <summary>A 256 px character frame drawn at this fraction of the room's side (256 of the shell's 1024 px).</summary>
        const float FigureFrame = 0.25f;

        /// <summary>How often the lighting is worked out again from the clock (it changes slowly).</summary>
        const float LightEvery = 0.5f;

        /// <summary>The lamp glow picture is 384 px on the 1024 px shell.</summary>
        const float GlowSize = 384f / 1024f;

        static readonly Color NightShade = new Color(0.03f, 0.04f, 0.12f, 1f);

        /// <summary>JUH-239: the gap between the room window and the docked chat box, and the bubble's limits.</summary>
        const float ChatGap = 10f;
        const int BubbleChars = 140;
        const float BubbleWidth = 0.46f;
        const float BubblePad = 7f;
        /// <summary>Her head is about this far above her feet, as a fraction of the 256 px frame (the figures are about 128 px tall).</summary>
        const float HeadAbove = 0.56f;

        /// <summary>The feet line of a character frame, from the bottom (frames have feet at y = 240 of 256).</summary>
        const float FeetPivot = 16f / 256f;

        static readonly Color Doorway = new Color32(46, 30, 20, 255);
        static readonly Color PlainDoor = new Color32(150, 98, 60, 255);
        static readonly Color Gold = new Color32(255, 202, 34, 255);

        Canvas _canvas;
        CanvasGroup _group;
        RectTransform _frame;
        RectTransform _content;
        RawImage _shell;
        RawImage _sky;
        RawImage _her;
        RawImage _player;
        RectTransform _things;
        HerRoomBeam _beam;
        RawImage _shadePool;
        Image _shadePlain;
        RawImage _glow;
        RectTransform _bubble;
        TextMeshProUGUI _bubbleText;
        int _bubbleRevision = -1;
        Vector2 _bubbleSize;
        readonly List<Graphic> _tinted = new List<Graphic>();
        RectTransform[] _drawn;
        float[] _depths;
        int[] _order;
        float _nextLight;
        RectTransform _doorway;
        Image _doorwayImage;
        RectTransform _doorPanel;
        RawImage _doorImage;

        Texture2D _shellTexture;
        Texture2D _poolMask;
        Texture2D _wallpaper;
        CharacterArt _herArt;
        CharacterArt _playerArt;

        RoomLayout _layout;
        RoomWalker _herWalk;
        RoomWalker _playerWalk;
        RoomSlots _slots;
        RoomRoutine _routine;
        readonly RoomPlayerUse _playerUse = new RoomPlayerUse();
        RoomHobby _hobby;
        RoomActivity _herPosed;
        List<RoomPiece> _room;
        readonly Dictionary<string, float> _pieceDepth = new Dictionary<string, float>(StringComparer.Ordinal);
        RawImage _wardrobe;
        Texture _wardrobeClosed;
        Texture _wardrobeOpen;
        Rect _wardrobeClosedRect;
        Rect _wardrobeOpenRect;
        bool _wardrobeIsOpen;
        readonly RoomDoor _door = new RoomDoor();
        OverlayWindow _overlay;
        HappiSettingsView _settingsView;
        HappiWheelHost _wheel;

        float _wantedSide;
        float _scale = 1f;
        Rect _frameRect;
        Rect _doorRect;
        bool _built;

        public bool IsOpen => !_door.IsClosed;
        public bool KeepFrameRateActive => !_door.IsClosed;

        public void Attach()
        {
            _overlay = GetComponent<OverlayWindow>();
            _settingsView = GetComponent<HappiSettingsView>();
            _wheel = GetComponent<HappiWheelHost>();
            _wantedSide = PlayerPrefs.GetFloat(SidePref, RoomWindowSize.Default);
        }

        /// <summary>The door glyph: opens the room, or closes it when it is open.</summary>
        public void Toggle()
        {
            if (_door.State == RoomDoorState.Open || _door.State == RoomDoorState.Opening)
                Close();
            else
                Open();
        }

        public void Open()
        {
            // Her hobby comes from her Pisara reading; another persona may have another one.
            if (_built && _door.IsClosed && CurrentHobby() != _hobby)
                Teardown();
            if (!Build())
                return;
            if (_door.IsClosed)
            {
                // She and the player come in through the door.
                _herWalk.Place(_layout.HerEntry, RoomFacing.Up);
                _playerWalk.Place(_layout.PlayerEntry, RoomFacing.Up);
                _herWalk.WalkTo(new RoomTile(_layout.HerEntry.X, _layout.HerEntry.Y - 2));
                _routine = new RoomRoutine(_layout, _slots, Environment.TickCount, Time.unscaledTime);
                _playerUse.Cancel();
                _herPosed = RoomActivity.None;
                SetWardrobe(false);
                LoadWallpaper();
            }

            _door.Open();
            _nextLight = 0f;
            HerRoomNow.IsOpen = true;
            // Her desktop walk stops; the desktop presence also pauses while the room is open.
            var walk = GetComponent<IWalkRequest>();
            walk?.RequestStop();
            _canvas.gameObject.SetActive(true);
        }

        public void Close()
        {
            _door.Close();
        }

        bool Build()
        {
            if (_built)
                return true;
            _shellTexture = Resources.Load<Texture2D>("HerRoom/room_shell");
            _herArt = CharacterArt.Load("happi");
            _playerArt = CharacterArt.Load("player");
            if (_shellTexture == null || _herArt == null || _playerArt == null)
            {
                Debug.LogWarning("Her room: the room art is missing from Resources/HerRoom.");
                return false;
            }

            _hobby = CurrentHobby();
            _layout = RoomLayout.V1(_hobby);
            _slots = RoomSlots.V1(_layout, _hobby);
            _herWalk = new RoomWalker(_layout, _layout.HerEntry, RoomFacing.Up);
            _playerWalk = new RoomWalker(_layout, _layout.PlayerEntry, RoomFacing.Up);

            var root = HappiUiKit.CreateCanvas(transform, "HerRoom", 60, out _canvas).transform;

            // The doorway and the door behind the room, so the room grows over them.
            _doorwayImage = HappiUiKit.Panel(root, "Doorway", Doorway, false);
            _doorway = _doorwayImage.rectTransform;
            _doorImage = new GameObject("Door", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _doorImage.transform.SetParent(root, false);
            _doorImage.raycastTarget = false;
            _doorPanel = _doorImage.rectTransform;

            _frame = HappiUiKit.Panel(root, "Her room window", RibbonMenu.Ink, true).rectTransform;
            RibbonShape.Apply(_frame.GetComponent<Image>(), false);
            _group = _frame.gameObject.AddComponent<CanvasGroup>();

            var accent = HappiUiKit.Panel(_frame, "Title accent", Gold, false).rectTransform;
            accent.anchorMin = accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 1f);
            accent.anchoredPosition = new Vector2(12f, -9f);
            accent.sizeDelta = new Vector2(5f, 16f);
            accent.localRotation = Quaternion.Euler(0f, 0f, -14f);

            var title = HappiUiKit.Text(_frame, "Title", "Her Room", HappiTheme.Current.FontTitle,
                HappiTheme.Current.Text, TextAlignmentOptions.MidlineLeft, true);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(26f, 0f);
            titleRect.sizeDelta = new Vector2(-70f, TitleHeight);

            var close = HappiUiKit.Button(_frame, "Close her room", "×", HappiTheme.Current.FontTitle, Close);
            close.Rect.anchorMin = close.Rect.anchorMax = new Vector2(1f, 1f);
            close.Rect.pivot = new Vector2(1f, 1f);
            close.Rect.anchoredPosition = new Vector2(-6f, -5f);
            close.Rect.sizeDelta = new Vector2(26f, 24f);

            // The room, back to front: the sky behind the shell's open window, the shell, the rug and
            // wall pieces, the sunbeam, the furniture and the characters (sorted every frame), the
            // night shade, and the lamp glow.
            _content = HappiUiKit.Rect(_frame, "Room");
            _sky = Raw(_content, "Sky", Resources.Load<Texture2D>("HerRoom/lighting/room_window_sky"), false);
            if (_sky.texture == null)
                _sky.texture = Texture2D.whiteTexture;
            Anchor(_sky.rectTransform, _layout.WindowLeft, _layout.WindowTop, _layout.WindowRight, _layout.WindowBottom);
            _shell = Raw(_content, "Shell", _shellTexture, true);
            HappiUiKit.Stretch(_shell.rectTransform, 0f, 0f, 0f, 0f);
            _shell.gameObject.AddComponent<HerRoomInput>().Pressed = OnFloorPressed;
            _tinted.Add(_shell);

            var back = HappiUiKit.Rect(_content, "Rug and walls");
            HappiUiKit.Stretch(back, 0f, 0f, 0f, 0f);
            _beam = new GameObject("Sunbeam", typeof(RectTransform), typeof(HerRoomBeam)).GetComponent<HerRoomBeam>();
            _beam.transform.SetParent(_content, false);
            _beam.raycastTarget = false;
            HappiUiKit.Stretch(_beam.rectTransform, 0f, 0f, 0f, 0f);
            _things = HappiUiKit.Rect(_content, "Furniture and people");
            HappiUiKit.Stretch(_things, 0f, 0f, 0f, 0f);
            BuildFurniture(back);

            _shadePlain = HappiUiKit.Panel(_content, "Night shade", NightShade, false);
            HappiUiKit.Stretch(_shadePlain.rectTransform, 0f, 0f, 0f, 0f);
            _poolMask = PoolMask();
            _shadePool = Raw(_content, "Night shade with lamp light", _poolMask, false);
            HappiUiKit.Stretch(_shadePool.rectTransform, 0f, 0f, 0f, 0f);
            _glow = Raw(_content, "Lamp glow", Resources.Load<Texture2D>("HerRoom/lighting/room_lamp_glow"), false);
            var shade = RoomLighting.LampShade;
            Anchor(_glow.rectTransform, shade.x - GlowSize * 0.5f, shade.y - GlowSize * 0.5f, shade.x + GlowSize * 0.5f, shade.y + GlowSize * 0.5f);
            _glow.gameObject.SetActive(_glow.texture != null);

            // JUH-239: her speech bubble, over everything in the room (not shaded at night).
            var bubble = HappiUiKit.Panel(_content, "Her speech bubble", RibbonMenu.Ink, false);
            RibbonShape.Apply(bubble, false);
            _bubble = bubble.rectTransform;
            _bubble.pivot = new Vector2(0.5f, 0f);
            _bubbleText = HappiUiKit.Text(_bubble, "Line", string.Empty, HappiTheme.Current.FontBody,
                HappiTheme.Current.Text, TextAlignmentOptions.Center, false);
            HappiUiKit.Stretch(_bubbleText.rectTransform, BubblePad, BubblePad, BubblePad, BubblePad);
            _bubbleText.textWrappingMode = TextWrappingModes.Normal;
            _bubbleRevision = -1;
            _bubble.gameObject.SetActive(false);

            var corner = HappiUiKit.Panel(_frame, "Resize", RibbonMenu.Cyan, true).rectTransform;
            corner.anchorMin = corner.anchorMax = new Vector2(1f, 0f);
            corner.pivot = new Vector2(1f, 0f);
            corner.anchoredPosition = new Vector2(-2f, 2f);
            corner.sizeDelta = new Vector2(12f, 12f);
            var resize = corner.gameObject.AddComponent<HerRoomResize>();
            resize.Dragged = OnResize;
            resize.Released = () => PlayerPrefs.SetFloat(SidePref, _wantedSide);

            _canvas.gameObject.SetActive(false);
            _built = true;
            return true;
        }

        /// <summary>The furniture from the catalogue, then the two characters, all under one parent so they can be sorted together.</summary>
        void BuildFurniture(RectTransform back)
        {
            var asset = Resources.Load<TextAsset>("HerRoom/room_catalog");
            var catalog = RoomCatalog.Parse(asset != null ? asset.text : null);
            var room = catalog.RoomFor(_hobby);
            _room = room;
            _pieceDepth.Clear();
            var drawn = new List<RectTransform>();
            var depths = new List<float>();
            foreach (var piece in room)
            {
                var texture = Resources.Load<Texture2D>(piece.ResourcePath);
                if (texture == null)
                {
                    Debug.LogWarning("Her room: missing furniture picture " + piece.id + ".");
                    continue;
                }

                var depth = RoomCatalog.DepthOf(piece, room);
                _pieceDepth[piece.id] = depth;
                var parent = depth < 0f ? back : _things;
                var image = Raw(parent, piece.id, texture, false);
                var rect = RoomCatalog.RectOf(piece);
                Anchor(image.rectTransform, rect.xMin, rect.yMin, rect.xMax, rect.yMax);
                _tinted.Add(image);
                if (piece.id == "room_wardrobe")
                    KeepWardrobe(catalog, piece, image, rect);
                if (depth < 0f)
                {
                    // Rugs lie under the wall pieces.
                    if (depth <= RoomCatalog.RugDepth)
                        image.transform.SetAsFirstSibling();
                    continue;
                }

                // Keep the list sorted by depth (insertion; a dozen pieces, built once).
                var at = depths.Count;
                while (at > 0 && depths[at - 1] > depth)
                    at--;
                depths.Insert(at, depth);
                drawn.Insert(at, image.rectTransform);
            }

            _herArt.LoadPoses("happi", _slots);
            _playerArt.LoadPoses("player", null);
            _player = Raw(_things, "Player", _playerArt.Idle[0], false);
            _her = Raw(_things, "Her", _herArt.Idle[0], false);
            Figure(_player.rectTransform);
            Figure(_her.rectTransform);
            _tinted.Add(_player);
            _tinted.Add(_her);

            drawn.Add(_her.rectTransform);
            drawn.Add(_player.rectTransform);
            _drawn = drawn.ToArray();
            _depths = depths.ToArray();
            _order = new int[_drawn.Length];
        }

        /// <summary>The wardrobe and its open picture, swapped while she uses it.</summary>
        void KeepWardrobe(RoomCatalog catalog, RoomPiece piece, RawImage image, Rect rect)
        {
            _wardrobe = image;
            _wardrobeClosed = image.texture;
            _wardrobeClosedRect = rect;
            var open = catalog.VariantAt("room_wardrobe_open", piece);
            _wardrobeOpen = open != null ? Resources.Load<Texture2D>(open.ResourcePath) : null;
            _wardrobeOpenRect = open != null ? RoomCatalog.RectOf(open) : rect;
        }

        void SetWardrobe(bool open)
        {
            if (_wardrobe == null || _wardrobeOpen == null || open == _wardrobeIsOpen)
                return;
            _wardrobeIsOpen = open;
            var rect = open ? _wardrobeOpenRect : _wardrobeClosedRect;
            _wardrobe.texture = open ? _wardrobeOpen : _wardrobeClosed;
            Anchor(_wardrobe.rectTransform, rect.xMin, rect.yMin, rect.xMax, rect.yMax);
        }

        /// <summary>Her hobby, from the active persona's Pisara reading or a guess from her description.</summary>
        RoomHobby CurrentHobby()
        {
            var keeper = GetComponent<PisaraReadingKeeper>();
            return keeper != null ? RoomHobbies.For(keeper.Sheet, keeper.Prompt) : RoomHobby.Piano;
        }

        /// <summary>Drops the built room so the next Open builds it again (another hobby).</summary>
        void Teardown()
        {
            if (_canvas != null)
                Destroy(_canvas.gameObject);
            if (_poolMask != null)
                Destroy(_poolMask);
            _canvas = null;
            _poolMask = null;
            _tinted.Clear();
            _wardrobe = null;
            _wardrobeIsOpen = false;
            _built = false;
        }

        /// <summary>The night shade's cover, baked once: clear around the lamp, full elsewhere.</summary>
        static Texture2D PoolMask()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "HerRoomPool" };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // Texture rows go up; room fractions go down.
                    var point = new Vector2((x + 0.5f) / size, 1f - (y + 0.5f) / size);
                    var cover = (byte)Mathf.RoundToInt(RoomLighting.PoolCover(point) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, cover);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static RawImage Raw(Transform parent, string name, Texture texture, bool raycast)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(parent, false);
            image.texture = texture;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>Anchors a child to a rectangle of the room picture (fractions, y down).</summary>
        static void Anchor(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = new Vector2(left, 1f - bottom);
            rect.anchorMax = new Vector2(right, 1f - top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Figure(RectTransform rect)
        {
            rect.pivot = new Vector2(0.5f, FeetPivot);
        }

        void LoadWallpaper()
        {
            ReleaseWallpaper();
            _wallpaper = DesktopWallpaper.Load();
            _doorImage.texture = _wallpaper;
            _doorImage.color = _wallpaper != null ? Color.white : PlainDoor;
        }

        void ReleaseWallpaper()
        {
            if (_wallpaper != null)
                Destroy(_wallpaper);
            _wallpaper = null;
            if (_doorImage != null)
                _doorImage.texture = null;
        }

        void Update()
        {
            if (!_built || (_door.IsClosed && !_canvas.gameObject.activeSelf))
                return;

            HappiUiKit.FollowCamera(_canvas, 0.41f);
            var delta = Time.unscaledDeltaTime;
            var doorEvent = _door.Tick(delta);
            Layout();
            Animate();

            if (doorEvent == RoomDoorEvent.Closed)
            {
                _canvas.gameObject.SetActive(false);
                ReleaseWallpaper();
                HerRoomNow.Reset();
                return;
            }

            var now = DateTime.Now;
            if (Time.unscaledTime >= _nextLight)
            {
                _nextLight = Time.unscaledTime + LightEvery;
                Light(now);
            }

            if (!_door.IsOpen)
            {
                PlaceFigures();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) && CanTakeKeys())
            {
                Close();
                return;
            }

            ReadArrows();
            _playerWalk.Tick(delta);
            _herWalk.Tick(delta);

            var night = RoomClock.At(now).Night;
            var hour = (float)now.TimeOfDay.TotalHours;
            RoomTile target;
            if (_routine.TryNext(Time.unscaledTime, _herWalk.Tile, _herWalk.Walking, _playerWalk.Tile, _playerUse.Doing,
                    hour, night, CompanionMoodNow.Effective, out target))
                _herWalk.WalkTo(target);

            // She arrived at a spot: turn to it once.
            var doing = _routine.Doing;
            RoomSlot slot;
            if (doing != _herPosed)
            {
                _herPosed = doing;
                if (doing != RoomActivity.None && _slots.TryGet(doing, out slot))
                    _herWalk.Place(slot.Stand, slot.Facing);
            }
            if (doing == RoomActivity.None && !_herWalk.Walking && RoomWander.Close(_herWalk.Tile, _playerWalk.Tile))
                _herWalk.Face(_playerWalk.Tile);
            SetWardrobe(doing == RoomActivity.Wardrobe);

            if (_playerUse.Arrive(_slots, _playerWalk.Tile, _playerWalk.Walking, out slot))
                _playerWalk.Place(slot.Stand, slot.Facing);

            PlaceFigures();
        }

        /// <summary>The picture-layer lighting for this moment (option 1 in JUH-229: no engine lights).</summary>
        void Light(DateTime now)
        {
            var light = RoomClock.At(now);
            var hour = (float)now.TimeOfDay.TotalHours;
            for (var i = 0; i < _tinted.Count; i++)
                _tinted[i].color = light.Tint;
            _sky.color = RoomLighting.SkyTint(light.Sky);
            _beam.Set(hour);

            var shade = RoomLighting.Shade(light.Tint);
            var lamp = Mathf.Clamp01(light.Lamp);
            _shadePool.color = new Color(NightShade.r, NightShade.g, NightShade.b, shade * lamp);
            _shadePlain.color = new Color(NightShade.r, NightShade.g, NightShade.b, shade * (1f - lamp));
            _shadePool.enabled = shade * lamp > 0.001f;
            _shadePlain.enabled = shade * (1f - lamp) > 0.001f;
            _glow.color = new Color(1f, 1f, 1f, RoomLighting.Glow(lamp));
            _glow.enabled = lamp > 0.001f;
        }

        /// <summary>Where the window and the door go: centred in the visible part of Happi's window.</summary>
        void Layout()
        {
            var visible = OverlayVisibleArea.Get(_overlay);
            var area = new Rect(visible.xMin, Screen.height - visible.yMax, visible.width, visible.height);
            _scale = HappiTheme.Current.UiScale(Screen.dpi);
            var pad = Padding * _scale;
            var title = TitleHeight * _scale;
            var room = Mathf.Min(area.width - 2f * pad, area.height - title - 2f * pad);
            var side = RoomWindowSize.Clamp(_wantedSide * _scale, room);

            // JUH-239: leave space beside the room for the chat box when it shows (shrinking the
            // room down to its minimum if needed); otherwise the chat box sits over its lower edge.
            var chat = HerRoomNow.ChatSeenFrame >= Time.frameCount - 2 ? HerRoomNow.ChatSize : Vector2.zero;
            var gap = ChatGap * _scale;
            float besideSide;
            var beside = RoomChatDock.FitBeside(area.width, side, pad, chat.x, gap, RoomWindowSize.Min * _scale, out besideSide);
            if (beside)
                side = besideSide;
            var width = side + 2f * pad;
            var height = side + title + 2f * pad;
            _frameRect = new Rect(RoomChatDock.FrameLeft(area, width, chat.x, gap, beside), area.center.y - height * 0.5f, width, height);

            // The door stands at the room's front wall, where the shell's doorway is.
            var doorWidth = side * 0.16f;
            var doorHeight = side * 0.32f;
            _doorRect = new Rect(_frameRect.center.x - doorWidth * 0.5f, _frameRect.yMax - pad - doorHeight, doorWidth, doorHeight);
            HerRoomNow.Window = _door.IsClosed ? default(Rect)
                : new Rect(_frameRect.x, Screen.height - _frameRect.yMax, _frameRect.width, _frameRect.height);
        }

        /// <summary>The door swings open on its hinge, then the room grows out of the doorway.</summary>
        void Animate()
        {
            var p = _door.Progress;
            var swing = Mathf.Clamp01(p / 0.5f);
            var grow = Mathf.Clamp01((p - 0.4f) / 0.6f);
            grow = grow * grow * (3f - 2f * grow);

            HappiUiKit.Place(_doorway, _doorRect.x, _doorRect.y, _doorRect.width, _doorRect.height);
            _doorwayImage.color = new Color(Doorway.r, Doorway.g, Doorway.b, 1f - grow);

            // The door panel turns on its left hinge: a narrowing picture, darker as it turns away.
            HappiUiKit.Place(_doorPanel, _doorRect.x, _doorRect.y, _doorRect.width, _doorRect.height);
            var turn = Mathf.Cos(swing * 82f * Mathf.Deg2Rad);
            _doorPanel.localScale = new Vector3(Mathf.Max(0.04f, turn), 1f, 1f);
            var shade = Mathf.Lerp(1f, 0.55f, swing);
            var baseColor = _wallpaper != null ? Color.white : PlainDoor;
            _doorImage.color = new Color(baseColor.r * shade, baseColor.g * shade, baseColor.b * shade, 1f - grow);
            if (_wallpaper != null)
                _doorImage.uvRect = DoorUv();

            // The room window grows from the doorway to its full size, and fades in.
            var rect = Lerp(_doorRect, _frameRect, grow);
            HappiUiKit.Place(_frame, rect.x, rect.y, rect.width, rect.height);
            _group.alpha = grow;
            _group.interactable = grow >= 0.99f;
            _group.blocksRaycasts = grow > 0.05f;

            var t = rect.width / _frameRect.width;
            var pad = Padding * _scale * t;
            var title = TitleHeight * _scale * t;
            HappiUiKit.Place(_content, pad, title + pad, Mathf.Max(1f, rect.width - 2f * pad), Mathf.Max(1f, rect.height - title - 2f * pad));
        }

        /// <summary>The part of the wallpaper behind the door (desktop pixels: the overlay window's own position).</summary>
        Rect DoorUv()
        {
            var chrome = _overlay != null ? _overlay.Chrome : null;
            int left;
            int top;
            int right;
            int bottom;
            if (chrome == null || !chrome.IsSupported || !chrome.TryGetWindowRect(out left, out top, out right, out bottom))
                return new Rect(0f, 0f, 1f, 1f);
            var x = left + Mathf.RoundToInt(_doorRect.x);
            var y = top + Mathf.RoundToInt(_doorRect.y);
            RectInt work;
            if (!MonitorWorkArea.TryGet(x, y, out work))
                return new Rect(0f, 0f, 1f, 1f);
            return WallpaperCrop.Uv(_wallpaper.width, _wallpaper.height,
                new Rect(work.x, work.y, work.width, work.height),
                new Rect(x, y, _doorRect.width, _doorRect.height));
        }

        static Rect Lerp(Rect a, Rect b, float t)
        {
            return new Rect(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t),
                Mathf.Lerp(a.width, b.width, t), Mathf.Lerp(a.height, b.height, t));
        }

        void PlaceFigures()
        {
            var side = _content.rect.width;
            var herDepth = Place(_her, _herWalk, _herArt, _routine != null ? _routine.Doing : RoomActivity.None, false, side, 0f);
            var playerDepth = Place(_player, _playerWalk, _playerArt, _playerUse.Doing, true, side, 1.7f);

            // Back to front with the furniture, by where their feet are on the floor (or on the piece they use).
            RoomDepth.Order(_depths, herDepth, playerDepth, _order);
            for (var k = 0; k < _order.Length; k++)
            {
                var item = _drawn[_order[k]];
                if (item.GetSiblingIndex() != k)
                    item.SetSiblingIndex(k);
            }

            PlaceBubble(side);
        }

        /// <summary>JUH-239: her current line over her head while the room is open; the text is set only when it changes.</summary>
        void PlaceBubble(float side)
        {
            if (_bubble == null)
                return;
            var show = _door.IsOpen && HerRoomNow.Line.Length > 0;
            if (_bubble.gameObject.activeSelf != show)
                _bubble.gameObject.SetActive(show);
            if (!show)
                return;

            var pad = BubblePad * _scale;
            if (_bubbleRevision != HerRoomNow.LineRevision)
            {
                _bubbleRevision = HerRoomNow.LineRevision;
                _bubbleText.fontSize = HappiTheme.Current.FontBody * _scale;
                _bubbleText.text = RoomChatDock.BubbleText(HerRoomNow.Line, BubbleChars);
                var maxWidth = side * BubbleWidth;
                var preferred = _bubbleText.GetPreferredValues(_bubbleText.text, maxWidth - 2f * pad, 0f);
                _bubbleSize = new Vector2(Mathf.Min(maxWidth, preferred.x + 2f * pad), preferred.y + 2f * pad);
            }

            // Over her head, kept inside the room.
            var her = _her.rectTransform;
            var x = Mathf.Clamp(her.anchorMin.x * side, _bubbleSize.x * 0.5f, side - _bubbleSize.x * 0.5f);
            var y = Mathf.Min(her.anchorMin.y * side + side * FigureFrame * HeadAbove, side - _bubbleSize.y);
            _bubble.anchorMin = _bubble.anchorMax = Vector2.zero;
            _bubble.anchoredPosition = new Vector2(x, y);
            _bubble.sizeDelta = _bubbleSize;
        }

        /// <summary>Places a character and returns its drawing depth.</summary>
        float Place(RawImage image, RoomWalker walker, CharacterArt art, RoomActivity doing, bool player, float side, float offset)
        {
            var rect = image.rectTransform;
            var feet = _layout.FeetOf(walker.X, walker.Y);
            var depth = feet.y;
            var time = Time.unscaledTime + offset;
            Texture2D texture = null;

            // In a pose she (or he, sitting) is drawn on the piece, not on the stand tile.
            RoomSlot slot;
            if (doing != RoomActivity.None && !walker.Walking && _slots.TryGet(doing, out slot))
            {
                var pose = player ? (slot.PlayerSits ? "sit" : string.Empty) : slot.Pose;
                texture = art.PoseFrame(pose, time);
                if (texture != null)
                {
                    feet = slot.PosePoint;
                    float pieceDepth;
                    depth = RoomDepth.Pose(feet.y, _pieceDepth.TryGetValue(slot.Piece, out pieceDepth) ? pieceDepth : feet.y);
                }
            }

            rect.anchorMin = rect.anchorMax = new Vector2(feet.x, 1f - feet.y);
            rect.anchoredPosition = Vector2.zero;
            var frame = side * FigureFrame;
            rect.sizeDelta = new Vector2(frame, frame);
            image.texture = texture != null ? texture : art.Frame(walker.Walking, walker.Facing, time);
            return depth;
        }

        /// <summary>A character's frames from the art pack: four idle (facing you) and four walking per direction.</summary>
        sealed class CharacterArt
        {
            public readonly Texture2D[] Idle = new Texture2D[4];
            public readonly Texture2D[] Walk = new Texture2D[16];
            readonly Dictionary<string, Texture2D[]> _poses = new Dictionary<string, Texture2D[]>(StringComparer.Ordinal);

            /// <summary>The two-frame activity poses (JUH-237): hers from the slots, his "sit". Missing ones are skipped.</summary>
            public void LoadPoses(string who, RoomSlots slots)
            {
                if (slots == null)
                {
                    AddPose(who, "sit");
                    return;
                }

                for (var i = 0; i < slots.Count; i++)
                    AddPose(who, slots[i].Pose);
            }

            void AddPose(string who, string pose)
            {
                if (string.IsNullOrEmpty(pose) || _poses.ContainsKey(pose))
                    return;
                var a = Resources.Load<Texture2D>("HerRoom/characters/" + who + "_" + pose + "_0");
                if (a == null)
                    return;
                var b = Resources.Load<Texture2D>("HerRoom/characters/" + who + "_" + pose + "_1");
                _poses[pose] = new[] { a, b != null ? b : a };
            }

            /// <summary>The pose frame now, or null when this pose has no pictures (then she stands and faces it).</summary>
            public Texture2D PoseFrame(string pose, float time)
            {
                Texture2D[] frames;
                if (string.IsNullOrEmpty(pose) || !_poses.TryGetValue(pose, out frames))
                    return null;
                return frames[RoomFrames.Pose(time)];
            }

            static readonly string[] Directions = { "down", "up", "left", "right" };

            /// <summary>Null when the first idle frame is missing; any other missing frame falls back to it.</summary>
            public static CharacterArt Load(string who)
            {
                var art = new CharacterArt();
                var first = Resources.Load<Texture2D>("HerRoom/characters/" + who + "_idle_0");
                if (first == null)
                    return null;
                for (var i = 0; i < 4; i++)
                    art.Idle[i] = Or(Resources.Load<Texture2D>("HerRoom/characters/" + who + "_idle_" + i), first);
                for (var d = 0; d < 4; d++)
                {
                    for (var i = 0; i < 4; i++)
                        art.Walk[d * 4 + i] = Or(Resources.Load<Texture2D>("HerRoom/characters/" + who + "_walk_" + Directions[d] + "_" + i), first);
                }

                return art;
            }

            static Texture2D Or(Texture2D frame, Texture2D fallback)
            {
                return frame != null ? frame : fallback;
            }

            public Texture2D Frame(bool walking, RoomFacing facing, float time)
            {
                var direction = (int)facing;
                if (walking)
                    return Walk[direction * 4 + RoomFrames.Walk(time)];
                if (facing == RoomFacing.Down)
                    return Idle[RoomFrames.Idle(time)];
                return Walk[direction * 4 + RoomFrames.StandFrame];
            }
        }

        /// <summary>
        /// Left-click: on furniture the player goes to use it (he sits where he can, otherwise he
        /// goes to it and faces it); on the floor he walks there. Right-click on furniture asks
        /// her to use it.
        /// </summary>
        void OnFloorPressed(PointerEventData eventData)
        {
            var left = eventData.button == PointerEventData.InputButton.Left;
            var right = eventData.button == PointerEventData.InputButton.Right;
            if (!_door.IsOpen || (!left && !right))
                return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, eventData.position, eventData.pressEventCamera, out local))
                return;
            var r = _content.rect;
            var point = new Vector2((local.x - r.xMin) / r.width, 1f - (local.y - r.yMin) / r.height);

            var activity = _slots.ForPiece(RoomCatalog.PieceAt(_room, point));
            if (activity == RoomActivity.None && point.x >= _layout.WindowLeft && point.x <= _layout.WindowRight
                && point.y >= _layout.WindowTop && point.y <= _layout.WindowBottom)
                activity = RoomActivity.LookWindow;

            RoomSlot slot;
            RoomTile target;
            if (right)
            {
                if (activity != RoomActivity.None
                    && _routine.Request(activity, _playerWalk.Tile, RoomClock.At(DateTime.Now).Night, out target))
                    _herWalk.WalkTo(target);
                return;
            }

            // Sleeping together is phase 3; she has the spot while she uses it.
            var herSpot = _routine.Doing == activity || _routine.Heading == activity;
            if (activity != RoomActivity.None && activity != RoomActivity.Sleep && !herSpot && _slots.TryGet(activity, out slot))
            {
                _playerUse.Plan(activity);
                _playerWalk.WalkTo(slot.Stand);
                return;
            }

            _playerUse.Cancel();
            RoomTile tile;
            if (_layout.TryTileAt(point, out tile))
                _playerWalk.WalkTo(tile);
        }

        void OnResize(Vector2 delta)
        {
            // Dragging the corner right or down makes the room bigger (screen y grows upward).
            var grow = (delta.x - delta.y) * 0.5f / Mathf.Max(0.01f, _scale);
            _wantedSide = Mathf.Max(RoomWindowSize.Min, _wantedSide + grow);
        }

        void ReadArrows()
        {
            if (_playerWalk.Walking || !CanTakeKeys())
                return;
            if (_playerUse.Doing != RoomActivity.None && (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)
                    || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow)))
                _playerUse.Cancel();
            if (Input.GetKey(KeyCode.LeftArrow))
                _playerWalk.Step(RoomFacing.Left);
            else if (Input.GetKey(KeyCode.RightArrow))
                _playerWalk.Step(RoomFacing.Right);
            else if (Input.GetKey(KeyCode.UpArrow))
                _playerWalk.Step(RoomFacing.Up);
            else if (Input.GetKey(KeyCode.DownArrow))
                _playerWalk.Step(RoomFacing.Down);
        }

        /// <summary>The arrows are the room's only while nothing else wants them (chat input, Settings, the wheel).</summary>
        bool CanTakeKeys()
        {
            if (!Application.isFocused || (_settingsView != null && _settingsView.IsOpen) || (_wheel != null && _wheel.IsOpen))
                return false;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected == null || !selected.TryGetComponent<TMP_InputField>(out var input) || !input.isFocused;
        }

        void OnDisable()
        {
            HerRoomNow.Reset();
        }

        void OnDestroy()
        {
            HerRoomNow.Reset();
            ReleaseWallpaper();
            if (_poolMask != null)
                Destroy(_poolMask);
            if (_canvas != null)
                Destroy(_canvas.gameObject);
        }
    }
}
