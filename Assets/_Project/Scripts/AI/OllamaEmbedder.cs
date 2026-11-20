using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AICompanion.AI
{
    /// <summary>
    /// JUH-141: meaning vectors from the local Ollama (<c>/api/embed</c>), for the smarter memory
    /// search. Local only: it refuses any base URL that is not Ollama on this PC (loopback, port
    /// 11434), so private and 18+ memories never leave the machine through it.
    ///
    /// After a failure (Ollama not running, the model not pulled) it waits
    /// <see cref="RetryMinutes"/> before trying again, so chat never waits on a dead endpoint.
    /// </summary>
    public sealed class OllamaEmbedder
    {
        public const string DefaultModel = "nomic-embed-text";
        public const float TimeoutSeconds = 10f;
        public const int MaxBatch = 32;
        public const int RetryMinutes = 5;
        public const string PullHint = "ollama pull " + DefaultModel;

        readonly IChatTransport _transport;
        readonly Func<string> _baseUrl;
        readonly string _model;
        DateTime _blockedUntilUtc = DateTime.MinValue;

        public OllamaEmbedder(IChatTransport transport, Func<string> baseUrl, string model = DefaultModel)
        {
            _transport = transport;
            _baseUrl = baseUrl ?? (() => ChatDefaults.LocalBaseUrl);
            _model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
        }

        public string Model
        {
            get { return _model; }
        }

        /// <summary>Why the last request failed, for the log. Empty after a success.</summary>
        public string LastProblem { get; private set; } = string.Empty;

        /// <summary>False when there is no transport, the URL is not the local Ollama, or a failure is too recent.</summary>
        public bool Ready(DateTime nowUtc)
        {
            return _transport != null && IsLocalOllama(_baseUrl()) && nowUtc >= _blockedUntilUtc;
        }

        /// <summary>Ollama on this PC: a loopback host on Ollama's port.</summary>
        public static bool IsLocalOllama(string baseUrl)
        {
            Uri uri;
            if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out uri))
                return false;
            var loopback = string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal)
                || string.Equals(uri.Host, "[::1]", StringComparison.Ordinal)
                || string.Equals(uri.Host, "::1", StringComparison.Ordinal);
            return loopback && uri.Port == 11434;
        }

        public static string EmbedUrl(string baseUrl)
        {
            var value = string.IsNullOrWhiteSpace(baseUrl) ? ChatDefaults.LocalBaseUrl : baseUrl.Trim();
            value = value.TrimEnd('/');
            if (value.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - 3).TrimEnd('/');
            return value + "/api/embed";
        }

        public static string BuildBody(string model, IReadOnlyList<string> inputs)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"model\":");
            JsonText.AppendString(sb, model);
            sb.Append(",\"input\":[");
            for (var i = 0; i < inputs.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                JsonText.AppendString(sb, inputs[i] ?? string.Empty);
            }

            sb.Append("],\"truncate\":true}");
            return sb.ToString();
        }

        /// <summary>
        /// One vector per input, in order, or null when anything failed (then nothing changes:
        /// the search falls back to words). At most <see cref="MaxBatch"/> inputs per call.
        /// </summary>
        public async Task<float[][]> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
        {
            if (inputs == null || inputs.Count == 0 || inputs.Count > MaxBatch || !Ready(DateTime.UtcNow))
                return null;

            var http = new ChatHttpRequest
            {
                Url = EmbedUrl(_baseUrl()),
                Body = BuildBody(_model, inputs),
                TimeoutSeconds = TimeoutSeconds,
                FollowRedirects = false,
            };
            http.Headers.Add(new KeyValuePair<string, string>("Content-Type", "application/json"));

            var body = new StringBuilder(inputs.Count * 9000);
            ChatHttpResult result;
            try
            {
                result = await _transport.PostAsync(http, chunk => body.Append(chunk), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception e)
            {
                Fail("request failed (" + e.GetType().Name + ")");
                return null;
            }

            if (result.Cancelled)
                return null;
            if (result.TimedOut || result.NetworkFailed)
            {
                Fail("Ollama did not answer at " + http.Url);
                return null;
            }

            if (result.StatusCode == 404)
            {
                Fail("the model " + _model + " is not installed; run: " + PullHint);
                return null;
            }

            if (result.StatusCode < 200 || result.StatusCode >= 300)
            {
                Fail("HTTP " + result.StatusCode);
                return null;
            }

            var vectors = Parse(body.Length > 0 ? body.ToString() : result.Body);
            if (vectors == null || vectors.Length != inputs.Count)
            {
                Fail("the reply could not be read");
                return null;
            }

            LastProblem = string.Empty;
            return vectors;
        }

        void Fail(string problem)
        {
            LastProblem = problem;
            _blockedUntilUtc = DateTime.UtcNow.AddMinutes(RetryMinutes);
        }

        /// <summary>Reads <c>{"embeddings":[[0.1,-0.2,…],[…]]}</c>. Null when it is not that.</summary>
        public static float[][] Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;
            var key = json.IndexOf("\"embeddings\"", StringComparison.Ordinal);
            if (key < 0)
                return null;
            var i = json.IndexOf('[', key);
            if (i < 0)
                return null;

            var vectors = new List<float[]>();
            var current = new List<float>(1024);
            var inVector = false;
            for (i = i + 1; i < json.Length; i++)
            {
                var c = json[i];
                if (c == '[')
                {
                    if (inVector)
                        return null;
                    inVector = true;
                    current.Clear();
                    continue;
                }

                if (c == ']')
                {
                    if (!inVector)
                        break;
                    if (current.Count == 0)
                        return null;
                    vectors.Add(current.ToArray());
                    inVector = false;
                    continue;
                }

                if (!inVector || c == ',' || char.IsWhiteSpace(c))
                    continue;

                var start = i;
                while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '-' || json[i] == '+' || json[i] == '.' || json[i] == 'e' || json[i] == 'E'))
                    i++;
                float value;
                if (i == start || !float.TryParse(json.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    return null;
                current.Add(value);
                i--;
            }

            if (inVector || vectors.Count == 0)
                return null;
            var length = vectors[0].Length;
            for (var v = 1; v < vectors.Count; v++)
            {
                if (vectors[v].Length != length)
                    return null;
            }

            return vectors.ToArray();
        }
    }
}
