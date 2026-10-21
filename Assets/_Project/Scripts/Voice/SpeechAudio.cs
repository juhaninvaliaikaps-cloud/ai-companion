using System;
using System.Text;
using AICompanion.AI;

namespace AICompanion.Voice
{
    /// <summary>
    /// Turns one TTS body into float samples.
    /// Accepts a RIFF WAV, raw little-endian PCM16 mono, or a JSON envelope
    /// whose <c>audio</c> field is base64. Never throws.
    /// </summary>
    public static class SpeechAudio
    {
        public static bool TryDecode(byte[] data, string contentType, int sampleRate, bool rawPcmAllowed, out float[] samples, out int channels, out int decodedRate)
        {
            return TryDecode(data, contentType, sampleRate, rawPcmAllowed, out samples, out channels, out decodedRate, out _);
        }

        public static bool TryDecode(byte[] data, string contentType, int sampleRate, bool rawPcmAllowed, out float[] samples, out int channels, out int decodedRate, out string reason)
        {
            samples = null;
            channels = 0;
            decodedRate = 0;
            reason = "empty body";
            try
            {
                return TryDecodeCore(data, contentType, sampleRate, rawPcmAllowed, 0, out samples, out channels, out decodedRate, out reason);
            }
            catch (Exception ex)
            {
                samples = null;
                channels = 0;
                decodedRate = 0;
                reason = "decoder failed: " + ex.GetType().Name;
                return false;
            }
        }

        /// <summary>
        /// First four bytes as hex and printable ASCII. Empty input returns "empty".
        /// </summary>
        public static string DescribeHead(byte[] data)
        {
            if (data == null || data.Length == 0)
                return "empty";

            var count = data.Length < 4 ? data.Length : 4;
            var hex = new StringBuilder(count * 3);
            var ascii = new StringBuilder(count);
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                    hex.Append(' ');
                hex.Append(data[i].ToString("X2"));
                var b = data[i];
                ascii.Append(b >= 32 && b < 127 ? (char)b : '.');
            }

            hex.Append(' ');
            hex.Append(ascii);
            return hex.ToString();
        }

        static bool TryDecodeCore(byte[] data, string contentType, int sampleRate, bool rawPcmAllowed, int depth, out float[] samples, out int channels, out int decodedRate, out string reason)
        {
            samples = null;
            channels = 0;
            decodedRate = 0;
            reason = "empty body";
            if (data == null || data.Length == 0)
                return false;

            // audio/pcm and audio/L16 are headerless PCM16 even when the first bytes look like whitespace or text.
            if (Says(contentType, "pcm") || Says(contentType, "l16") || Says(contentType, "audio/raw"))
                return TryPcm16Mono(data, sampleRate, out samples, out channels, out decodedRate, out reason);

            if (Says(contentType, "json") || StartsWithJsonObject(data))
            {
                if (depth > 0)
                {
                    reason = "nested JSON envelope";
                    return false;
                }

                if (!TryUnwrapJson(data, out var inner, out var innerType, out reason))
                    return false;
                var nextType = string.IsNullOrEmpty(innerType) ? contentType : innerType;
                return TryDecodeCore(inner, nextType, sampleRate, rawPcmAllowed, depth + 1, out samples, out channels, out decodedRate, out reason);
            }

            if (IsRiff(data))
            {
                if (WavPcm.TryDecode(data, sampleRate, out samples, out channels, out decodedRate))
                {
                    reason = string.Empty;
                    return true;
                }

                reason = "RIFF WAV could not be read";
                return false;
            }

            if (Says(contentType, "mpeg") || Says(contentType, "mp3") || StartsWithId3(data))
            {
                reason = "MP3 is not played";
                return false;
            }

            if (Says(contentType, "wav") || Says(contentType, "wave"))
            {
                reason = "content type says wav but the body is not a RIFF WAV";
                return false;
            }

            if (Says(contentType, "mulaw") || Says(contentType, "alaw"))
            {
                reason = "mu-law and a-law are not played";
                return false;
            }

            if (!AllowsRawPcm(contentType, rawPcmAllowed))
            {
                reason = "unrecognized content type " + TypeLabel(contentType);
                return false;
            }

            return TryPcm16Mono(data, sampleRate, out samples, out channels, out decodedRate, out reason);
        }

        static bool TryUnwrapJson(byte[] data, out byte[] audio, out string contentType, out string reason)
        {
            audio = null;
            contentType = null;
            reason = "JSON envelope has no audio field";
            if (!TryJsonText(data, out var json))
                return false;
            if (!JsonText.TryReadStringField(json, "audio", out var payload) || string.IsNullOrWhiteSpace(payload))
                return false;

            JsonText.TryReadStringField(json, "content_type", out contentType);
            try
            {
                audio = Convert.FromBase64String(payload.Trim());
            }
            catch (FormatException)
            {
                audio = null;
                reason = "JSON audio is not valid base64";
                return false;
            }

            if (audio == null || audio.Length == 0)
            {
                reason = "JSON audio was empty";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        static bool TryPcm16Mono(byte[] data, int sampleRate, out float[] samples, out int channels, out int decodedRate, out string reason)
        {
            samples = null;
            channels = 0;
            decodedRate = 0;
            reason = "empty body";
            if (data == null || data.Length < 2)
                return false;
            if ((data.Length & 1) != 0)
            {
                reason = "PCM16 length is odd (" + data.Length.ToString() + " bytes)";
                return false;
            }

            var rate = TtsRequestJson.NormalizeSampleRate(sampleRate);
            if (rate < 1000)
            {
                reason = "sample rate is not supported";
                return false;
            }

            var count = data.Length / 2;
            samples = new float[count];
            for (var i = 0; i < count; i++)
            {
                var lo = data[i * 2];
                var hi = data[i * 2 + 1];
                var value = (short)(lo | (hi << 8));
                samples[i] = value / 32768f;
            }

            channels = 1;
            decodedRate = rate;
            reason = string.Empty;
            return true;
        }

        static string TypeLabel(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return "missing";
            var type = contentType.Trim().Replace('\r', ' ').Replace('\n', ' ');
            if (type.Length > 80)
                type = type.Substring(0, 80);
            return type;
        }

        static bool AllowsRawPcm(string contentType, bool rawPcmAllowed)
        {
            if (Says(contentType, "pcm") || Says(contentType, "l16") || Says(contentType, "audio/raw"))
                return true;
            if (!rawPcmAllowed)
                return false;
            return string.IsNullOrWhiteSpace(contentType) || Says(contentType, "octet-stream");
        }

        static bool IsRiff(byte[] data)
        {
            return data != null
                && data.Length >= 12
                && data[0] == (byte)'R'
                && data[1] == (byte)'I'
                && data[2] == (byte)'F'
                && data[3] == (byte)'F'
                && data[8] == (byte)'W'
                && data[9] == (byte)'A'
                && data[10] == (byte)'V'
                && data[11] == (byte)'E';
        }

        static bool StartsWithId3(byte[] data)
        {
            return data != null
                && data.Length >= 3
                && data[0] == (byte)'I'
                && data[1] == (byte)'D'
                && data[2] == (byte)'3';
        }

        static bool StartsWithJsonObject(byte[] data)
        {
            var i = BomSkip(data);
            i = SkipWs(data, i);
            if (i >= data.Length || data[i] != (byte)'{')
                return false;
            i = SkipWs(data, i + 1);
            return i < data.Length && data[i] == (byte)'"';
        }

        static bool TryJsonText(byte[] data, out string json)
        {
            json = null;
            if (data == null || data.Length == 0)
                return false;
            var start = BomSkip(data);
            if (start >= data.Length)
                return false;
            json = Encoding.UTF8.GetString(data, start, data.Length - start);
            return true;
        }

        static int BomSkip(byte[] data)
        {
            if (data != null && data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return 3;
            return 0;
        }

        static int SkipWs(byte[] data, int index)
        {
            while (index < data.Length)
            {
                var b = data[index];
                if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\r' && b != (byte)'\n')
                    break;
                index++;
            }

            return index;
        }

        static bool Says(string contentType, string token)
        {
            if (string.IsNullOrEmpty(contentType) || string.IsNullOrEmpty(token))
                return false;
            return contentType.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
