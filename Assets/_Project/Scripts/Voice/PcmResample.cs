using System;

namespace AICompanion.Voice
{
    /// <summary>
    /// Linear resample of interleaved microphone audio to 16 kHz mono,
    /// which is what whisper.cpp expects. No audio is written to disk.
    /// </summary>
    public static class PcmResample
    {
        public const int WhisperRate = 16000;

        public static float[] ToMono16k(float[] interleaved, int channels, int sourceRate)
        {
            if (interleaved == null || channels < 1 || sourceRate < 1 || interleaved.Length < channels)
                return Array.Empty<float>();

            var frames = interleaved.Length / channels;
            if (frames < 1)
                return Array.Empty<float>();

            var mono = new float[frames];
            for (var i = 0; i < frames; i++)
            {
                var sum = 0f;
                var start = i * channels;
                for (var c = 0; c < channels; c++)
                    sum += interleaved[start + c];
                mono[i] = sum / channels;
            }

            if (sourceRate == WhisperRate)
                return mono;

            var outFrames = (int)Math.Round(frames * (double)WhisperRate / sourceRate);
            if (outFrames < 1)
                outFrames = 1;

            var output = new float[outFrames];
            var ratio = (double)sourceRate / WhisperRate;
            var last = frames - 1;
            for (var i = 0; i < outFrames; i++)
            {
                var position = i * ratio;
                var index = (int)position;
                if (index >= last)
                {
                    output[i] = mono[last];
                    continue;
                }

                var blend = (float)(position - index);
                output[i] = mono[index] + (mono[index + 1] - mono[index]) * blend;
            }

            return output;
        }
    }
}
