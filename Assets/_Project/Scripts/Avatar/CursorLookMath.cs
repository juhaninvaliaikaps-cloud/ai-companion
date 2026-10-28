using System;

namespace AICompanion.Avatar
{
    public struct LookAngles
    {
        public float Yaw;
        public float Pitch;
    }

    /// <summary>
    /// Turns a cursor offset into a clamped head and eye pose. Positive yaw looks
    /// toward screen-left (her right when she faces the camera). Positive pitch looks up.
    /// A cursor past the far threshold, or one that has not moved, eases back to neutral.
    /// </summary>
    public static class CursorLookMath
    {
        public const float MaxHeadYaw = 20f;
        public const float MaxHeadPitch = 12f;
        public const float FarYaw = 46f;
        public const float FarPitch = 34f;
        public const float MaxEyeYaw = 28f;
        public const float MaxEyePitch = 16f;
        public const float YawPerPixel = 0.08f;
        public const float PitchPerPixel = 0.07f;
        public const float IdleSeconds = 2.5f;
        public const float HeadSmoothSeconds = 0.18f;
        public const float EyeSmoothSeconds = 0.09f;
        public const float MoveEpsilon = 2f;

        public static LookAngles FromPixels(float deltaX, float deltaY)
        {
            if (float.IsNaN(deltaX) || float.IsNaN(deltaY))
                return default;

            return new LookAngles
            {
                Yaw = -deltaX * YawPerPixel,
                Pitch = deltaY * PitchPerPixel
            };
        }

        public static LookAngles Desired(LookAngles raw)
        {
            if (float.IsNaN(raw.Yaw) || float.IsNaN(raw.Pitch))
                return default;
            if (Math.Abs(raw.Yaw) > FarYaw || Math.Abs(raw.Pitch) > FarPitch)
                return default;

            return new LookAngles
            {
                Yaw = Clamp(raw.Yaw, -MaxHeadYaw, MaxHeadYaw),
                Pitch = Clamp(raw.Pitch, -MaxHeadPitch, MaxHeadPitch)
            };
        }

        public static LookAngles Eyes(LookAngles head)
        {
            return new LookAngles
            {
                Yaw = Clamp(head.Yaw * 1.4f, -MaxEyeYaw, MaxEyeYaw),
                Pitch = Clamp(head.Pitch * 1.35f, -MaxEyePitch, MaxEyePitch)
            };
        }

        public static float Smooth(float current, float target, float deltaTime, float seconds)
        {
            if (float.IsNaN(current))
                current = 0f;
            if (float.IsNaN(target))
                target = 0f;
            if (seconds <= 0f || deltaTime >= seconds)
                return target;
            if (deltaTime <= 0f)
                return current;

            var step = deltaTime / seconds;
            return current + (target - current) * step;
        }

        public static LookAngles Smooth(LookAngles current, LookAngles target, float deltaTime, float seconds)
        {
            return new LookAngles
            {
                Yaw = Smooth(current.Yaw, target.Yaw, deltaTime, seconds),
                Pitch = Smooth(current.Pitch, target.Pitch, deltaTime, seconds)
            };
        }

        static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value) || value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }

    public struct CursorLookModel
    {
        public LookAngles Head;
        public LookAngles Eyes;
        public float StillSeconds;
        public float LastX;
        public float LastY;
        public bool HasSample;

        public CursorLookModel Advance(
            bool enabled,
            bool hasTarget,
            float deltaX,
            float deltaY,
            float cursorX,
            float cursorY,
            float deltaTime)
        {
            if (deltaTime < 0f)
                deltaTime = 0f;

            var desired = default(LookAngles);
            if (!enabled || !hasTarget)
            {
                if (hasTarget)
                {
                    LastX = cursorX;
                    LastY = cursorY;
                    HasSample = true;
                }

                StillSeconds = CursorLookMath.IdleSeconds;
            }
            else
            {
                var moved = !HasSample
                    || Math.Abs(cursorX - LastX) > CursorLookMath.MoveEpsilon
                    || Math.Abs(cursorY - LastY) > CursorLookMath.MoveEpsilon;
                LastX = cursorX;
                LastY = cursorY;
                HasSample = true;
                if (moved)
                    StillSeconds = 0f;
                else
                    StillSeconds += deltaTime;

                if (StillSeconds < CursorLookMath.IdleSeconds)
                    desired = CursorLookMath.Desired(CursorLookMath.FromPixels(deltaX, deltaY));
            }

            Head = CursorLookMath.Smooth(Head, desired, deltaTime, CursorLookMath.HeadSmoothSeconds);
            Eyes = CursorLookMath.Smooth(Eyes, CursorLookMath.Eyes(desired), deltaTime, CursorLookMath.EyeSmoothSeconds);
            return this;
        }
    }
}
