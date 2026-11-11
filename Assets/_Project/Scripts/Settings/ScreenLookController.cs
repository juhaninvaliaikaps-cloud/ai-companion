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
        readonly ScreenLookInput _buttonInput = new ScreenLookInput();
        readonly Queue<Action> _main = new Queue<Action>();
        WindowTarget _selected;
        MonitorTarget _monitor;
        Texture2D _preview;
        bool _automaticLook;
        float _lastAutoLook = -1000f;
        public const float AutoLookCooldownSeconds = 20f;
        bool _waitingForChat;
        bool _alive = true;
        CancellationTokenSource _visionCancel;
        int _activeGeneration;

        public int Revision { get; private set; }

        public bool IsBusy => _gate.Busy || _waitingForChat;

        public bool SendEnabled { get; private set; }

        public bool LookButtonInteractable => ScreenLookPolicy.LookButtonInteractable(SendEnabled, IsBusy);

        public bool KeepFrameRateActive => IsBusy;

        public string StatusText { get; private set; } = ScreenLookPolicy.FeatureOff;

        /// <summary>Sizes of the last encoded frame. No pixels and no window text.</summary>
        public string DeliveryNote { get; private set; } = string.Empty;

        public WindowTarget Selected => _selected;

        public Texture2D PreviewTexture => _preview;

        /// <summary>Size and byte count of the exact frame shown in the preview. No pixels or text.</summary>
        public string PreviewDetails { get; private set; } = string.Empty;

        public IReadOnlyList<WindowTarget> Windows { get; private set; } = new WindowTarget[0];

        public IReadOnlyList<MonitorTarget> Monitors { get; private set; } = new MonitorTarget[0];

        public MonitorTarget SelectedMonitor => _monitor;

        public bool AutoLookEnabled
        {
            get
            {
                var settings = CurrentSettings();
                return settings != null && settings.ScreenLookEnabled && settings.ScreenAutoLook && !settings.ScreenAutoLookPaused;
            }
        }

        public bool WholeMonitorMode
        {
            get
            {
                var settings = CurrentSettings();
                return settings != null && settings.ScreenCaptureWholeMonitor;
            }
        }

        CompanionSettings CurrentSettings()
        {
            var panel = GetComponent<SettingsPanel>();
            return panel != null ? panel.CurrentSettings : null;
        }

        CapturedWindowFrame CaptureSelected()
        {
            var settings = CurrentSettings();
            if (settings != null && settings.ScreenCaptureWholeMonitor && _monitor != null)
                return WindowCapture.CaptureMonitor(_monitor);
            return WindowCapture.CaptureWindow(_selected);
        }

        public void RefreshMonitors()
        {
            Monitors = MonitorList.Read();
            var settings = CurrentSettings();
            var device = settings != null ? settings.ScreenMonitorDevice : string.Empty;
            MonitorTarget chosen = null;
            for (var i = 0; i < Monitors.Count; i++)
            {
                if (!string.IsNullOrEmpty(device) && Monitors[i].DeviceName == device)
                {
                    chosen = Monitors[i];
                    break;
                }
            }

            if (chosen == null)
            {
                for (var i = 0; i < Monitors.Count; i++)
                {
                    if (Monitors[i].IsPrimary)
                    {
                        chosen = Monitors[i];
                        break;
                    }
                }
            }

            _monitor = chosen != null ? chosen : (Monitors.Count > 0 ? Monitors[0] : null);
        }

        public void SelectMonitor(MonitorTarget monitor)
        {
            if (monitor == null || IsBusy || !Enabled())
                return;

            _monitor = monitor;
            var settings = CurrentSettings();
            if (settings != null)
                settings.ScreenMonitorDevice = monitor.DeviceName;
            PreviewMonitor();
        }

        void PreviewMonitor()
        {
            if (!WindowCapture.IsSupported || _monitor == null)
                return;
            if (!_gate.TryEnter(out var generation))
                return;

            _activeGeneration = generation;
            var target = _monitor;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                CapturedWindowFrame frame;
                try
                {
                    frame = WindowCapture.CaptureMonitor(target);
                }
                catch (Exception ex)
                {
                    frame = CapturedWindowFrame.Fail(CaptureDiagnostics.Stopped(ex.GetType().Name, 0));
                }

                Post(() => OnPreviewFrame(generation, frame));
            });
        }

        void Update()
        {
            Drain();
            RefreshSendEnabled();
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

            StartCapture(false);
        }

        /// <summary>
        /// A look she asked for herself, through the [look] cue. Local only, rate limited,
        /// and it needs the separate Let her look switch. The reply is written only when the
        /// observation has something worth saying.
        /// </summary>
        public bool RequestAutoLook()
        {
            var settings = CurrentSettings();
            if (!Enabled() || settings == null || !settings.ScreenAutoLook || settings.ScreenAutoLookPaused)
                return false;
            if (IsBusy)
                return false;
            if (Time.unscaledTime - _lastAutoLook < AutoLookCooldownSeconds)
                return false;

            if (WholeMonitorMode && _monitor == null)
                RefreshMonitors();
            var input = BuildInput();
            input.Automatic = true;
            input.AutoLookConsent = settings.ScreenAutoLook;
            var decision = ScreenLookPolicy.Evaluate(input);
            if (decision.Route != ScreenLookRoute.Local || !decision.AllowSend)
                return false;
            if (!WindowCapture.IsSupported)
                return false;

            _lastAutoLook = Time.unscaledTime;
            StartCapture(true);
            return true;
        }

        void StartCapture(bool automatic)
        {
            var input = BuildInput();
            if (automatic)
            {
                input.Automatic = true;
                input.AutoLookConsent = true;
            }

            var decision = ScreenLookPolicy.Evaluate(input);
            Debug.Log(CaptureDiagnostics.RouteLine(
                decision.Route.ToString(),
                decision.SendButtonEnabled,
                input.CloudConsent,
                input.PrivateMode,
                input.EffectiveProviderId,
                input.LocalVisionModel,
                input.ChatModel));
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

            if (WholeMonitorMode && _monitor == null)
                RefreshMonitors();

            if (!_gate.TryEnter(out var generation))
            {
                Note(ScreenLookPolicy.Busy);
                Touch(ScreenLookPolicy.Busy);
                return;
            }

            _activeGeneration = generation;
            _automaticLook = automatic;
            Touch(automatic ? "She is having a quick look." : (WholeMonitorMode ? "Capturing the selected display." : "Capturing the selected window."));
            ThreadPool.QueueUserWorkItem(_ =>
            {
                CapturedWindowFrame frame;
                try
                {
                    frame = CaptureSelected();
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
            if (_visionCancel != null)
                _visionCancel.Cancel();
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
            DeliveryNote = string.Empty;
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
            if (decision.Route == ScreenLookRoute.None)
            {
                _gate.Leave(generation);
                Wipe(frame);
                Note(decision.Message);
                Touch(decision.Message);
                Log(false, 0, 0, decision.Message);
                return;
            }

            var localFrame = decision.Route == ScreenLookRoute.Local;
            var maxEdge = localFrame ? ScreenImageBudget.LocalMaxEdge : ScreenImageBudget.MaxEdge;
            // Local vision resizes to the exact cap with bilinear sampling: a 2560-wide display
            // becomes 1600 wide, not the 1280 an integer 2x step would give. Nearest sampling
            // drops the thin strokes of small text before the model can see them.
            byte[] small;
            int width;
            int height;
            if (localFrame && Mathf.Max(frame.Width, frame.Height) > maxEdge)
                ScreenImageBudget.DownscaleFit(frame.Bgra, frame.Width, frame.Height, frame.Width * 4, out small, out width, out height, maxEdge);
            else
                ScreenImageBudget.Downscale(frame.Bgra, frame.Width, frame.Height, frame.Width * 4, out small, out width, out height, maxEdge, true);
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
            int quality;
            var jpeg = localFrame
                ? ScreenImageBudget.FitJpeg(q => EncodeJpeg(rgba, width, height, q), out quality, ScreenImageBudget.LocalMaxBytes, ScreenImageBudget.LocalQualities)
                : ScreenImageBudget.FitJpeg(q => EncodeJpeg(rgba, width, height, q), out quality);
            if (jpeg == null)
            {
                _gate.Leave(generation);
                Note(VisionCapability.PayloadRefusal);
                Touch(VisionCapability.PayloadRefusal);
                Log(false, 0, 0, VisionCapability.PayloadRefusal);
                return;
            }

            var scale = ScreenImageBudget.ScaleFor(frame.Width, frame.Height, maxEdge);
            var imageChars = OllamaVision.ImageFieldChars(jpeg);
            // Replace the quick preview with the exact JPEG that is sent, so the 1:1 inspection
            // shows decoder output and not the pre-encode buffer.
            ApplyJpeg(jpeg);
            DeliveryNote = "Preview is the exact image encoded for this look. Capture " + frame.Width + "x" + frame.Height
                + ", sent " + width + "x" + height
                + ", scale " + scale
                + ", JPEG " + jpeg.Length + " bytes, quality " + quality
                + ". It stays in memory on this PC and is not saved.";
            Log(true, width, height, null);
            Debug.Log(CaptureDiagnostics.FrameLine(decision.Route.ToString(), frame.Width, frame.Height, width, height, scale, quality, jpeg.Length, imageChars, localFrame ? OllamaVision.AskPrompt.Length : VisionPrompt.Addendum.Length));
            Touch(localFrame ? "Local look started. The preview is the encoded image." : "Cloud look started. The preview is the encoded image.");

            if (decision.Route == ScreenLookRoute.Local)
            {
                Wipe(frame);
                RunLocalVision(generation, jpeg);
                return;
            }

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
            Debug.Log(CaptureDiagnostics.ChatLine("grok", true, "jpegBytes", jpeg.Length, started));
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

        /// <summary>Decode the encoded JPEG so the preview and the 1:1 inspection are exact.</summary>
        void ApplyJpeg(byte[] jpeg)
        {
            if (jpeg == null || jpeg.Length == 0)
                return;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, jpeg, false))
            {
                Destroy(tex);
                return;
            }

            tex.hideFlags = HideFlags.HideAndDontSave;
            if (_preview != null)
                Destroy(_preview);
            _preview = tex;
            PreviewDetails = "Encoded frame " + tex.width + "x" + tex.height
                + ", JPEG " + jpeg.Length + " bytes. This is the decoded image that was sent.";
        }

        void ShowPreview(CapturedWindowFrame frame)
        {
            var local = ScreenLookPolicy.UseLocalVision(BuildInput());
            var edge = local ? ScreenImageBudget.LocalMaxEdge : ScreenImageBudget.MaxEdge;
            byte[] small;
            int width;
            int height;
            if (local && Mathf.Max(frame.Width, frame.Height) > edge)
                ScreenImageBudget.DownscaleFit(frame.Bgra, frame.Width, frame.Height, frame.Width * 4, out small, out width, out height, edge);
            else
                ScreenImageBudget.Downscale(frame.Bgra, frame.Width, frame.Height, frame.Width * 4, out small, out width, out height, edge, true);
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

        public ScreenLookDecision CurrentDecision()
        {
            return ScreenLookPolicy.Evaluate(BuildInput());
        }

        public bool PlansLocalVision()
        {
            return ScreenLookPolicy.UseLocalVision(BuildInput());
        }

        ScreenLookInput BuildInput()
        {
            var input = new ScreenLookInput();
            Fill(input);
            return input;
        }

        void RefreshSendEnabled()
        {
            Fill(_buttonInput);
            var send = ScreenLookPolicy.Evaluate(_buttonInput).SendButtonEnabled;
            if (send == SendEnabled)
                return;
            SendEnabled = send;
            Revision++;
        }

        void Fill(ScreenLookInput input)
        {
            var settings = GetComponent<SettingsPanel>();
            var current = settings != null ? settings.CurrentSettings : null;
            var privateMode = current != null && current.PrivateMode;
            var selected = current != null ? current.ProviderId : ChatDefaults.GrokId;
            input.FeatureEnabled = current != null && current.ScreenLookEnabled;
            input.CloudConsent = current != null && current.ScreenCloudConsent;
            input.PrivateMode = privateMode;
            input.AllowCloud = OutboundPrivacy.AllowsCloudVision(privateMode);
            input.EffectiveProviderId = OutboundPrivacy.ChatProviderId(privateMode, selected);
            input.CloudModel = current != null ? current.Model : ChatDefaults.Model;
            input.ChatModel = current != null ? current.LocalModel : ChatDefaults.LocalModel;
            input.LocalVisionModel = current != null ? current.LocalVisionModel : string.Empty;
            input.MonitorRequested = current != null && current.ScreenCaptureWholeMonitor;
            input.WholeMonitorConsent = current != null && current.ScreenWholeMonitorConsent;
            input.Monitor = _monitor;
            input.AutoLookConsent = current != null && current.ScreenAutoLook && !current.ScreenAutoLookPaused;
            input.Busy = false;
            input.Automatic = false;
            input.Target = _selected;
        }

        bool Enabled()
        {
            var settings = GetComponent<SettingsPanel>();
            return settings != null && settings.CurrentSettings != null && settings.CurrentSettings.ScreenLookEnabled;
        }

        async void RunLocalVision(int generation, byte[] jpeg)
        {
            var cts = new CancellationTokenSource();
            _visionCancel = cts;
            var panel = GetComponent<SettingsPanel>();
            var current = panel != null ? panel.CurrentSettings : null;
            var vision = current != null ? current.LocalVisionModel : string.Empty;
            Touch("Vision engine: " + vision + " on this PC. The chat model is not reading the picture.");
            try
            {
                var timeout = current != null ? current.TimeoutSeconds : ChatDefaults.TimeoutSeconds;
                if (timeout < 30f)
                    timeout = 30f;
                if (timeout > 180f)
                    timeout = 180f;
                var fast = current != null && current.ScreenFastReply;
                var result = await LocalVisionExchange.Run(
                    new UnityChatTransport(this),
                    current != null ? current.LocalBaseUrl : ChatDefaults.LocalBaseUrl,
                    current != null ? current.LocalModel : ChatDefaults.LocalModel,
                    vision,
                    jpeg,
                    timeout,
                    cts.Token,
                    fast ? "2m" : "0");
                if (!_alive)
                    return;
                var observationChars = result.Observation == null ? 0 : result.Observation.Length;
                Debug.Log(CaptureDiagnostics.VisionLine(result.StatusCode, result.Ok, result.Cancelled, result.Cancelled ? 0 : observationChars));
                if (!_gate.IsCurrent(generation) || cts.IsCancellationRequested || result.Cancelled)
                {
                    _gate.Leave(generation);
                    var cancelled = string.IsNullOrEmpty(result.Message) ? "Look cancelled. Nothing was sent." : result.Message;
                    Note(cancelled);
                    Touch(cancelled);
                    Log(false, 0, 0, cancelled);
                    return;
                }
                if (!result.Ok || !VisionPrompt.HasObservation(result.Observation))
                {
                    _gate.Leave(generation);
                    var failed = result.Ok ? "The local vision model returned no description. Nothing was sent to the cloud." : result.Message;
                    Note(failed);
                    Touch(failed);
                    Log(false, 0, 0, failed);
                    return;
                }

                if (fast)
                {
                    var automaticFast = _automaticLook;
                    _automaticLook = false;
                    await FinishFastLook(generation, cts.Token, current, result.Observation, automaticFast, timeout);
                    return;
                }

                var chat = GetComponent<ChatPanel>();
                var automatic = _automaticLook;
                _automaticLook = false;
                if (automatic)
                {
                    // She looked on her own. The reply is optional: a silent filter decides
                    // whether the observation is worth saying. The gate can be released here
                    // because the vision model has already finished.
                    _gate.Leave(generation);
                    var autoStarted = chat != null && chat.SubmitAutoObservation(result.Observation);
                    Debug.Log(CaptureDiagnostics.ChatLine("local", false, "observationChars", observationChars, autoStarted));
                    Touch(autoStarted ? "She glanced at the screen." : "She looked, and had nothing to say.");
                    return;
                }

                _waitingForChat = chat != null && chat.SubmitTextObservation(result.Observation);
                Debug.Log(CaptureDiagnostics.ChatLine("local", false, "observationChars", observationChars, _waitingForChat));
                if (!_waitingForChat)
                {
                    _gate.Leave(generation);
                    Touch("The local text model did not start. Nothing was sent to the cloud.");
                    return;
                }

                Touch("Chat engine is replying from the local text model. The picture stayed on this PC.");
            }
            catch (Exception ex)
            {
                if (_gate.IsCurrent(generation))
                    _gate.Leave(generation);
                var message = CaptureDiagnostics.Stopped(ex.GetType().Name, 0);
                Note(message);
                Touch(message);
                Log(false, 0, 0, message);
            }
            finally
            {
                if (jpeg != null && jpeg.Length > 0)
                    Array.Clear(jpeg, 0, jpeg.Length);
                if (_visionCancel == cts)
                    _visionCancel = null;
                cts.Dispose();
            }
        }

        /// <summary>
        /// Fast screen reply: the vision model that already read the picture writes the remark,
        /// so the chat model is not reloaded. The observation is kept in session context by
        /// ChatPanel. For an automatic look a one-word YES/NO check decides whether to speak.
        /// </summary>
        async System.Threading.Tasks.Task FinishFastLook(
            int generation, CancellationToken token, CompanionSettings settings,
            string observation, bool automatic, float timeout)
        {
            var transport = new UnityChatTransport(this);
            var baseUrl = settings != null ? settings.LocalBaseUrl : ChatDefaults.LocalBaseUrl;
            var vision = settings != null ? settings.LocalVisionModel : string.Empty;

            if (automatic)
            {
                var worth = await LocalVisionExchange.SilentCheck(transport, baseUrl, vision, observation, timeout, token);
                if (!_alive || token.IsCancellationRequested)
                    return;
                _gate.Leave(generation);
                if (!worth)
                {
                    Touch("She looked and had nothing to say.");
                    return;
                }
            }

            var name = settings != null ? settings.CompanionName : string.Empty;
            var system = OllamaVision.FastReplySystem(name);
            var reply = await LocalVisionExchange.FastReply(transport, baseUrl, vision, system, observation, timeout, token);
            _gate.Leave(generation);
            if (!_alive || token.IsCancellationRequested)
                return;

            if (!automatic)
                _gate.Leave(generation);
            if (!reply.Ok || string.IsNullOrWhiteSpace(reply.Observation))
            {
                Touch("The fast screen reply did not answer. Nothing was spoken.");
                return;
            }

            var chat = GetComponent<ChatPanel>();
            var started = chat != null && chat.SubmitFastObservation(observation, reply.Observation);
            Debug.Log(CaptureDiagnostics.ChatLine("local", false, "observationChars",
                observation != null ? observation.Length : 0, started));
            Touch(started ? "She answered from the local vision model." : "The fast reply did not start.");
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
