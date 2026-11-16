using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>Body parts a 2D touch menu can belong to.</summary>
    public enum BodyPart
    {
        None = 0,
        Lips = 1,
        Head = 2,
        Hands = 3,
        Cheeks = 4,
        Face = 5,
        Shoulder = 6,
        Chest = 7,
        Crotch = 8,
        Butt = 9,
        Thighs = 10,
    }

    /// <summary>
    /// JUH-101: maps a right-click on the 2D avatar to a body part and the point the local
    /// wheel should point at. Implemented by the video avatar; null while it is not playing.
    /// </summary>
    public interface IBodyHitZones
    {
        bool TryHit(Vector2 unityScreen, out BodyPart part, out Vector2 anchor);
    }
}
