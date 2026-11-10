using System;
using System.Collections.Generic;
using System.Threading;
using AICompanion.AI;
using AICompanion.Core;
using AICompanion.ScreenLook;
using AICompanion.UI;
using AICompanion.Voice;
using UnityEngine;

namespace AICompanion.Settings
{
    /// <summary>
    /// One opted-in window, a local preview, and a single "what do you see?" request.
    /// Capture runs off the Unity thread. Pixels are not written to disk or the log.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class ScreenLookController : MonoBehaviour, IScreenLook, IOverlayKeepAwake
    {
        readonly ScreenLookGate _gate = new ScreenLookGate();
        readonly Queue<Action> _main = new Queue<Action>();
        WindowTarget _selected;
        Texture2D _preview;
        bool _waitingForChat;
        bool _alive = true;
        int _activeGeneration;

        public int Revision { get; private set; }

        public bool IsBusy => _gate.Busy || _waitingForChat;

        public bool KeepFrameRateActive => IsBusy;

        public string StatusText { get; private set; } = ScreenLookPolicy.FeatureOff;

        public WindowTarget Selected => _selected;

        public Texture2D PreviewTexture => _preview;

        public IReadOnlyList<WindowTarget> Windows { get; private set; } = new WindowTarget[0];

        void Update()
        {
            Drain();
            if (!_waitingForChat)
                return;

            var chat = GetComponent<ChatPanel>();
            if (chat != null && chat.TranscriptBusy)
                return;

            _waitingForChat = false;
            _gate.Leave(_activeGeneration);
            Touch("Ready");
        }

        void OnDisable()
        {
            if (_alive && IsBusy)
                Stop();
        }

        void OnDestroy()
        {
            _alive = false;
            _gate.Cancel();
            _waitingForChat = false;
            Drain();
            if (_preview != null)
                Destroy(_preview);
            _preview = null;
        }

        public void RequestLook()
        {
            if (IsBusy)
                return;

            var input = BuildInput();
            var decision = ScreenLookPolicy.Evaluate(input);
            if (!decision.AllowSend)
            {
                Note(decision.Message);
                Touch(decision.Message);
                return;
            }

            if (!WindowCapture.IsSupported)
            {
                Note(ScreenLookPolicy.PlayerOnly);
                Touch(ScreenLookPolicy.PlayerOnly);
                return;
            }

            if (!_gate.TryEnter(out var generation))
            {
                Note(ScreenLookPolicy.Busy);
                Touch(ScreenLookPolicy.Busy);
                return;
            }

            _activeGeneration = generation;
            Touch("Capturing the selected window.");
            var target = _selected;
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

                Post(() => OnLookFrame(generation, frame));
            });
        }

        public void Stop()
        {
            WindowCapture.CancelActive();
            var sendInFlight = _waitingForChat;
            _gate.Cancel();
            _waitingForChat = false;
            if (sendInFlight)
            {
                var chat = GetComponent<ChatPanel>();
                if (chat != null)
                    chat.CancelActiveReply();
            }

            Touch("Look cancelled.");
        }

        public void ClearSelection()
        {
            Stop();
            _selected = null;
            if (_preview != null)
                Destroy(_preview);
            _preview = null;
            Touch(Enabled() ? "No window selected." : ScreenLookPolicy.FeatureOff);
        }

        public void RefreshWindows()
        {
            if (!Enabled())
            {
                Windows = new WindowTarget[0];
                Touch(ScreenLookPolicy.FeatureOff);
                return;
            }

            if (!WindowCapture.IsSupported)
            {
                Windows = new WindowTarget[0];
                Touch(ScreenLookPolicy.PlayerOnly);
                return;
            }

            Windows = WindowCapture.ListWindows();
            Touch(Windows.Count == 0 ? "No other windows were found." : "Choose a window. The desktop is not in this list.");
        }

        public void Select(WindowTarget target)
        {
            if (IsBusy || !Enabled())
                return;

            _selected = target;
            var decision = ScreenLookPolicy.Evaluate(BuildInput());
            if (!decision.AllowPreview)
            {
                Note(decision.Message);
                Touch(decision.Message);
                return;
            }

            if (!WindowCapture.IsSupported)
            {
                Touch(ScreenLookPolicy.PlayerOnly);
                return;
            }

            if (!_gate.TryEnter(out var generation))
                return;

            _activeGeneration = generation;
            Touch("Capturing a local preview.");
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

                Post(() => OnPreviewFrame(generation, frame));
            });
        }

        public void DisableFeature()
        {
            ClearSelection();
            Windows = new WindowTarget[0];
            Touch(ScreenLookPolicy.FeatureOff);
        }

        void OnPreviewFrame(int generation, CapturedWindowFrame frame)
        {
            if (!_alive)
            {
                Wipe(frame);
                return;
            }

            if (!_gate.IsCurrent(generation))
            {
                Wipe(frame);
                Log(false, 0, 0, CaptureDiagnostics.Stopped("Stale", 0));
                return;
            }

            _gate.Leave(generation);
            if (!frame.Ok)
            {
                Wipe(frame);
                Note(frame.Error);
                Touch(frame.Error);
                Log(false, 0, 0, frame.Error);
                return;
            }

            ShowPreview(frame);
            Log(true, frame.Width, frame.Height, null);
            Wipe(frame);
            Touch("Preview is local only. Nothing has been sent.");
        }

        void OnLookFrame(int generation, CapturedWindowFrame frame)
        {
            if (!_alive)
            {
                Wipe(frame);
                return;
            }

            if (!_gate.IsCurrent(generation))
            {
                Wipe(frame);
                Log(false, 0, 0, CaptureDiagnostics.Stopped("Stale", 0));
                Note(ScreenLookPolicy.Stale);
                return;
            }

            if (!frame.Ok || !CaptureDiagnostics.AllowsUpload(frame.Ok, false, false, false, false))
            {
                _gate.Leave(generation);
                Wipe(frame);
                var message = string.IsNullOrEmpty(frame.Error) ? CaptureDiagnostics.Stopped("Frame", 0) : frame.Error;
                Note(message);
                Touch(message);
                Log(false, 0, 0, message);
                return;
            }

            var decision = ScreenLookPolicy.Evaluate(BuildInput());
            if (!decision.AllowSend)
            {
                _gate.Leave(generation);
                Wipe(frame);
                Note(decision.Message);
                Touch(decision.Message);
                Log(false, 0, 0, decision.Message);
                return;
            }

            ScreenImageBudget.Downscale(frame.Bgra, frame.Width, frame.Height, frame.Width * 4, out var small, out var width, out var height);
            Wipe(frame);
            if (small == null || ScreenImageBudget.LooksBlank(small, width, height))
            {
                _gate.Leave(generation);
                Note(ScreenLookPolicy.Blank);
                Touch(ScreenLookPolicy.Blank);
                Log(false, 0, 0, ScreenLookPolicy.Blank);
                return;
            }

            var rgba = ScreenImageBudget.BgraTopDownToRgbaBottomUp(small, width, height);
            ApplyTexture(rgba, width, height);
            var jpeg = ScreenImageBudget.FitJpeg(quality => EncodeJpeg(rgba, width, height, quality), out _);
            if (jpeg == null)
            {
                _gate.Leave(generation);
                Note(VisionCapability.PayloadRefusal);
                Touch(VisionCapability.PayloadRefusal);
                Log(false, 0, 0, VisionCapability.PayloadRefusal);
                return;
            }

            Log(true, width, height, null);

            if (!_gate.IsCurrent(generation))
            {
                Array.Clear(jpeg, 0, jpeg.Length);
                Note(ScreenLookPolicy.Stale);
                return;
            }

            var chat = GetComponent<ChatPanel>();
            var attachment = new ChatImageAttachment
            {
                JpegBytes = jpeg,
                Width = width,
                Height = height,
                Generation = generation
            };
            var title = _selected != null ? _selected.Title : string.Empty;
            _waitingForChat = true;
            var started = chat != null && chat.SubmitObservation(attachment, title);
            if (!started)
            {
                attachment.Wipe();
                _waitingForChat = false;
                _gate.Leave(generation);
                var message = chat == null ? ScreenLookPolicy.PlayerOnly : "Look did not start. Nothing was sent.";
                Touch(message);
                Log(false, 0, 0, message);
                return;
            }

            Touch("Looking. The selected window image is being sent to the cloud vision provider.");
        }

        byte[] EncodeJpeg(byte[] rgba, int width, int height, int quality)
        {
            if (rgba == null || width <= 0 || height <= 0)
                return null;

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

        void ShowPreview(CapturedWindowFrame frame)
        {
            ScreenImageBudget.Downscale(frame.Bgra, frame.Width, frame.Height, frame.Width * 4, out var small, out var width, out var height);
            if (small == null)
                return;
            ApplyTexture(ScreenImageBudget.BgraTopDownToRgbaBottomUp(small, width, height), width, height);
        }

        void ApplyTexture(byte[] rgba, int width, int height)
        {
            if (rgba == null || width <= 0 || height <= 0)
                return;
            if (_preview != null && (_preview.width != width || _preview.height != height))
            {
                Destroy(_preview);
                _preview = null;
            }

            if (_preview == null)
            {
                _preview = new Texture2D(width, height, TextureFormat.RGBA32, false);
                _preview.hideFlags = HideFlags.HideAndDontSave;
            }

            _preview.LoadRawTextureData(rgba);
            _preview.Apply(false, false);
        }

        ScreenLookInput BuildInput()
        {
            var settings = GetComponent<SettingsPanel>();
            var current = settings != null ? settings.CurrentSettings : null;
            var privateMode = current != null && current.PrivateMode;
            var selected = current != null ? current.ProviderId : ChatDefaults.GrokId;
            return new ScreenLookInput
            {
                FeatureEnabled = current != null && current.ScreenLookEnabled,
                CloudConsent = current != null && current.ScreenCloudConsent,
                PrivateMode = privateMode,
                AllowCloud = OutboundPrivacy.AllowsCloudVision(privateMode),
                EffectiveProviderId = OutboundPrivacy.ChatProviderId(privateMode, selected),
                CloudModel = current != null ? current.Model : ChatDefaults.Model,
                Busy = false,
                Automatic = false,
                MonitorRequested = false,
                Target = _selected
            };
        }

        bool Enabled()
        {
            var settings = GetComponent<SettingsPanel>();
            return settings != null && settings.CurrentSettings != null && settings.CurrentSettings.ScreenLookEnabled;
        }

        void Log(bool ok, int width, int height, string error)
        {
            Debug.Log(CaptureDiagnostics.LogLine(ok, width, height, error));
        }

        void Note(string message)
        {
            var chat = GetComponent<ChatPanel>();
            if (chat != null)
                chat.ShowSpeechNote(message);
        }

        void Touch(string status)
        {
            StatusText = status ?? string.Empty;
            Revision++;
        }

        void Wipe(CapturedWindowFrame frame)
        {
            if (frame.Bgra != null && frame.Bgra.Length > 0)
                Array.Clear(frame.Bgra, 0, frame.Bgra.Length);
        }

        void Post(Action action)
        {
            if (action == null)
                return;
            lock (_main)
                _main.Enqueue(action);
        }

        void Drain()
        {
            while (true)
            {
                Action action = null;
                lock (_main)
                {
                    if (_main.Count == 0)
                        return;
                    action = _main.Dequeue();
                }

                try
                {
                    action();
                }
                catch (Exception)
                {
                    _gate.Cancel();
                    _waitingForChat = false;
                    Touch(ScreenLookPolicy.CaptureFailed);
                }
            }
        }
    }
}
