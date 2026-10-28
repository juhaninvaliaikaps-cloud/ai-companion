namespace AICompanion.Avatar
{
    /// <summary>
    /// Degrees added on top of the clip. Neck and head split the look so the turn
    /// reads as a head, and the eyes only add the extra past the head.
    /// </summary>
    public struct ProceduralPose
    {
        public float SpinePitch;
        public float SpineYaw;
        public float SpineRoll;
        public float ChestPitch;
        public float NeckYaw;
        public float NeckPitch;
        public float HeadYaw;
        public float HeadPitch;
        public float HeadRoll;
        public float EyeYaw;
        public float EyePitch;

        public static ProceduralPose Compose(
            IdlePose idle,
            float breathDegrees,
            LookAngles head,
            LookAngles eyes,
            float bodyWeight,
            float lookWeight)
        {
            bodyWeight = Clamp01(bodyWeight);
            lookWeight = Clamp01(lookWeight);
            return new ProceduralPose
            {
                SpinePitch = (idle.SpinePitch + breathDegrees * 0.45f) * bodyWeight,
                SpineYaw = idle.SpineYaw * bodyWeight,
                SpineRoll = idle.SpineRoll * bodyWeight,
                ChestPitch = (idle.ChestPitch + breathDegrees) * bodyWeight,
                NeckYaw = head.Yaw * 0.35f * lookWeight,
                NeckPitch = head.Pitch * 0.35f * lookWeight,
                HeadYaw = idle.HeadYaw * bodyWeight + head.Yaw * 0.65f * lookWeight,
                HeadPitch = idle.HeadPitch * bodyWeight + head.Pitch * 0.65f * lookWeight,
                HeadRoll = idle.HeadRoll * bodyWeight,
                EyeYaw = (eyes.Yaw - head.Yaw) * lookWeight,
                EyePitch = (eyes.Pitch - head.Pitch) * lookWeight
            };
        }

        public static ProceduralPose FoldMissingNeck(ProceduralPose pose, bool hasNeck)
        {
            if (hasNeck)
                return pose;

            pose.HeadYaw += pose.NeckYaw;
            pose.HeadPitch += pose.NeckPitch;
            pose.NeckYaw = 0f;
            pose.NeckPitch = 0f;
            return pose;
        }

        static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }
    }
}
