using System;
using AICompanion.Core;
using AICompanion.NewUI.Wheel;
using AICompanion.Overlay;
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
    /// this view only draws it. Stand-in art from Codex's drafts lives in Resources/HerRoom.
    /// </summary>
    public sealed class HerRoomView : MonoBehaviour, IOverlayKeepAwake
    {
        const string SidePref = "HerRoom.Side";
        const float TitleHeight = 34f;
        const float Padding = 8f;

        /// <summary>A 256 px character frame drawn at this fraction of the room's side.</summary>
        const float FigureFrame = 0.21f;

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
        Image _sky;
        RawImage _her;
        RawImage _player;
        RectTransform _doorway;
        Image _doorwayImage;
        RectTransform _doorPanel;
        RawImage _doorImage;

        Texture2D _shellTexture;
        Texture2D _herTexture;
        Texture2D _playerTexture;
        Texture2D _wallpaper;

        RoomLayout _layout;
        RoomWalker _herWalk;
        RoomWalker _playerWalk;
        RoomWander _wander;
        readonly RoomDoor _door = new RoomDoor();
        OverlayWindow _overlay;
        HappiSettingsView _settingsView;
        HappiWheelHost _wheel;

        float _wantedSide;
        float _scale = 1f;
        Rect _frameRect;
        Rect _doorRect;
        bool _herInFront;
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
            if (!Build())
                return;
            if (_door.IsClosed)
            {
                // She and the player come in through the door.
                _herWalk.Place(_layout.HerEntry, RoomFacing.Up);
                _playerWalk.Place(_layout.PlayerEntry, RoomFacing.Up);
                _herWalk.WalkTo(new RoomTile(_layout.HerEntry.X, _layout.HerEntry.Y - 2));
                _wander = new RoomWander(_layout, Environment.TickCount, Time.unscaledTime);
                LoadWallpaper();
            }

            _door.Open();
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
            _herTexture = Resources.Load<Texture2D>("HerRoom/happi_idle");
            _playerTexture = Resources.Load<Texture2D>("HerRoom/player_idle");
            if (_shellTexture == null || _herTexture == null || _playerTexture == null)
            {
                Debug.LogWarning("Her room: the room art is missing from Resources/HerRoom.");
                return false;
            }

            _layout = RoomLayout.V1();
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

            // The room: sky behind the shell's open window, then the shell, then the figures.
            _content = HappiUiKit.Rect(_frame, "Room");
            _sky = HappiUiKit.Panel(_content, "Sky", Color.white, false);
            Anchor(_sky.rectTransform, _layout.WindowLeft, _layout.WindowTop, _layout.WindowRight, _layout.WindowBottom);
            _shell = Raw(_content, "Shell", _shellTexture, true);
            HappiUiKit.Stretch(_shell.rectTransform, 0f, 0f, 0f, 0f);
            _shell.gameObject.AddComponent<HerRoomInput>().Pressed = OnFloorPressed;
            _player = Raw(_content, "Player", _playerTexture, false);
            _her = Raw(_content, "Her", _herTexture, false);
            Figure(_player.rectTransform);
            Figure(_her.rectTransform);

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
                HerRoomNow.IsOpen = false;
                return;
            }

            if (!_door.IsOpen)
                return;

            if (Input.GetKeyDown(KeyCode.Escape) && CanTakeKeys())
            {
                Close();
                return;
            }

            ReadArrows();
            _playerWalk.Tick(delta);
            _herWalk.Tick(delta);

            var light = RoomClock.At(DateTime.Now);
            RoomTile target;
            if (_wander.TryNext(Time.unscaledTime, _herWalk.Tile, _herWalk.Walking, _playerWalk.Tile, light.Night, out target))
                _herWalk.WalkTo(target);
            if (!_herWalk.Walking && RoomWander.Close(_herWalk.Tile, _playerWalk.Tile))
                _herWalk.Face(_playerWalk.Tile);

            _shell.color = light.Tint;
            _her.color = light.Tint;
            _player.color = light.Tint;
            _sky.color = light.Sky;
            PlaceFigures();
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
            var width = side + 2f * pad;
            var height = side + title + 2f * pad;
            _frameRect = new Rect(area.center.x - width * 0.5f, area.center.y - height * 0.5f, width, height);

            // The door stands at the room's front wall, where the shell's doorway is.
            var doorWidth = side * 0.16f;
            var doorHeight = side * 0.32f;
            _doorRect = new Rect(area.center.x - doorWidth * 0.5f, _frameRect.yMax - pad - doorHeight, doorWidth, doorHeight);
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
            Place(_her.rectTransform, _herWalk, side);
            Place(_player.rectTransform, _playerWalk, side);

            // Back to front: the one lower on the floor is drawn over the other.
            var herInFront = _herWalk.Y >= _playerWalk.Y;
            if (herInFront != _herInFront)
            {
                _herInFront = herInFront;
                (herInFront ? _her.rectTransform : _player.rectTransform).SetAsLastSibling();
            }
        }

        void Place(RectTransform rect, RoomWalker walker, float side)
        {
            var feet = _layout.FeetOf(walker.X, walker.Y);
            rect.anchorMin = rect.anchorMax = new Vector2(feet.x, 1f - feet.y);
            // Until the walk frames exist, a small bob shows she is walking.
            var bob = walker.Walking ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 11f)) * side * 0.006f : 0f;
            rect.anchoredPosition = new Vector2(0f, bob);
            var frame = side * FigureFrame;
            rect.sizeDelta = new Vector2(frame, frame);
        }

        void OnFloorPressed(PointerEventData eventData)
        {
            if (!_door.IsOpen || eventData.button != PointerEventData.InputButton.Left)
                return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, eventData.position, eventData.pressEventCamera, out local))
                return;
            var r = _content.rect;
            var point = new Vector2((local.x - r.xMin) / r.width, 1f - (local.y - r.yMin) / r.height);
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
            HerRoomNow.IsOpen = false;
        }

        void OnDestroy()
        {
            HerRoomNow.IsOpen = false;
            ReleaseWallpaper();
            if (_canvas != null)
                Destroy(_canvas.gameObject);
        }
    }
}
