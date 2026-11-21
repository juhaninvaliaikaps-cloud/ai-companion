using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace AICompanion.Voice
{
    /// <summary>JUH-162: the mouse button that talks to Happi while a game has the focus.</summary>
    public enum TalkMouseButton
    {
        Off = 0,
        Mouse4 = 4,
        Mouse5 = 5,
    }

    /// <summary>
    /// JUH-162: the plain rules behind talking while gaming. Windows lets any program ask whether a
    /// button is held, whichever window has the focus, and the game still gets the button too:
    /// Happi only reads it, the way Discord's push-to-talk does.
    /// </summary>
    public static class GameTalkButtons
    {
        public const ushort SonyVendor = 0x054C;
        public const ushort DualSense = 0x0CE6;
        public const ushort DualSenseEdge = 0x0DF2;

        /// <summary>The Xbox (Guide) button in <c>XInputGetStateEx</c>'s buttons.</summary>
        public const ushort XboxGuideBit = 0x0400;

        public const int VirtualKeyMouse4 = 0x05; // VK_XBUTTON1
        public const int VirtualKeyMouse5 = 0x06; // VK_XBUTTON2

        public static TalkMouseButton Normalize(int value)
        {
            return value == 4 ? TalkMouseButton.Mouse4 : value == 5 ? TalkMouseButton.Mouse5 : TalkMouseButton.Off;
        }

        /// <summary>The virtual key for <paramref name="button"/>, or 0 for off.</summary>
        public static int VirtualKey(TalkMouseButton button)
        {
            switch (button)
            {
                case TalkMouseButton.Mouse4: return VirtualKeyMouse4;
                case TalkMouseButton.Mouse5: return VirtualKeyMouse5;
                default: return 0;
            }
        }

        public static string Label(TalkMouseButton button)
        {
            switch (button)
            {
                case TalkMouseButton.Mouse4: return "Mouse 4 (back side button)";
                case TalkMouseButton.Mouse5: return "Mouse 5 (forward side button)";
                default: return "Off";
            }
        }

        public static bool XboxGuide(ushort buttons)
        {
            return (buttons & XboxGuideBit) != 0;
        }

        public static bool IsDualSense(ushort vendor, ushort product)
        {
            return vendor == SonyVendor && (product == DualSense || product == DualSenseEdge);
        }

        /// <summary>Bluetooth HID device paths carry the HID service id or the Bluetooth enumerator.</summary>
        public static bool IsBluetoothPath(string devicePath)
        {
            var path = (devicePath ?? string.Empty).ToLowerInvariant();
            return path.Contains("00001124-0000-1000-8000-00805f9b34fb") || path.Contains("bthenum") || path.Contains("bthledevice");
        }

        /// <summary>
        /// The PS button in one DualSense input report. Over USB, report 0x01 has the buttons at
        /// bytes 8 to 10 and PS is bit 0 of byte 10. Over Bluetooth, the full report 0x31 has one
        /// more byte first (PS in byte 11), and the short report 0x01 that the controller sends
        /// before a game switches it to full has PS in byte 7.
        /// </summary>
        public static bool DualSensePs(byte[] report, int length, bool bluetooth)
        {
            if (report == null || length < 1 || length > report.Length)
                return false;
            if (report[0] == 0x31)
                return length >= 12 && (report[11] & 0x01) != 0;
            if (report[0] != 0x01)
                return false;
            if (!bluetooth)
                return length >= 11 && (report[10] & 0x01) != 0;
            return length >= 8 && (report[7] & 0x01) != 0;
        }
    }

    /// <summary>
    /// JUH-162: reads the talk buttons while a game has the focus: a mouse side button, the Xbox
    /// button and the DualSense PS button. Poll <see cref="Held"/> once a frame; it does not
    /// allocate. The DualSense is read on a background thread. A game running as administrator
    /// hides its input from a program that isn't, so then Happi must run as administrator too.
    /// </summary>
    public sealed class GameTalkInput : IDisposable
    {
        TalkMouseButton _mouse = TalkMouseButton.Mouse4;
        bool _xbox = true;
        bool _ps = true;

        public TalkMouseButton Mouse => _mouse;
        public bool Xbox => _xbox;
        public bool Ps => _ps;

        /// <summary>A short line for the settings: which controllers were found.</summary>
        public string Status
        {
            get
            {
                var parts = string.Empty;
                if (_ps)
                    parts = _dualSenseFound ? "DualSense found" : "no DualSense found";
                if (_xbox)
                    parts += (parts.Length > 0 ? ", " : string.Empty) + (_xboxSlot >= 0 ? "Xbox controller found" : "no Xbox controller found");
                return parts;
            }
        }

        public void Configure(TalkMouseButton mouse, bool xbox, bool ps)
        {
            _mouse = mouse;
            _xbox = xbox;
            _ps = ps;
            if (_ps)
                StartDualSense();
            else
                StopDualSense();
        }

        /// <summary>True while any enabled talk button is held.</summary>
        public bool Held()
        {
            var key = GameTalkButtons.VirtualKey(_mouse);
            if (key != 0 && MouseHeld(key))
                return true;
            if (_xbox && XboxHeld())
                return true;
            return _ps && _psHeld;
        }

        public void Dispose()
        {
            StopDualSense();
        }

        // ---- Xbox --------------------------------------------------------------------------------

        int _xboxSlot = -1;
        float _nextXboxScan;
        bool _xboxBroken;

        bool XboxHeld()
        {
            if (_xboxBroken)
                return false;
            try
            {
                // A disconnected slot is slow to ask, so empty slots are checked every 2 seconds.
                if (_xboxSlot < 0)
                {
                    if (Time.unscaledTime < _nextXboxScan)
                        return false;
                    _nextXboxScan = Time.unscaledTime + 2f;
                    for (var slot = 0; slot < 4; slot++)
                    {
                        ushort found;
                        if (XInputButtons(slot, out found))
                        {
                            _xboxSlot = slot;
                            return GameTalkButtons.XboxGuide(found);
                        }
                    }

                    return false;
                }

                ushort buttons;
                if (XInputButtons(_xboxSlot, out buttons))
                    return GameTalkButtons.XboxGuide(buttons);
                _xboxSlot = -1;
                return false;
            }
            catch (Exception ex)
            {
                // No xinput1_4.dll (very old Windows) or the ordinal is missing.
                _xboxBroken = true;
                Debug.Log("Talk while gaming: the Xbox button can't be read here (" + ex.GetType().Name + ")");
                return false;
            }
        }

        // ---- DualSense ---------------------------------------------------------------------------

        Thread _dualSenseThread;
        volatile int _generation;
        volatile bool _psHeld;
        volatile bool _dualSenseFound;
        IntPtr _dualSenseHandle = IntPtr.Zero;
        readonly object _handleLock = new object();

        void StartDualSense()
        {
            if (_dualSenseThread != null || !Native)
                return;
            var generation = ++_generation;
            _dualSenseThread = new Thread(() => DualSenseLoop(generation)) { IsBackground = true, Name = "Happi DualSense" };
            _dualSenseThread.Start();
        }

        void StopDualSense()
        {
            // A thread from an older generation ends at its next check.
            _generation++;
            _psHeld = false;
            lock (_handleLock)
            {
                if (_dualSenseHandle != IntPtr.Zero)
                    CancelRead(_dualSenseHandle);
            }

            _dualSenseThread = null;
        }

        void DualSenseLoop(int generation)
        {
            var buffer = new byte[128];
            while (generation == _generation)
            {
                bool bluetooth;
                var handle = OpenDualSense(out bluetooth);
                if (handle == IntPtr.Zero)
                {
                    _dualSenseFound = false;
                    SleepWhileRunning(generation, 3000);
                    continue;
                }

                lock (_handleLock)
                    _dualSenseHandle = handle;
                _dualSenseFound = true;
                try
                {
                    while (generation == _generation)
                    {
                        int read;
                        if (!ReadReport(handle, buffer, out read))
                            break;
                        if (generation == _generation)
                            _psHeld = GameTalkButtons.DualSensePs(buffer, read, bluetooth);
                    }
                }
                finally
                {
                    lock (_handleLock)
                    {
                        if (_dualSenseHandle == handle)
                            _dualSenseHandle = IntPtr.Zero;
                    }

                    if (generation == _generation)
                    {
                        _psHeld = false;
                        _dualSenseFound = false;
                    }

                    CloseDevice(handle);
                }

                SleepWhileRunning(generation, 1000);
            }
        }

        void SleepWhileRunning(int generation, int milliseconds)
        {
            for (var waited = 0; waited < milliseconds && generation == _generation; waited += 100)
                Thread.Sleep(100);
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        static readonly bool Native = true;

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int virtualKey);

        static bool MouseHeld(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct XInputStateEx
        {
            public uint PacketNumber;
            public ushort Buttons;
            public byte LeftTrigger;
            public byte RightTrigger;
            public short ThumbLX;
            public short ThumbLY;
            public short ThumbRX;
            public short ThumbRY;
            public uint Reserved;
        }

        // XInputGetState hides the Guide button; the ordinal-100 export (XInputGetStateEx) reports it.
        [DllImport("xinput1_4.dll", EntryPoint = "#100")]
        static extern int XInputGetStateEx(int userIndex, out XInputStateEx state);

        static bool XInputButtons(int slot, out ushort buttons)
        {
            XInputStateEx state;
            var result = XInputGetStateEx(slot, out state);
            buttons = state.Buttons;
            return result == 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DeviceInterfaceData
        {
            public int Size;
            public Guid ClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HidAttributes
        {
            public int Size;
            public ushort VendorId;
            public ushort ProductId;
            public ushort Version;
        }

        [DllImport("hid.dll")]
        static extern void HidD_GetHidGuid(out Guid guid);

        [DllImport("hid.dll")]
        static extern bool HidD_GetAttributes(IntPtr device, ref HidAttributes attributes);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr parent, int flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr deviceInfo, ref Guid classGuid, int index, ref DeviceInterfaceData data);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref DeviceInterfaceData data, IntPtr detail, int detailSize, out int requiredSize, IntPtr deviceInfo);

        [DllImport("setupapi.dll")]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadFile(IntPtr file, byte[] buffer, int toRead, out int read, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CancelIoEx(IntPtr file, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        const int DigcfPresent = 0x02;
        const int DigcfDeviceInterface = 0x10;
        const uint GenericRead = 0x80000000;
        const uint FileShareRead = 0x01;
        const uint FileShareWrite = 0x02;
        const uint OpenExisting = 3;
        static readonly IntPtr InvalidHandle = new IntPtr(-1);

        /// <summary>The first DualSense, opened shared for reading so the game and Steam keep their input.</summary>
        static IntPtr OpenDualSense(out bool bluetooth)
        {
            bluetooth = false;
            Guid hid;
            HidD_GetHidGuid(out hid);
            var set = SetupDiGetClassDevs(ref hid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
            if (set == InvalidHandle || set == IntPtr.Zero)
                return IntPtr.Zero;
            try
            {
                for (var index = 0; index < 256; index++)
                {
                    var data = new DeviceInterfaceData { Size = Marshal.SizeOf(typeof(DeviceInterfaceData)) };
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hid, index, ref data))
                        break;
                    var path = DevicePath(set, ref data);
                    if (string.IsNullOrEmpty(path))
                        continue;
                    var handle = CreateFile(path, GenericRead, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
                    if (handle == InvalidHandle || handle == IntPtr.Zero)
                        continue;
                    var attributes = new HidAttributes { Size = Marshal.SizeOf(typeof(HidAttributes)) };
                    if (HidD_GetAttributes(handle, ref attributes) && GameTalkButtons.IsDualSense(attributes.VendorId, attributes.ProductId))
                    {
                        bluetooth = GameTalkButtons.IsBluetoothPath(path);
                        return handle;
                    }

                    CloseHandle(handle);
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(set);
            }

            return IntPtr.Zero;
        }

        static string DevicePath(IntPtr set, ref DeviceInterfaceData data)
        {
            int required;
            SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out required, IntPtr.Zero);
            if (required <= 0 || required > 4096)
                return null;
            var detail = Marshal.AllocHGlobal(required);
            try
            {
                // SP_DEVICE_INTERFACE_DETAIL_DATA_W: cbSize is 8 on 64-bit and 6 on 32-bit; the path follows the int.
                Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, required, out required, IntPtr.Zero))
                    return null;
                return Marshal.PtrToStringUni(new IntPtr(detail.ToInt64() + 4));
            }
            finally
            {
                Marshal.FreeHGlobal(detail);
            }
        }

        static bool ReadReport(IntPtr handle, byte[] buffer, out int read)
        {
            return ReadFile(handle, buffer, buffer.Length, out read, IntPtr.Zero) && read > 0;
        }

        static void CancelRead(IntPtr handle)
        {
            CancelIoEx(handle, IntPtr.Zero);
        }

        static void CloseDevice(IntPtr handle)
        {
            CloseHandle(handle);
        }
#else
        static readonly bool Native = false;

        static bool MouseHeld(int virtualKey)
        {
            return false;
        }

        static bool XInputButtons(int slot, out ushort buttons)
        {
            buttons = 0;
            return false;
        }

        static IntPtr OpenDualSense(out bool bluetooth)
        {
            bluetooth = false;
            return IntPtr.Zero;
        }

        static bool ReadReport(IntPtr handle, byte[] buffer, out int read)
        {
            read = 0;
            return false;
        }

        static void CancelRead(IntPtr handle)
        {
        }

        static void CloseDevice(IntPtr handle)
        {
        }
#endif
    }
}
