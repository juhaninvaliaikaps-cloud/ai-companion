using System;
using System.Runtime.InteropServices;

namespace AICompanion.Settings
{
    /// <summary>
    /// JUH-153: the Windows "Save as" dialog (comdlg32), for exporting a persona. It asks before
    /// overwriting a file. Non-Windows builds return false.
    /// </summary>
    public static class SaveFileDialog
    {
        public const string CardPng = "Character card (*.png)\0*.png\0All files (*.*)\0*.*\0\0";
        public const string CardJson = "Character card JSON (*.json)\0*.json\0All files (*.*)\0*.*\0\0";
        public const string Zip = "Zip (*.zip)\0*.zip\0All files (*.*)\0*.*\0\0";

        public static string FilterFor(CardExportFormat format)
        {
            switch (format)
            {
                case CardExportFormat.Json:
                    return CardJson;
                case CardExportFormat.Zip:
                    return Zip;
                default:
                    return CardPng;
            }
        }

        public static string ExtensionFor(CardExportFormat format)
        {
            switch (format)
            {
                case CardExportFormat.Json:
                    return "json";
                case CardExportFormat.Zip:
                    return "zip";
                default:
                    return "png";
            }
        }

        public static bool TryPick(string title, string filter, string defaultExtension, string suggestedName, out string path)
        {
            path = null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                var buffer = Marshal.AllocHGlobal(2 * MaxPath);
                try
                {
                    var name = (suggestedName ?? string.Empty).Replace("\0", string.Empty);
                    if (name.Length >= MaxPath)
                        name = name.Substring(0, MaxPath - 1);
                    var chars = (name + "\0").ToCharArray();
                    Marshal.Copy(chars, 0, buffer, chars.Length);
                    var info = new SaveFileName
                    {
                        lStructSize = Marshal.SizeOf(typeof(SaveFileName)),
                        lpstrFilter = filter,
                        lpstrFile = buffer,
                        nMaxFile = MaxPath,
                        lpstrTitle = title,
                        lpstrDefExt = defaultExtension,
                        Flags = OfnOverwritePrompt | OfnPathMustExist | OfnNoChangeDir | OfnExplorer,
                    };
                    if (!GetSaveFileNameW(ref info))
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
        const int OfnOverwritePrompt = 0x00000002;
        const int OfnPathMustExist = 0x00000800;
        const int OfnNoChangeDir = 0x00000008;
        const int OfnExplorer = 0x00080000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SaveFileName
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
        static extern bool GetSaveFileNameW(ref SaveFileName info);
#endif
    }
}
