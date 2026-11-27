using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>The bundled default faces shared by the classic and current Happi views.</summary>
    public static class HappiTypeface
    {
        public const string Family = "Plus Jakarta Sans";
        public const float ChatSize = 18f;

        public static Font Load(string style = "Regular") =>
            Resources.Load<Font>("HappiFonts/PlusJakartaSans-" + style.Replace(" ", string.Empty));
    }
}
