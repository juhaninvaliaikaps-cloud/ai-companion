using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AICompanion.GameCompanion
{
    /// <summary>
    /// Steam's folder from <c>HKCU\Software\Valve\Steam\SteamPath</c>, read with advapi32 because
    /// the player's .NET Standard profile has no Microsoft.Win32.Registry (see
    /// RegistryStartupRegistration). Read only; returns null off Windows or when Steam isn't installed.
    /// </summary>
    public static class WindowsSteamPath
    {
        const string SubKey = @"Software\Valve\Steam";
        const string ValueName = "SteamPath";

        public static string Read()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                const uint RrfRtRegSz = 0x00000002;
                uint type;
                uint bytes = 2048;
                var data = new StringBuilder(1024);
                var status = RegGetValueW(HkeyCurrentUser, SubKey, ValueName, RrfRtRegSz, out type, data, ref bytes);
                if (status != 0)
                    return null;
                var path = data.ToString().Trim();
                return path.Length == 0 ? null : path.Replace('/', '\\');
            }
            catch (Exception)
            {
                return null;
            }
#else
            return null;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        static readonly IntPtr HkeyCurrentUser = new IntPtr(unchecked((int)0x80000001));

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int RegGetValueW(IntPtr hkey, string subKey, string value, uint flags, out uint type, StringBuilder data, ref uint dataBytes);
#endif
    }
}
