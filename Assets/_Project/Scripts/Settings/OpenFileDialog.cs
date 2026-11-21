using System;
using System.Runtime.InteropServices;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-150: the Windows "Open" dialog (comdlg32), for picking a persona picture or a text
    /// file to import. Non-Windows builds return false and the path is typed instead.
    /// </summary>
    public static class OpenFileDialog
    {
        public const string Images = "Pictures (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0All files (*.*)\0*.*\0\0";
        public const string Texts = "Text (*.txt;*.md)\0*.txt;*.md\0All files (*.*)\0*.*\0\0";

        /// <summary>JUH-153: SillyTavern / Chub character cards.</summary>
        public const string Cards = "Character cards (*.png;*.json)\0*.png;*.json\0All files (*.*)\0*.*\0\0";

        public static bool TryPick(string title, string filter, out string path)
        {
            path = null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                var buffer = Marshal.AllocHGlobal(2 * MaxPath);
                try
                {
                    Marshal.WriteInt16(buffer, 0);
                    var info = new OpenFileName
                    {
                        lStructSize = Marshal.SizeOf(typeof(OpenFileName)),
                        lpstrFilter = filter,
                        lpstrFile = buffer,
                        nMaxFile = MaxPath,
                        lpstrTitle = title,
                        Flags = OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnExplorer,
                    };
                    if (!GetOpenFileNameW(ref info))
                        return false;
                    path = Marshal.PtrToStringUni(buffer);
                    return !string.IsNullOrWhiteSpace(path);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception)
            {
                path = null;
                return false;
            }
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        const int MaxPath = 1024;
        const int OfnFileMustExist = 0x00001000;
        const int OfnPathMustExist = 0x00000800;
        const int OfnNoChangeDir = 0x00000008;
        const int OfnExplorer = 0x00080000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern bool GetOpenFileNameW(ref OpenFileName info);
#endif
    }
}
