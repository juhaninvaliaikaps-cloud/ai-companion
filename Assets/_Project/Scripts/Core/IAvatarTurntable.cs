namespace AICompanion.Core
{
    /// <summary>
    /// Extra yaw on top of the camera-facing default. The overlay calls this
    /// while rotate mode is on. The offset is stored per avatar source.
    /// </summary>
    public interface IAvatarTurntable
    {
        void AddYawPixels(float deltaPixels);

        void CommitYaw();

        void ResetYaw();
    }
}
