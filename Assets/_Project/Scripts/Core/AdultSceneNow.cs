using System.Collections.Generic;
using System.Text;

namespace AICompanion.Core
{
    /// <summary>
    /// JUH-188: keeps the adult scene that is already happening so a new touch reads as a
    /// continuation instead of a fresh, disconnected event. Example: blowjob active, then
    /// touch butt -> "User grabs your butt, while you are giving him a blowjob".
    /// Main thread only. Plain C# so the wording is testable.
    /// </summary>
    public static class AdultSceneNow
    {
        const string Breast = "breasts";
        const string Butt = "butt";

        static string _scene = string.Empty;
        static readonly List<string> _touches = new List<string>();

        public static bool Active
        {
            get { return _scene.Length > 0; }
        }

        public static void Reset()
        {
            _scene = string.Empty;
            _touches.Clear();
        }

        /// <summary>
        /// Updates the scene/touch state and returns the interaction line to send. When there
        /// is no active scene the original line is returned unchanged.
        /// </summary>
        public static string Compose(string cue, string fallback)
        {
            var id = Normalize(cue);

            if (id == AvatarInteractionCatalog.BlowjobKneeling)
                return BeginScene("you are on your knees, giving him a blowjob", fallback);
            if (id == AvatarInteractionCatalog.BlowjobLying)
                return BeginScene("you are lying down, giving him a blowjob", fallback);
            if (id == AvatarInteractionCatalog.BlowjobOnTop)
                return BeginScene("you are on top of him, giving him a blowjob", fallback);
            if (id == AvatarInteractionCatalog.LapdanceFacing)
                return BeginScene("you are on top of him, riding him facing him", fallback);
            if (id == AvatarInteractionCatalog.LapdanceBehind)
                return BeginScene("he is fucking you from behind", fallback);

            if (id == AvatarInteractionCatalog.TouchBreast || id == AvatarInteractionCatalog.TouchBreastLoop)
            {
                Add(Breast);
                return Build(fallback);
            }

            if (id == AvatarInteractionCatalog.TouchButt || id == AvatarInteractionCatalog.TouchButtLoop)
            {
                Add(Butt);
                return Build(fallback);
            }

            // A different adult action ends the old continuation; it must not leak into it.
            if (id == AvatarInteractionCatalog.Kiss
                || id == AvatarInteractionCatalog.MakeOut
                || id == AvatarInteractionCatalog.LyingKiss
                || id == AvatarInteractionCatalog.LyingMakeOut
                || id == AvatarInteractionCatalog.LyingMakeOut2
                || id == AvatarInteractionCatalog.FingerMouth
                || id == AvatarInteractionCatalog.FingerSuckLoop
                || id == AvatarInteractionCatalog.TouchVagina)
            {
                Reset();
            }

            return fallback;
        }

        static string BeginScene(string scene, string fallback)
        {
            _scene = scene;
            _touches.Clear();
            return fallback;
        }

        static void Add(string touch)
        {
            if (!_touches.Contains(touch))
                _touches.Add(touch);
        }

        static string Build(string fallback)
        {
            if (_scene.Length == 0 || _touches.Count == 0)
                return fallback;

            var sb = new StringBuilder("*User grabs your ");
            var hasBreast = _touches.Contains(Breast);
            var hasButt = _touches.Contains(Butt);
            if (hasBreast && hasButt)
                sb.Append("breasts, and butt while ");
            else
            {
                sb.Append(hasBreast ? Breast : Butt);
                sb.Append(", while ");
            }
            sb.Append(_scene).Append(".*");
            return sb.ToString();
        }

        static string Normalize(string cue)
        {
            return (cue ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_');
        }
    }
}
