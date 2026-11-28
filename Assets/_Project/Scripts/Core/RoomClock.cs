using System;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>The light in her room at one moment.</summary>
    public struct RoomLight
    {
        /// <summary>Multiplied over the room and the figures.</summary>
        public Color Tint;

        /// <summary>The colour of the sky in the window.</summary>
        public Color Sky;

        /// <summary>0 off to 1 on: the lamp's glow layer.</summary>
        public float Lamp;

        /// <summary>Bedtime: she goes to bed when nothing else is going on.</summary>
        public bool Night;
    }

    /// <summary>
    /// JUH-226: the room follows the computer's clock (Jussi). The tint blends smoothly between
    /// night, morning, day and dusk, the sky in the window follows, and the lamp is on in the
    /// evening and at night. The art is drawn in neutral daylight; this is all the lighting.
    /// </summary>
    public static class RoomClock
    {
        struct Key
        {
            public float Hour;
            public Color Tint;
            public Color Sky;

            public Key(float hour, Color tint, Color sky)
            {
                Hour = hour;
                Tint = tint;
                Sky = sky;
            }
        }

        static readonly Color NightTint = new Color(0.46f, 0.50f, 0.74f);
        static readonly Color MorningTint = new Color(1.00f, 0.93f, 0.86f);
        static readonly Color DayTint = Color.white;
        static readonly Color DuskTint = new Color(1.00f, 0.80f, 0.68f);

        static readonly Color NightSky = new Color(0.07f, 0.09f, 0.21f);
        static readonly Color MorningSky = new Color(0.98f, 0.80f, 0.66f);
        static readonly Color DaySky = new Color(0.56f, 0.78f, 0.98f);
        static readonly Color DuskSky = new Color(0.93f, 0.53f, 0.40f);

        static readonly Key[] Keys =
        {
            new Key(0f, NightTint, NightSky),
            new Key(5.5f, NightTint, NightSky),
            new Key(7f, MorningTint, MorningSky),
            new Key(9f, DayTint, DaySky),
            new Key(17f, DayTint, DaySky),
            new Key(19f, DuskTint, DuskSky),
            new Key(21f, NightTint, NightSky),
            new Key(24f, NightTint, NightSky),
        };

        /// <summary>The lamp is on from this hour…</summary>
        public const float LampOnHour = 18.5f;

        /// <summary>…until this hour.</summary>
        public const float LampOffHour = 7f;

        /// <summary>Bedtime starts…</summary>
        public const float NightStartHour = 22.5f;

        /// <summary>…and ends.</summary>
        public const float NightEndHour = 6.5f;

        public static RoomLight At(DateTime localTime)
        {
            return AtHour((float)localTime.TimeOfDay.TotalHours);
        }

        public static RoomLight AtHour(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            var light = new RoomLight();
            for (var i = 0; i < Keys.Length - 1; i++)
            {
                var a = Keys[i];
                var b = Keys[i + 1];
                if (hour < a.Hour || hour > b.Hour)
                    continue;
                var t = b.Hour > a.Hour ? (hour - a.Hour) / (b.Hour - a.Hour) : 0f;
                t = t * t * (3f - 2f * t);
                light.Tint = Color.Lerp(a.Tint, b.Tint, t);
                light.Sky = Color.Lerp(a.Sky, b.Sky, t);
                break;
            }

            light.Lamp = LampLevel(hour);
            light.Night = hour >= NightStartHour || hour < NightEndHour;
            return light;
        }

        /// <summary>The lamp fades in over half an hour at the start, and out over half an hour at the end.</summary>
        static float LampLevel(float hour)
        {
            const float fade = 0.5f;
            if (hour >= LampOnHour)
                return Mathf.Clamp01((hour - LampOnHour) / fade);
            if (hour < LampOffHour)
                return Mathf.Clamp01((LampOffHour - hour) / fade);
            return 0f;
        }
    }
}
