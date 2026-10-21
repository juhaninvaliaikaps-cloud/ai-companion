using System.Globalization;
using System.Text;
using AICompanion.AI;

namespace AICompanion.Voice
{
    public static class TtsRequestJson
    {
        public static string Write(TtsRequest request)
        {
            var text = request != null ? request.Text : string.Empty;
            var voice = request != null ? request.VoiceId : null;
            var language = request != null ? request.Language : null;
            var speed = ClampSpeed(request != null ? request.Speed : 1f);
            var rate = NormalizeSampleRate(request != null ? request.SampleRate : 24000);
            if (string.IsNullOrWhiteSpace(voice))
                voice = "eve";
            if (string.IsNullOrWhiteSpace(language))
                language = "en";

            var sb = new StringBuilder(text != null ? text.Length + 96 : 96);
            sb.Append("{\"text\":");
            JsonText.AppendString(sb, text ?? string.Empty);
            sb.Append(",\"voice_id\":");
            JsonText.AppendString(sb, voice.Trim());
            sb.Append(",\"language\":");
            JsonText.AppendString(sb, language.Trim());
            sb.Append(",\"speed\":");
            sb.Append(speed.ToString("0.0##", CultureInfo.InvariantCulture));
            sb.Append(",\"output_format\":{\"codec\":\"pcm\",\"sample_rate\":");
            sb.Append(rate.ToString(CultureInfo.InvariantCulture));
            sb.Append("}}");
            return sb.ToString();
        }

        public static float ClampSpeed(float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f)
                return 1f;
            if (speed < 0.7f)
                return 0.7f;
            if (speed > 1.5f)
                return 1.5f;
            return speed;
        }

        public static int NormalizeSampleRate(int rate)
        {
            switch (rate)
            {
                case 8000:
                case 16000:
                case 22050:
                case 24000:
                case 44100:
                case 48000:
                    return rate;
                default:
                    return 24000;
            }
        }
    }
}
