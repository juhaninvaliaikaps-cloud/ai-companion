using System;
using System.Threading;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.AI
{
    /// <summary>
    /// Temporary chat box until the real panel exists. F9 sends the text field.
    /// The API key box writes the per-user file and clears itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChatDebugHarness : MonoBehaviour, ICompanionStartup, IDebugPanel
    {
        const float PanelWidth = 464f;
        const float PanelHeight = 220f;

        struct PendingDelta
        {
            public int Generation;
            public string Text;
        }

        [SerializeField] ChatSettings settings;

        readonly System.Collections.Generic.Queue<PendingDelta> _deltas = new System.Collections.Generic.Queue<PendingDelta>();
        ChatProviderRegistry _registry;
        IChatTransport _transport;
        CancellationTokenSource _cancel;
        string _prompt = "Say hello in one short sentence.";
        string _reply = string.Empty;
        string _status = string.Empty;
        string _keyField = string.Empty;
        int _generation;
        bool _started;
        bool _busy;
        static GUIStyle _wrap;

        public Rect InteractiveRect
        {
            get
            {
                var height = Screen.height > 0 ? Screen.height : PanelHeight + 128f;
                return new Rect(8f, Mathf.Max(120f, height - PanelHeight - 8f), PanelWidth, PanelHeight);
            }
        }

        public void StartCompanion()
        {
            if (_started)
                return;

            _started = true;
            if (settings == null)
                settings = ScriptableObject.CreateInstance<ChatSettings>();
            _transport = new UnityChatTransport(this);
            _registry = ChatProviderFactory.Create(settings, new EnvironmentApiKeySource(), _transport);
        }

        void Start()
        {
            StartCompanion();
        }

        void OnDestroy()
        {
            if (_cancel != null)
            {
                _cancel.Cancel();
                _cancel.Dispose();
                _cancel = null;
            }
        }

        void Update()
        {
            if (!_started)
                return;

            DrainDeltas();
            if (Input.GetKeyDown(KeyCode.F9))
                Send();
            if (_busy && Input.GetKeyDown(KeyCode.Escape) && _cancel != null)
                _cancel.Cancel();
        }

        void OnGUI()
        {
            if (!_started)
                return;

            var panel = InteractiveRect;
            var line = WrapStyle();
            GUI.Box(panel, GUIContent.none);
            var x = panel.x + 8f;
            var y = panel.y + 8f;
            var width = panel.width - 16f;
            GUI.Label(new Rect(x, y, width, 18f), _busy ? "Chat  sending    Esc cancel" : "Chat  F9 send", line);
            y += 22f;
            _prompt = GUI.TextField(new Rect(x, y, width, 22f), _prompt ?? string.Empty);
            y += 26f;
            GUI.Label(new Rect(x, y, width, 48f), string.IsNullOrEmpty(_status) ? " " : _status, line);
            y += 50f;
            var reply = string.IsNullOrEmpty(_reply) ? "Reply appears here." : _reply;
            GUI.Label(new Rect(x, y, width, 52f), reply, line);
            y += 56f;
            _keyField = GUI.PasswordField(new Rect(x, y, width - 88f, 22f), _keyField ?? string.Empty, '*');
            if (GUI.Button(new Rect(x + width - 80f, y, 80f, 22f), "Save key"))
                SaveKey();
        }

        void Send()
        {
            if (_busy)
            {
                _status = "Still waiting for a reply.";
                return;
            }

            var prompt = (_prompt ?? string.Empty).Trim();
            if (prompt.Length == 0)
            {
                _status = "Type a message first.";
                return;
            }

            if (_registry == null || settings == null)
                StartCompanion();

            if (_registry == null || !_registry.TryGet(settings.providerId, out var provider))
            {
                _status = ChatErrors.UnknownProvider(settings.providerId);
                Debug.Log("Chat error: " + _status);
                return;
            }

            _generation++;
            lock (_deltas)
                _deltas.Clear();
            _reply = string.Empty;
            _status = "Sending...";
            _busy = true;
            if (_cancel != null)
                _cancel.Dispose();
            _cancel = new CancellationTokenSource();
            var generation = _generation;
            var request = new ChatRequest
            {
                SystemPrompt = settings.systemPrompt,
                Temperature = settings.temperature,
                MaxTokens = settings.maxTokens
            };
            request.Messages.Add(new ChatMessage(ChatRole.User, prompt));
            SendAsync(provider, request, generation, _cancel.Token);
        }

        async void SendAsync(IChatProvider provider, ChatRequest request, int generation, CancellationToken token)
        {
            try
            {
                var result = await provider.StreamChat(request, piece => QueueDelta(generation, piece), token);
                if (generation != _generation)
                    return;
                if (result.Ok)
                {
                    _status = result.Text.Length == 0 ? "Empty reply." : string.Empty;
                    Debug.Log("Chat reply: " + result.Text);
                }
                else
                {
                    _status = result.UserMessage;
                    if (!string.IsNullOrEmpty(result.LogMessage))
                        Debug.LogWarning(result.LogMessage);
                    else
                        Debug.Log("Chat error: " + result.UserMessage);
                }
            }
            catch (Exception)
            {
                if (generation != _generation)
                    return;
                _status = ChatErrors.Unreachable("Grok");
                Debug.Log("Chat error: " + _status);
            }
            finally
            {
                if (generation == _generation)
                    _busy = false;
            }
        }

        void QueueDelta(int generation, string piece)
        {
            if (string.IsNullOrEmpty(piece))
                return;
            lock (_deltas)
                _deltas.Enqueue(new PendingDelta { Generation = generation, Text = piece });
        }

        void DrainDeltas()
        {
            lock (_deltas)
            {
                while (_deltas.Count > 0)
                {
                    var pending = _deltas.Dequeue();
                    if (pending.Generation != _generation || string.IsNullOrEmpty(pending.Text))
                        continue;
                    _reply += pending.Text;
                    Debug.Log("Chat delta: " + pending.Text);
                }
            }
        }

        void SaveKey()
        {
            var key = (_keyField ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                _status = "Enter an API key first.";
                return;
            }

            try
            {
                ApiKeyFile.SavePreferred(key);
                _keyField = string.Empty;
                _status = "API key saved on this PC.";
            }
            catch (Exception)
            {
                _status = "Could not save the API key on this PC.";
            }
        }

        static GUIStyle WrapStyle()
        {
            if (_wrap == null)
            {
                _wrap = new GUIStyle(GUI.skin.label);
                _wrap.wordWrap = true;
            }

            return _wrap;
        }
    }
}
