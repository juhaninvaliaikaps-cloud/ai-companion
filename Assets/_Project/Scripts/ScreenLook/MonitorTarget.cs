using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AICompanion.ScreenLook
{
    /// <summary>
    /// One display as Windows reports it. The handle is only valid until the display
    /// configuration changes, so the list is rebuilt before every monitor capture.
    /// The type exists on every platform; only the Windows build can enumerate displays.
    /// </summary>
    public sealed class MonitorTarget
    {
        public IntPtr Handle;
        public string DeviceName = string.Empty;
        public bool IsPrimary;
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width
        {
            get { return Right - Left; }
        }

        public int Height
        {
            get { return Bottom - Top; }
        }

        public string Label
        {
            get
            {
                var name = string.IsNullOrEmpty(DeviceName) ? "Display" : DeviceName;
                return name + "  " + Width + "x" + Height + (IsPrimary ? "  (primary)" : string.Empty);
            }
        }
    }

    /// <summary>Enumerates the physical displays. No capture happens here.</summary>
    public static class MonitorList
    {
        public const int MaxMonitors = 8;

        public static IReadOnlyList<MonitorTarget> Read()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var found = new List<MonitorTarget>(2);
            try
            {
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hmon, hdc, rect, data) =>
                {
                    if (found.Count >= MaxMonitors)
                        return false;

                    var info = new MonitorInfoEx();
                    info.Size = Marshal.SizeOf(typeof(MonitorInfoEx));
                    if (!GetMonitorInfoW(hmon, ref info))
                        return true;

                    found.Add(new MonitorTarget
                    {
                        Handle = hmon,
                        DeviceName = Clean(info.DeviceName),
                        IsPrimary = (info.Flags & MonitorInfoPrimary) != 0,
                        Left = info.Monitor.Left,
                        Top = info.Monitor.Top,
                        Right = info.Monitor.Right,
                        Bottom = info.Monitor.Bottom,
                    });
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception)
            {
                found.Clear();
            }

            found.Sort((a, b) =>
            {
                if (a.IsPrimary != b.IsPrimary)
                    return a.IsPrimary ? -1 : 1;
                if (a.Left != b.Left)
                    return a.Left.CompareTo(b.Left);
                return a.Top.CompareTo(b.Top);
            });
            return found;
#else
            return new MonitorTarget[0];
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            var end = value.IndexOf('\0');
            return end >= 0 ? value.Substring(0, end) : value;
        }

        const int MonitorInfoPrimary = 0x1;

        delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MonitorInfoEx
        {
            public int Size;
            public Rect Monitor;
            public Rect Work;
            public int Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
        }

        [DllImport("user32.dll")]
        static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
        static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfoEx info);
#endif
    }
}
