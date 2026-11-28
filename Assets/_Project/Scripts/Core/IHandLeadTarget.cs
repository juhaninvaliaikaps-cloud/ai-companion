namespace AICompanion.Core
{
    /// <summary>
    /// JUH-194: the small surface the window drag needs to turn a held hand into a pull
    /// or a sideways lead. The video avatar implements it; the overlay must not reference
    /// the Settings or UI assemblies.
    /// </summary>
    public interface IHandLeadTarget
    {
        /// <summary>True while the existing JUH-101 hand-hold action is active.</summary>
        bool IsHandHeld { get; }

        /// <summary>Held hand dragged toward the user.</summary>
        void BeginHandPull();

        /// <summary>The pull ended; play the settle clip.</summary>
        void EndHandPull();

        /// <summary>Held hand dragged sideways enough to lead her. True is rightward travel.</summary>
        void BeginHandLead(bool right);

        /// <summary>The drag reversed while she was already being led; flip the walk/turn direction.</summary>
        void ChangeHandLeadDirection(bool right);

        /// <summary>Scale the hand-walk playback speed from cursor speed. 1 is normal.</summary>
        void SetHandLeadSpeed(float speed);

        /// <summary>The sideways lead ended; turn back to the front hold.</summary>
        void EndHandLead();
    }
}
