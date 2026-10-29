namespace AICompanion.Core
{
    /// <summary>
    /// Asks the desktop presence to walk for a while, or to stand still. The model and the
    /// animation director use it instead of a background wander timer, so movement follows
    /// the conversation rather than interrupting it. The presence keeps authority over
    /// physics, drops and the work area.
    /// </summary>
    public interface IWalkRequest
    {
        /// <summary>+1 walks right, -1 walks left. Seconds is clamped by the presence.</summary>
        void RequestWalk(float direction, float seconds);

        /// <summary>Ends a walk immediately and stands still.</summary>
        void RequestStop();
    }
}
