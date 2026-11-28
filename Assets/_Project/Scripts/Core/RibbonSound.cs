using System;

namespace AICompanion.Core
{
    public enum RibbonRewardSound { None, Affection, Discovery }

    public static class RibbonSound
    {
        public const float DefaultVolume = 0.35f;

        public static float ClampVolume(double volume)
        {
            if (double.IsNaN(volume) || double.IsInfinity(volume))
                return DefaultVolume;
            return (float)Math.Max(0d, Math.Min(1d, volume));
        }

        public static RibbonRewardSound Include(RibbonRewardSound current, FeedbackKind kind)
        {
            if (kind == FeedbackKind.Secret)
                return RibbonRewardSound.Discovery;
            if (current == RibbonRewardSound.None && (kind == FeedbackKind.Up || kind == FeedbackKind.CritUp))
                return RibbonRewardSound.Affection;
            return current;
        }
    }
}
