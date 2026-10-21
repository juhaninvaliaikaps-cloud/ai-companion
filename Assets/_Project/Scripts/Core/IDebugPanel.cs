using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// Extra IMGUI the overlay must keep clickable. The rect is GUI space,
    /// origin at the top left, the same space as <c>GUI.Box</c>.
    /// </summary>
    public interface IDebugPanel
    {
        Rect InteractiveRect { get; }
    }
}
