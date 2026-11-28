using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-229: the room's lighting layers, worked out from the clock (option 1: picture layers
    /// over the art, no engine lights). RoomClock gives the ambient tint, the sky colour and the
    /// lamp; this adds the window's sunbeam on the floor, the night shade with its pool of lamp
    /// light, and the lamp's glow. All positions are room fractions, y down.
    /// </summary>
    public static class RoomLighting
    {
        /// <summary>The clock's day sky; the sky picture already has these colours, so its tint is relative to them.</summary>
        static readonly Color DaySky = new Color(0.56f, 0.78f, 0.98f);

        /// <summary>The glass of the window (from the art catalogue) and the floor's back edge under it.</summary>
        public const float WindowLeft = 399f / 1024f;
        public const float WindowRight = 624f / 1024f;
        public const float FloorTop = 242f / 1024f;

        /// <summary>How far down the floor the beam reaches, and how much it fans out at the far end.</summary>
        public const float BeamLength = 0.42f;
        public const float BeamSpread = 0.06f;

        /// <summary>The most the beam brightens the floor (alpha of its near end at midday).</summary>
        public const float BeamAlpha = 0.2f;

        /// <summary>The middle of the lamp's pool of light on the floor, and its soft edge.</summary>
        public static readonly Vector2 LampPool = new Vector2(352f / 1024f, 300f / 1024f);
        public const float PoolInner = 0.08f;
        public const float PoolOuter = 0.36f;

        /// <summary>The lamp shade, where the glow picture is centred.</summary>
        public static readonly Vector2 LampShade = new Vector2(352f / 1024f, 215f / 1024f);

        public const float MaxShade = 0.35f;
        public const float MaxGlow = 0.55f;

        /// <summary>The tint for the sky picture: the clock's sky relative to the day sky, clamped to 1.</summary>
        public static Color SkyTint(Color sky)
        {
            return new Color(Mathf.Clamp01(sky.r / DaySky.r), Mathf.Clamp01(sky.g / DaySky.g), Mathf.Clamp01(sky.b / DaySky.b), 1f);
        }

        /// <summary>How dark the room is from its tint: 0 in daylight, about 0.4 at night.</summary>
        public static float Darkness(Color tint)
        {
            var brightness = 0.299f * tint.r + 0.587f * tint.g + 0.114f * tint.b;
            return Mathf.Clamp01(1f - brightness);
        }

        /// <summary>The night shade's alpha: darker rooms get a deeper shade, up to <see cref="MaxShade"/>.</summary>
        public static float Shade(Color tint)
        {
            return Mathf.Clamp01(Darkness(tint) / 0.45f) * MaxShade;
        }

        /// <summary>The lamp glow's alpha.</summary>
        public static float Glow(float lamp)
        {
            return Mathf.Clamp01(lamp) * MaxGlow;
        }

        /// <summary>The sunbeam's strength: up after sunrise, full from 9 to 16, gone by 18:30.</summary>
        public static float Sun(float hour)
        {
            if (hour <= 7f || hour >= 18.5f)
                return 0f;
            if (hour < 9f)
                return Smooth((hour - 7f) / 2f);
            if (hour <= 16f)
                return 1f;
            return Smooth((18.5f - hour) / 2.5f);
        }

        /// <summary>The beam's lean: the far end slides from right in the morning to left in the evening (fraction of its length).</summary>
        public static float Lean(float hour)
        {
            var t = Mathf.Clamp01((hour - 7f) / 11.5f);
            return Mathf.Lerp(0.6f, -0.6f, t);
        }

        /// <summary>The beam's four corners on the floor: near left, near right, far right, far left.</summary>
        public static void Beam(float hour, out Vector2 nearLeft, out Vector2 nearRight, out Vector2 farRight, out Vector2 farLeft)
        {
            var shift = Lean(hour) * BeamLength;
            var farY = FloorTop + BeamLength;
            nearLeft = new Vector2(WindowLeft, FloorTop);
            nearRight = new Vector2(WindowRight, FloorTop);
            farRight = new Vector2(WindowRight + shift + BeamSpread, farY);
            farLeft = new Vector2(WindowLeft + shift - BeamSpread, farY);
        }

        /// <summary>
        /// The night shade's cover at a point (0 = the lamp's light shows through, 1 = full shade).
        /// Baked once into the shade's texture.
        /// </summary>
        public static float PoolCover(Vector2 point)
        {
            var distance = Vector2.Distance(point, LampPool);
            return Smooth((distance - PoolInner) / (PoolOuter - PoolInner));
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>JUH-229: which character frame to show (the art pack's 8 fps cycles).</summary>
    public static class RoomFrames
    {
        public const float Fps = 8f;

        /// <summary>The idle blink: held on frame 0, then this sequence once every <see cref="BlinkEvery"/> seconds.</summary>
        public static readonly int[] IdleOrder = { 0, 1, 3, 2, 3, 1, 0 };
        public const float BlinkEvery = 4f;

        /// <summary>Standing still facing away or sideways: the walk frame with the legs together.</summary>
        public const int StandFrame = 1;

        /// <summary>Activity poses have two frames; they breathe slowly rather than at the walk's 8 fps.</summary>
        public const float PoseFps = 2f;

        public static int Pose(float time)
        {
            var frame = (int)Mathf.Floor(time * PoseFps) % 2;
            return frame < 0 ? frame + 2 : frame;
        }

        public static int Walk(float time)
        {
            var frame = (int)Mathf.Floor(time * Fps) % 4;
            return frame < 0 ? frame + 4 : frame;
        }

        public static int Idle(float time)
        {
            var within = time % BlinkEvery;
            if (within < 0f)
                within += BlinkEvery;
            var step = (int)Mathf.Floor(within * Fps);
            return step < IdleOrder.Length ? IdleOrder[step] : 0;
        }
    }

    /// <summary>JUH-229: the drawing order of the furniture and the two characters.</summary>
    public static class RoomDepth
    {
        /// <summary>
        /// JUH-237: the depth of a character in a pose on a piece of furniture: in front of the
        /// piece she uses (on the bed, on the pouf), and never behind her own pose point.
        /// </summary>
        public static float Pose(float poseY, float pieceDepth)
        {
            return Mathf.Max(poseY, pieceDepth + 0.001f);
        }

        /// <summary>
        /// Fills <paramref name="order"/> back to front. Furniture are 0..n-1 (their depths sorted
        /// ascending), her is n and the player n+1. A character goes in front of every piece whose
        /// depth is at or above its feet; between the two characters, the lower one is in front.
        /// </summary>
        public static void Order(float[] furnitureDepths, float herDepth, float playerDepth, int[] order)
        {
            var n = furnitureDepths.Length;
            var herIndex = n;
            var playerIndex = n + 1;
            var first = herDepth <= playerDepth ? herIndex : playerIndex;
            var second = first == herIndex ? playerIndex : herIndex;
            var firstDepth = Mathf.Min(herDepth, playerDepth);
            var secondDepth = Mathf.Max(herDepth, playerDepth);

            var k = 0;
            var placedFirst = false;
            var placedSecond = false;
            for (var i = 0; i < n; i++)
            {
                if (!placedFirst && furnitureDepths[i] > firstDepth)
                {
                    order[k++] = first;
                    placedFirst = true;
                }

                if (placedFirst && !placedSecond && furnitureDepths[i] > secondDepth)
                {
                    order[k++] = second;
                    placedSecond = true;
                }

                order[k++] = i;
            }

            if (!placedFirst)
                order[k++] = first;
            if (!placedSecond)
                order[k] = second;
        }
    }
}
