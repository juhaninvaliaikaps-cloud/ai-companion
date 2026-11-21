using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.GameCompanion;
using AICompanion.ScreenLook;
using AICompanion.UI;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-147: gacha games. Happi finds the game window by process name or title, and while
    /// it is in front she looks every 2 minutes or on Ctrl+Shift+G. The window is captured the
    /// way OBS does (Windows Graphics Capture), downscaled, sent once to the cloud vision model,
    /// and wiped. Nothing reads or touches the game process.
    /// </summary>
    public sealed partial class GameCompanionController
    {
        public const float GachaScanSeconds = 3f;
        public const float GachaLookTimeoutSeconds = 15f;
        public const string LookHotkey = "Ctrl+Shift+G";

        readonly GachaLookSchedule _look = new GachaLookSchedule();
        readonly Dictionary<int, string> _processNames = new Dictionary<int, string>();
        readonly ConcurrentQueue<Action> _main = new ConcurrentQueue<Action>();
        WindowTarget _gachaWindow;
        GameTitle _gachaGame = GameTitle.Unknown;
        float _nextGachaScan;
        bool _looking;
        bool _hotkeyDown;
        CancellationTokenSource _lookCancel;

        /// <summary>The gacha game Happi sees running now, or Unknown.</summary>
        public GameTitle GachaGame
        {
            get { return _gachaWindow != null ? _gachaGame : GameTitle.Unknown; }
        }

        public static bool Enabled(CompanionSettings settings, GameTitle game)
        {
            if (settings == null)
                return false;
            switch (game)
            {
                case GameTitle.GenshinImpact: return settings.GameGenshin;
                case GameTitle.HonkaiStarRail: return settings.GameStarRail;
                case GameTitle.ZenlessZoneZero: return settings.GameZzz;
                case GameTitle.ArknightsEndfield: return settings.GameEndfield;
                case GameTitle.NevernessToEverness: return settings.GameNte;
                default: return false;
            }
        }

        static bool AnyGacha(CompanionSettings settings)
        {
            return settings.GameGenshin || settings.GameStarRail || settings.GameZzz || settings.GameEndfield || settings.GameNte;
        }

        /// <summary>The "Look now" button in the Games section, the same as the hotkey.</summary>
        public void LookNow()
        {
            if (_gachaWindow == null)
            {
                Touch("No gacha game window is open.");
                return;
            }

            _look.AskNow();
        }

        void KeepGacha(CompanionSettings settings, ChatPanel chat)
        {
            Action pending;
            while (_main.TryDequeue(out pending))
                pending();

            if (!AnyGacha(settings))
            {
                _gachaWindow = null;
                KeepPulls(settings);
                return;
            }

            if (Time.unscaledTime >= _nextGachaScan)
            {
                _nextGachaScan = Time.unscaledTime + GachaScanSeconds;
                ScanGachaWindow(settings);
            }

            var now = DateTime.UtcNow;
            var inFront = _gachaWindow != null && !_gachaWindow.Minimized && ForegroundIs(_gachaWindow.Hwnd);
            _look.Front(inFront, now);

            var hotkey = HotkeyHeld();
            if (hotkey && !_hotkeyDown && _gachaWindow != null)
                _look.AskNow();
            _hotkeyDown = hotkey;

            KeepPulls(settings);

            bool asked;
            if (_looking || !settings.GameScreenLooks || !_look.Due(now, out asked))
                return;

            string reason;
            if (!LooksAllowed(chat, out reason))
            {
                _look.Looked(now);
                if (asked)
                    Touch(reason);
                return;
            }

            StartLook(chat, _gachaWindow, _gachaGame, asked);
            _look.Looked(now);
        }

        bool LooksAllowed(ChatPanel chat, out string reason)
        {
            reason = string.Empty;
            if (_mode != CommentaryMode.ModelFirst)
            {
                reason = _modeReason.Length > 0 ? _modeReason : "Screen looks need the cloud.";
                return false;
            }

            if (chat == null || !chat.CloudSeesImages)
            {
                reason = ChatPanel.GameLineNoVision;
                return false;
            }

            if (!chat.CanSayGameLine)
            {
                reason = "She's answering you, so she didn't look.";
                return false;
            }

            if (!WindowCapture.IsSupported)
            {
                reason = ScreenLookPolicy.PlayerOnly;
                return false;
            }

            return true;
        }

        void ScanGachaWindow(CompanionSettings settings)
        {
            WindowTarget found = null;
            var game = GameTitle.Unknown;
            IReadOnlyList<WindowTarget> windows;
            try
            {
                windows = WindowCapture.ListWindows();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Game companion: couldn't list windows (" + ex.GetType().Name + ")");
                return;
            }

            for (var i = 0; i < windows.Count && found == null; i++)
            {
                var window = windows[i];
                if (window == null || window.IsOwnProcess || window.IsShell)
                    continue;
                var match = GachaGames.Match(ProcessName(window.ProcessId), window.Title);
                if (match != GameTitle.Unknown && Enabled(settings, match))
                {
                    found = window;
                    game = match;
                }
            }

            var before = GachaGame;
            _gachaWindow = found;
            _gachaGame = game;
            if (before != GachaGame)
                Touch(found != null ? CommentaryPrompt.GameName(game) + " is open." : "The gacha game closed.");
        }

        string ProcessName(int pid)
        {
            if (pid <= 0)
                return string.Empty;
            string name;
            if (_processNames.TryGetValue(pid, out name))
                return name;
            try
            {
                using (var process = System.Diagnostics.Process.GetProcessById(pid))
                    name = process.ProcessName ?? string.Empty;
            }
            catch (Exception)
            {
                name = string.Empty;
            }

            if (_processNames.Count > 256)
                _processNames.Clear();
            _processNames[pid] = name;
            return name;
        }

        void StartLook(ChatPanel chat, WindowTarget target, GameTitle game, bool asked)
        {
            _looking = true;
            var voice = CurrentVoice();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                CapturedWindowFrame frame;
                try
                {
                    frame = WindowCapture.CaptureWindow(target);
                }
                catch (Exception ex)
                {
                    frame = CapturedWindowFrame.Fail(CaptureDiagnostics.Stopped(ex.GetType().Name, 0));
                }

                _main.Enqueue(() => OnLookFrame(chat, frame, game, asked, voice));
            });
        }

        void OnLookFrame(ChatPanel chat, CapturedWindowFrame frame, GameTitle game, bool asked, GameVoice voice)
        {
            if (!_alive || !frame.Ok)
            {
                Wipe(frame);
                _looking = false;
                if (_alive && asked)
                    Touch(string.IsNullOrEmpty(frame.Error) ? "The look didn't work." : frame.Error);
                return;
            }

            if (_mode != CommentaryMode.ModelFirst)
            {
                // Private mode, 18+ or consent changed while the frame was being captured.
                Wipe(frame);
                _looking = false;
                return;
            }

            var attachment = Encode(frame);
            Wipe(frame);
            if (attachment == null)
            {
                _looking = false;
                Touch("The game window looked blank. If it stays black, the game may be running as administrator; run Happi the same way.");
                return;
            }

            RequestLook(chat, attachment, game, asked, voice);
        }

        async void RequestLook(ChatPanel chat, ChatImageAttachment attachment, GameTitle game, bool asked, GameVoice voice)
        {
            if (_lookCancel != null)
                _lookCancel.Dispose();
            _lookCancel = new CancellationTokenSource();
            ChatResult result;
            try
            {
                result = await chat.RequestGameLine(CommentaryPrompt.GameText(voice) + "\n\n" + GachaLookPrompt.Rules,
                    GachaLookPrompt.Turn(game, asked), CommentaryPrompt.MaxTokens, CommentaryPrompt.Temperature, GachaLookTimeoutSeconds,
                    _lookCancel.Token, attachment);
            }
            catch (Exception ex)
            {
                result = new ChatResult(false, string.Empty, ChatErrorKind.Unreachable, string.Empty, ex.GetType().Name);
            }
            finally
            {
                attachment.Wipe();
                _looking = false;
            }

            if (!_alive)
                return;
            if (!result.Ok)
            {
                Touch(string.IsNullOrEmpty(result.UserMessage) ? "The look didn't answer." : result.UserMessage);
                Debug.Log("Game companion: gacha look failed (" + result.LogMessage + ")");
                return;
            }

            if (GachaLookPrompt.IsSilent(result.Text))
            {
                Touch("She looked and had nothing to say.");
                return;
            }

            var line = GameCommentarySession.CleanReply(result.Text);
            if (chat != null && chat.CanSayGameLine)
                Say(chat, new GameReaction { Text = line, Face = Emotion.Happy, Gesture = string.Empty }, true);
        }

        ChatImageAttachment Encode(CapturedWindowFrame frame)
        {
            byte[] small;
            int width;
            int height;
            ScreenImageBudget.Downscale(frame.Bgra, frame.Width, frame.Height, frame.Width * 4, out small, out width, out height, ScreenImageBudget.MaxEdge, true);
            if (small == null || ScreenImageBudget.LooksBlank(small, width, height))
                return null;

            var rgba = ScreenImageBudget.BgraTopDownToRgbaBottomUp(small, width, height);
            Array.Clear(small, 0, small.Length);
            int quality;
            var jpeg = ScreenImageBudget.FitJpeg(q => EncodeJpeg(rgba, width, height, q), out quality);
            Array.Clear(rgba, 0, rgba.Length);
            if (jpeg == null || jpeg.Length == 0)
                return null;
            return new ChatImageAttachment { JpegBytes = jpeg, Width = width, Height = height };
        }

        byte[] EncodeJpeg(byte[] rgba, int width, int height, int quality)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                tex.LoadRawTextureData(rgba);
                tex.Apply(false, false);
                return ImageConversion.EncodeToJPG(tex, quality);
            }
            finally
            {
                Destroy(tex);
            }
        }

        static void Wipe(CapturedWindowFrame frame)
        {
            if (frame.Bgra != null && frame.Bgra.Length > 0)
                Array.Clear(frame.Bgra, 0, frame.Bgra.Length);
        }

        void StopGacha()
        {
            if (_lookCancel != null)
            {
                _lookCancel.Cancel();
                _lookCancel.Dispose();
                _lookCancel = null;
            }

            _gachaWindow = null;
            _looking = false;
            ResetPulls(GameTitle.Unknown);
        }

        // ---- Windows: which window is in front, and the global hotkey ------------------------

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int virtualKey);

        static bool ForegroundIs(long hwnd)
        {
            return hwnd != 0 && GetForegroundWindow().ToInt64() == hwnd;
        }

        /// <summary>Ctrl+Shift+G, read with GetAsyncKeyState so it works while the game has focus.</summary>
        static bool HotkeyHeld()
        {
            const int control = 0x11;
            const int shift = 0x10;
            const int g = 0x47;
            return (GetAsyncKeyState(control) & 0x8000) != 0 && (GetAsyncKeyState(shift) & 0x8000) != 0 && (GetAsyncKeyState(g) & 0x8000) != 0;
        }
#else
        static bool ForegroundIs(long hwnd)
        {
            return false;
        }

        static bool HotkeyHeld()
        {
            return false;
        }
#endif
    }
}
