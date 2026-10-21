using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// Yaw that turns a humanoid toward the overlay camera, plus the extra
    /// yaw from a horizontal drag. The overlay camera sits on −Z and looks
    /// along +Z, so a model whose chest faces +Z is backwards until this runs.
    /// </summary>
    public static class AvatarFacing
    {
        public const float DegreesPerPixel = 0.5f;

        /// <summary>
        /// Anatomical forward from the hips. Right-minus-left crossed with
        /// spine-minus-hips, flattened onto the ground. False when the bones
        /// do not describe a direction.
        /// </summary>
        public static bool TryBodyForward(Vector3 hips, Vector3 spine, Vector3 leftUpperLeg, Vector3 rightUpperLeg, out Vector3 forward)
        {
            var across = rightUpperLeg - leftUpperLeg;
            var up = spine - hips;
            var body = Vector3.Cross(across, up);
            body.y = 0f;
            if (body.sqrMagnitude < 1e-8f)
            {
                forward = Vector3.zero;
                return false;
            }

            forward = body.normalized;
            return true;
        }

        /// <summary>
        /// Signed degrees around world up that swing <paramref name="bodyForward"/>
        /// onto <paramref name="fromModelToCamera"/>. Both are flattened onto XZ.
        /// A missing direction returns 0 so the root is left as authored.
        /// </summary>
        public static float YawDegreesToward(Vector3 bodyForward, Vector3 fromModelToCamera)
        {
            bodyForward.y = 0f;
            fromModelToCamera.y = 0f;
            if (bodyForward.sqrMagnitude < 1e-8f || fromModelToCamera.sqrMagnitude < 1e-8f)
                return 0f;

            return Vector3.SignedAngle(bodyForward, fromModelToCamera, Vector3.up);
        }

        public static float DragDelta(float deltaPixels)
        {
            return deltaPixels * DegreesPerPixel;
        }

        /// <summary>
        /// Adds a horizontal drag. Positive pixels (cursor moves right) increase yaw,
        /// which is a positive rotation around world up. The result stays in (−180, 180].
        /// </summary>
        public static float ApplyDrag(float yawDegrees, float deltaPixels)
        {
            return WrapDegrees(yawDegrees + DragDelta(deltaPixels));
        }

        public static float WrapDegrees(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f)
                degrees -= 360f;
            if (degrees <= -180f)
                degrees += 360f;
            return degrees;
        }
    }
}
