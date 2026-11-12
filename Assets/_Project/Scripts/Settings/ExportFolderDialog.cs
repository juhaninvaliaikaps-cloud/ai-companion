using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AICompanion.Settings
{
    /// <summary>
    /// Folder picker for the decrypted export. Non-Windows builds return false
    /// and the settings field is typed instead.
    /// </summary>
    public static class ExportFolderDialog
    {
        public static bool TryPick(out string path)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return WindowsFolderBrowser.TryPick(out path);
#else
            path = null;
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        static class WindowsFolderBrowser
        {
            const uint BifReturnOnlyFsDirs = 0x0001;

            public static bool TryPick(out string path)
            {
                path = null;
                var display = Marshal.AllocHGlobal(520);
                try
                {
                    var info = new BrowseInfo
                    {
                        hwndOwner = IntPtr.Zero,
                        pidlRoot = IntPtr.Zero,
                        pszDisplayName = display,
                        lpszTitle = "Choose a folder for the decrypted export",
                        ulFlags = BifReturnOnlyFsDirs,
                        lpfn = IntPtr.Zero,
                        lParam = IntPtr.Zero,
                        iImage = 0
                    };
                    var pidl = SHBrowseForFolder(ref info);
                    if (pidl == IntPtr.Zero)
                        return false;

                    try
                    {
                        var chosen = new StringBuilder(520);
                        if (!SHGetPathFromIDList(pidl, chosen))
                            return false;
                        path = chosen.ToString();
                        return !string.IsNullOrWhiteSpace(path);
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pidl);
                    }
                }
                catch (Exception)
                {
                    path = null;
                    return false;
                }
                finally
                {
                    Marshal.FreeHGlobal(display);
                }
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            struct BrowseInfo
            {
                public IntPtr hwndOwner;
                public IntPtr pidlRoot;
                public IntPtr pszDisplayName;
                public string lpszTitle;
                public uint ulFlags;
                public IntPtr lpfn;
                public IntPtr lParam;
                public int iImage;
            }

            [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
            static extern IntPtr SHBrowseForFolder(ref BrowseInfo lpbi);

            [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
            static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder pszPath);
        }
#endif
    }
}
