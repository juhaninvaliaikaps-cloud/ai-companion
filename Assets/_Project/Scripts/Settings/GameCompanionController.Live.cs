using System;
using System.Threading;
using System.Threading.Tasks;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.GameCompanion;
using AICompanion.ScreenLook;
using AICompanion.UI;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-160: live commentary for a game without an event feed. Frames are sampled only
    /// every few seconds, compared, and sent to the vision model only when the picture changed.
    /// The existing two-minute gacha look is left alone.
    /// </summary>
    public sealed partial class GameCompanionController
    {
        public const float LiveSampleSeconds = 30f;
        public const float LiveCommentGapSeconds = 45f;
        public const float LiveFrameDifference = 14f;

        bool _liveLooking;
        float _nextLiveSample;
        DateTime _lastLiveCommentUtc = DateTime.MinValue;
        int[] _lastLiveThumb;
        CancellationTokenSource _liveCancel;

        DateTime _nextPatchLookupUtc = DateTime.MinValue;
        bool _patchLooking;
        string _patchGame = string.Empty;

        void KeepLive(CompanionSettings settings, ChatPanel chat, bool inFront)
        {
            if (settings == null || !settings.GameLiveCommentary || _gachaWindow == null || !inFront)
                return;
            if (chat == null || _liveLooking || _looking || _mode != CommentaryMode.ModelFirst)
                return;
            if (Time.unscaledTime < _nextLiveSample)
                return;
            _nextLiveSample = Time.unscaledTime + LiveSampleSeconds;

            if (!chat.CanSayGameLine || !chat.CloudSeesImages)
                return;
            if ((DateTime.UtcNow - _lastLiveCommentUtc).TotalSeconds < LiveCommentGapSeconds)
                return;

            StartLiveLook(chat, _gachaWindow, _gachaGame);
        }

        void StartLiveLook(ChatPanel chat, WindowTarget target, GameTitle game)
        {
            _liveLooking = true;
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

                _main.Enqueue(() => OnLiveFrame(chat, frame, game, voice));
            });
        }

        void OnLiveFrame(ChatPanel chat, CapturedWindowFrame frame, GameTitle game, GameVoice voice)
        {
            if (!_alive || !frame.Ok)
            {
                Wipe(frame);
                _liveLooking = false;
                return;
            }

            var thumb = LiveThumb(frame);
            if (!LiveFrameChanged(_lastLiveThumb, thumb))
            {
                Wipe(frame);
                _liveLooking = false;
                return;
            }

            _lastLiveThumb = thumb;
            var attachment = Encode(frame, ScreenImageBudget.LiveMaxEdge, ScreenImageBudget.LiveQualities, ScreenImageBudget.LiveMaxBytes);
            Wipe(frame);
            if (attachment == null)
            {
                _liveLooking = false;
                return;
            }

            RequestLive(chat, attachment, game, voice);
        }

        async void RequestLive(ChatPanel chat, ChatImageAttachment attachment, GameTitle game, GameVoice voice)
        {
            if (_liveCancel != null)
                _liveCancel.Dispose();
            _liveCancel = new CancellationTokenSource();
            ChatResult result;
            try
            {
                var system = CommentaryPrompt.GameText(voice) + "\n\n" + LiveGamePrompt.Rules;
                system = GameContextNow.Append(system);
                result = await chat.RequestGameLine(system, LiveGamePrompt.Turn(game),
                    CommentaryPrompt.MaxTokens, CommentaryPrompt.Temperature, 30f, _liveCancel.Token, attachment);
            }
            catch (Exception ex)
            {
                result = new ChatResult(false, string.Empty, ChatErrorKind.Unreachable, string.Empty, ex.GetType().Name);
            }
            finally
            {
                attachment.Wipe();
                _liveLooking = false;
            }

            if (!_alive || !result.Ok || _mode != CommentaryMode.ModelFirst)
                return;
            if (LiveGamePrompt.IsSilent(result.Text))
                return;

            var line = GameCommentarySession.CleanReply(result.Text);
            if (line.Length > 0 && chat.CanSayGameLine)
            {
                Say(chat, new GameReaction { Text = line, Face = Emotion.Happy, Gesture = string.Empty }, true);
                _lastLiveCommentUtc = DateTime.UtcNow;
                GameContextNow.SetObservation(line);
            }
        }

        /// <summary>Small luminance grid; cheap enough to run on every sampled frame.</summary>
        static int[] LiveThumb(CapturedWindowFrame frame)
        {
            const int grid = 12;
            var cells = new int[grid * grid];
            if (frame.Width <= 0 || frame.Height <= 0 || frame.Bgra == null)
                return cells;

            for (var gy = 0; gy < grid; gy++)
            for (var gx = 0; gx < grid; gx++)
            {
                var x = Math.Min(frame.Width - 1, (gx * 2 + 1) * frame.Width / (grid * 2));
                var y = Math.Min(frame.Height - 1, (gy * 2 + 1) * frame.Height / (grid * 2));
                var i = (y * frame.Width + x) * 4;
                if (i + 2 >= frame.Bgra.Length)
                    continue;
                cells[gy * grid + gx] = (frame.Bgra[i] * 29 + frame.Bgra[i + 1] * 150 + frame.Bgra[i + 2] * 77) >> 8;
            }

            return cells;
        }

        static bool LiveFrameChanged(int[] previous, int[] current)
        {
            if (previous == null || current == null || previous.Length != current.Length)
                return true;
            var sum = 0;
            for (var i = 0; i < previous.Length; i++)
                sum += Math.Abs(previous[i] - current[i]);
            return sum / (float)previous.Length >= LiveFrameDifference;
        }

        /// <summary>One public patch/meta lookup per game every six hours, behind the Web permission.</summary>
        void KeepPatchContext(CompanionSettings settings, GameTitle game)
        {
            if (settings == null || !settings.GamePatchContext || game == GameTitle.Unknown)
                return;
            if (_patchLooking || DateTime.UtcNow < _nextPatchLookupUtc)
                return;
            var panel = GetComponent<SettingsPanel>();
            if (panel == null)
                return;

            _patchLooking = true;
            _nextPatchLookupUtc = DateTime.UtcNow.AddHours(6);
            _patchGame = CommentaryPrompt.GameName(game);
            GameKnowledge.Request(panel, game, context =>
            {
                _patchLooking = false;
                if (!string.IsNullOrEmpty(context) && _alive)
                {
                    Touch("Game reference loaded for " + _patchGame + ".");
                }
            });
        }
    }
}
