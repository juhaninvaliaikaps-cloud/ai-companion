using System;

namespace AICompanion.Voice
{
    /// <summary>
    /// Picks a microphone without opening one. An empty preference uses the
    /// system default. A missing name falls back to that default.
    /// </summary>
    public readonly struct MicChoice
    {
        public bool Available { get; }
        public bool FellBack { get; }
        public string Device { get; }

        public MicChoice(bool available, bool fellBack, string device)
        {
            Available = available;
            FellBack = fellBack;
            Device = device;
        }

        public static MicChoice Resolve(string preferred, string[] devices)
        {
            if (devices == null || devices.Length == 0)
                return new MicChoice(false, false, null);

            if (string.IsNullOrWhiteSpace(preferred))
                return new MicChoice(true, false, null);

            var wanted = preferred.Trim();
            for (var i = 0; i < devices.Length; i++)
            {
                if (string.Equals(devices[i], wanted, StringComparison.OrdinalIgnoreCase))
                    return new MicChoice(true, false, devices[i]);
            }

            return new MicChoice(true, true, null);
        }
    }

    /// <summary>
    /// Chooses a capture rate inside the device caps. 0 and 0 means any rate.
    /// </summary>
    public static class MicRate
    {
        public static int Choose(int min, int max, int preferred = PcmResample.WhisperRate)
        {
            if (preferred < 8000)
                preferred = 8000;
            if (min <= 0 && max <= 0)
                return preferred;
            if (min > 0 && preferred < min)
                preferred = min;
            if (max > 0 && preferred > max)
                preferred = max;
            return preferred;
        }
    }
}
