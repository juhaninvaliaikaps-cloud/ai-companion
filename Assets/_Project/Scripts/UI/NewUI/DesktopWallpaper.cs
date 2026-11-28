using System;
using System.IO;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
using System.Text;
#endif

namespace AICompanion.NewUI
{
    /// <summary>
    /// JUH-228: the Windows desktop wallpaper as a texture, for the door into her room. Only the
    /// wallpaper file is read: never a capture of the screen, so no window or icon can end up in
    /// it (Jussi, JUH-175). Null when there is none (a plain colour) or it cannot be read.
    /// </summary>
    static class DesktopWallpaper
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const uint SpiGetDeskWallpaper = 0x0073;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool SystemParametersInfo(uint action, uint param, StringBuilder value, uint winIni);
#endif

        /// <summary>The wallpaper file: Windows' own JPEG copy first, then the file the user picked.</summary>
        static string FindFile()
        {
            try
            {
                var transcoded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
                if (File.Exists(transcoded))
                    return transcoded;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                var path = new StringBuilder(520);
                if (SystemParametersInfo(SpiGetDeskWallpaper, (uint)path.Capacity, path, 0))
                {
                    var picked = path.ToString();
                    if (!string.IsNullOrEmpty(picked) && File.Exists(picked))
                        return picked;
                }
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning("Her room: could not find the wallpaper (" + e.GetType().Name + ")");
            }

            return null;
        }

        /// <summary>Loads the wallpaper, or null. JPEG and PNG only; anything else gives the plain door.</summary>
        public static Texture2D Load()
        {
            var file = FindFile();
            if (file == null)
                return null;
            try
            {
                var bytes = File.ReadAllBytes(file);
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "Her room door wallpaper" };
                if (texture.LoadImage(bytes, true))
                {
                    texture.wrapMode = TextureWrapMode.Clamp;
                    return texture;
                }

                UnityEngine.Object.Destroy(texture);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Her room: could not read the wallpaper (" + e.GetType().Name + ")");
            }

            return null;
        }
    }
}
