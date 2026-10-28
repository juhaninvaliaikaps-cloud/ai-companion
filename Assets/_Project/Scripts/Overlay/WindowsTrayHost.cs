#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AICompanion.Core;

namespace AICompanion.Overlay
{
    /// <summary>
    /// Notification-area icon. The message loop lives on a background thread so it
    /// does not block the player. Menu commands are queued for the Unity thread.
    /// Compiled only into the Windows player.
    /// </summary>
    public sealed class WindowsTrayHost
    {
        const uint NimAdd = 0x00000000;
        const uint NimDelete = 0x00000002;
        const uint NimSetVersion = 0x00000004;
        const uint NifMessage = 0x00000001;
        const uint NifIcon = 0x00000002;
        const uint NifTip = 0x00000004;
        const uint NifShowTip = 0x00000080;
        const uint NotifyIconVersion4 = 4;
        const uint TrayCallback = 0x8000 + 1;
        const uint WmClose = 0x0010;
        const uint WmDestroy = 0x0002;
        const uint WmNull = 0x0000;
        const uint MfString = 0x0000;
        const uint MfChecked = 0x0008;
        const uint MfSeparator = 0x0800;
        const uint TpmReturnCmd = 0x0100;
        const uint TpmNonotify = 0x0080;
        const uint TpmRightAlign = 0x0008;
        const uint TpmBottomAlign = 0x0020;
        const uint WsPopup = 0x80000000;
        const uint WsExToolWindow = 0x00000080;
        const uint WsExNoActivate = 0x08000000;
        const int ErrorClassAlreadyExists = 1410;
        const int SmCyScreen = 1;
        const int DebounceMs = 300;

        const string ClassName = "AICompanion.Tray";

        static readonly WndProc ProcDelegate = WindowProc;
        static volatile WindowsTrayHost _live;
        static bool _processHooked;

        readonly TrayMenuState _menu;
        readonly ConcurrentQueue<TrayCommand> _commands;
        readonly TrayIconLifecycle _life = new TrayIconLifecycle();
        readonly object _state = new object();

        Thread _thread;
        IntPtr _hwnd;
        IntPtr _icon;
        bool _version4;
        bool _inMenu;
        int _lastPrimary;
        int _lastContext;
        bool _hasPrimary;
        bool _hasContext;
        int _stop;
        int _removeOnce;
        uint _taskbarCreated;
        volatile int _status;

        public WindowsTrayHost(TrayMenuState menu, ConcurrentQueue<TrayCommand> commands)
        {
            _menu = menu ?? new TrayMenuState();
            _commands = commands ?? new ConcurrentQueue<TrayCommand>();
        }

        public int Status => _status;

        public void Start()
        {
            if (_thread != null)
                return;

            lock (_state)
                _live = this;
            HookProcessExit();
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "AICompanion.Tray"
            };
            _thread.Start();
        }

        public void Remove()
        {
            if (Interlocked.Exchange(ref _removeOnce, 1) == 1)
                return;

            Interlocked.Exchange(ref _stop, 1);
            IntPtr hwnd;
            lock (_state)
                hwnd = _hwnd;
            if (hwnd != IntPtr.Zero)
                PostMessage(hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);

            var thread = _thread;
            if (thread != null && thread.IsAlive && Thread.CurrentThread != thread)
            {
                if (!thread.Join(1500))
                    DeleteIcon();
            }
            else
            {
                DeleteIcon();
            }

            lock (_state)
            {
                if (_live == this)
                    _live = null;
            }
        }

        void Run()
        {
            try
            {
                var instance = GetModuleHandle(null);
                var wc = new WndClass
                {
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(ProcDelegate),
                    hInstance = instance,
                    lpszClassName = ClassName
                };
                if (RegisterClassW(ref wc) == 0 && Marshal.GetLastWin32Error() != ErrorClassAlreadyExists)
                {
                    _status = 2;
                    return;
                }

                var hwnd = CreateWindowExW(WsExToolWindow | WsExNoActivate, ClassName, AppName.Display, WsPopup, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
                if (hwnd == IntPtr.Zero)
                {
                    _status = 2;
                    return;
                }

                lock (_state)
                    _hwnd = hwnd;

                _taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
                _icon = CreateIcon(TrayIconImage.CreateBgra(), TrayIconImage.Size);
                var added = AddIcon();
                if (!added)
                {
                    for (var i = 0; i < 5 && Volatile.Read(ref _stop) == 0; i++)
                    {
                        Thread.Sleep(200);
                        added = AddIcon();
                        if (added)
                            break;
                    }
                }

                if (Volatile.Read(ref _stop) != 0 || !added)
                {
                    DeleteIcon();
                    DestroyWindow(hwnd);
                    if (_status == 0)
                        _status = 2;
                    return;
                }

                _status = 1;
                while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            }
            catch (Exception)
            {
                if (_status == 0)
                    _status = 2;
            }
            finally
            {
                DeleteIcon();
                IntPtr icon;
                lock (_state)
                {
                    icon = _icon;
                    _icon = IntPtr.Zero;
                    _hwnd = IntPtr.Zero;
                }

                if (icon != IntPtr.Zero)
                    DestroyIcon(icon);
                if (_status != 2)
                    _status = 3;
            }
        }

        bool AddIcon()
        {
            IntPtr hwnd;
            IntPtr icon;
            lock (_state)
            {
                hwnd = _hwnd;
                icon = _icon;
            }

            if (hwnd == IntPtr.Zero)
                return false;

            var data = new NotifyIconData();
            data.cbSize = Marshal.SizeOf(typeof(NotifyIconData));
            data.hWnd = hwnd;
            data.uID = 1;
            data.uFlags = NifMessage | NifIcon | NifTip | NifShowTip;
            data.uCallbackMessage = TrayCallback;
            data.hIcon = icon;
            data.szTip = AppName.Display;
            if (!Shell_NotifyIcon(NimAdd, ref data))
                return false;

            data.uTimeoutOrVersion = NotifyIconVersion4;
            _version4 = Shell_NotifyIcon(NimSetVersion, ref data);
            _life.TryMarkAdded();
            return true;
        }

        void DeleteIcon()
        {
            // TryMarkRemoved counts once. NIM_DELETE still runs on later calls so a quit
            // and a process-exit hook both ask the shell to drop the icon.
            _life.TryMarkRemoved();
            IntPtr hwnd;
            lock (_state)
                hwnd = _hwnd;
            if (hwnd == IntPtr.Zero)
                return;

            var data = new NotifyIconData();
            data.cbSize = Marshal.SizeOf(typeof(NotifyIconData));
            data.hWnd = hwnd;
            data.uID = 1;
            Shell_NotifyIcon(NimDelete, ref data);
        }

        static IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            var self = _live;
            if (self == null)
                return DefWindowProcW(hwnd, msg, wParam, lParam);
            return self.Handle(hwnd, msg, wParam, lParam);
        }

        IntPtr Handle(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == TrayCallback)
            {
                var code = TrayClick.EventCode(unchecked((uint)lParam.ToInt64()), _version4);
                var kind = TrayClick.Classify(code);
                if (kind == TrayClickKind.Primary)
                {
                    var now = Environment.TickCount;
                    if (TrayClick.Allow(ref _lastPrimary, now, DebounceMs, ref _hasPrimary))
                        _commands.Enqueue(TrayCommand.ToggleVisible);
                }
                else if (kind == TrayClickKind.Context)
                {
                    var now = Environment.TickCount;
                    if (TrayClick.Allow(ref _lastContext, now, DebounceMs, ref _hasContext))
                        ShowMenu(hwnd);
                }

                return IntPtr.Zero;
            }

            if (_taskbarCreated != 0 && msg == _taskbarCreated)
            {
                _life.OnTaskbarRecreated();
                AddIcon();
                return IntPtr.Zero;
            }

            if (msg == WmClose)
            {
                DestroyWindow(hwnd);
                return IntPtr.Zero;
            }

            if (msg == WmDestroy)
            {
                DeleteIcon();
                PostQuitMessage(0);
                return IntPtr.Zero;
            }

            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }

        void ShowMenu(IntPtr hwnd)
        {
            if (_inMenu)
                return;

            _inMenu = true;
            var menu = IntPtr.Zero;
            try
            {
                bool visible;
                bool paused;
                bool startup;
                _menu.CopyFlags(out visible, out paused, out startup);
                var items = TrayMenuState.Build(visible, paused, startup);
                menu = CreatePopupMenu();
                if (menu == IntPtr.Zero)
                    return;

                for (var i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    if (item.SeparatorBefore)
                        AppendMenuW(menu, MfSeparator, UIntPtr.Zero, string.Empty);
                    var flags = MfString;
                    if (item.Checked)
                        flags |= MfChecked;
                    AppendMenuW(menu, flags, (UIntPtr)(uint)item.Command, item.Label);
                }

                if (!GetCursorPos(out var point))
                {
                    point.x = 0;
                    point.y = 0;
                }

                var align = TpmReturnCmd | TpmNonotify | TpmRightAlign;
                var screenHeight = GetSystemMetrics(SmCyScreen);
                if (point.y > screenHeight / 2)
                    align |= TpmBottomAlign;

                SetForegroundWindow(hwnd);
                var command = TrackPopupMenu(menu, align, point.x, point.y, 0, hwnd, IntPtr.Zero);
                PostMessage(hwnd, WmNull, IntPtr.Zero, IntPtr.Zero);
                if (command != 0)
                    _commands.Enqueue((TrayCommand)command);
            }
            finally
            {
                if (menu != IntPtr.Zero)
                    DestroyMenu(menu);
                _inMenu = false;
            }
        }

        static void HookProcessExit()
        {
            if (_processHooked)
                return;
            _processHooked = true;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
        }

        static void OnProcessExit(object sender, EventArgs e)
        {
            var live = _live;
            if (live != null)
                live.Remove();
        }

        static void OnUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            var live = _live;
            if (live != null)
                live.Remove();
        }

        static IntPtr CreateIcon(byte[] bgra, int size)
        {
            if (bgra == null || bgra.Length < size * size * 4)
                return IntPtr.Zero;

            var header = new BitmapInfo();
            header.bmiHeader.biSize = Marshal.SizeOf(typeof(BitmapInfoHeader));
            header.bmiHeader.biWidth = size;
            header.bmiHeader.biHeight = -size;
            header.bmiHeader.biPlanes = 1;
            header.bmiHeader.biBitCount = 32;
            IntPtr bits;
            var color = CreateDIBSection(IntPtr.Zero, ref header, 0, out bits, IntPtr.Zero, 0);
            if (color == IntPtr.Zero || bits == IntPtr.Zero)
            {
                if (color != IntPtr.Zero)
                    DeleteObject(color);
                return IntPtr.Zero;
            }

            Marshal.Copy(bgra, 0, bits, size * size * 4);
            var stride = ((size + 31) / 32) * 4;
            var maskBytes = new byte[stride * size];
            var maskBits = Marshal.AllocHGlobal(maskBytes.Length);
            var mask = IntPtr.Zero;
            try
            {
                Marshal.Copy(maskBytes, 0, maskBits, maskBytes.Length);
                mask = CreateBitmap(size, size, 1, 1, maskBits);
            }
            finally
            {
                Marshal.FreeHGlobal(maskBits);
            }
            var info = new IconInfo
            {
                fIcon = 1,
                hbmColor = color,
                hbmMask = mask
            };
            var icon = CreateIconIndirect(ref info);
            if (color != IntPtr.Zero)
                DeleteObject(color);
            if (mask != IntPtr.Zero)
                DeleteObject(mask);
            return icon;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NotifyIconData
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WndClass
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Msg
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int ptX;
            public int ptY;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WinPoint
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BitmapInfoHeader
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BitmapInfo
        {
            public BitmapInfoHeader bmiHeader;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IconInfo
        {
            public int fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
        static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData data);

        [DllImport("user32.dll", EntryPoint = "RegisterClassW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern ushort RegisterClassW(ref WndClass wc);

        [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateWindowExW(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
        static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetMessageW")]
        static extern int GetMessage(out Msg msg, IntPtr hWnd, uint filterMin, uint filterMax);

        [DllImport("user32.dll")]
        static extern bool TranslateMessage(ref Msg msg);

        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage(ref Msg msg);

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll")]
        static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode)]
        static extern uint RegisterWindowMessageW(string lpString);

        [DllImport("user32.dll")]
        static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)]
        static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out WinPoint point);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("gdi32.dll", EntryPoint = "CreateDIBSection")]
        static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfo pbmi, uint usage, out IntPtr bits, IntPtr section, uint offset);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bitCount, IntPtr bits);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr ho);

        [DllImport("user32.dll")]
        static extern IntPtr CreateIconIndirect(ref IconInfo iconInfo);
    }

    /// <summary>
    /// HKCU Run key via advapi32. The player API profile is .NET Standard, which
    /// does not include Microsoft.Win32.Registry.
    /// </summary>
    public sealed class RegistryStartupRegistration : IStartupRegistration
    {
        const int ErrorSuccess = 0;
        const int ErrorFileNotFound = 2;
        const int ErrorMoreData = 234;
        const uint RegSz = 1;
        const uint RegExpandSz = 2;
        const uint KeyQueryValue = 0x0001;
        const uint KeySetValue = 0x0002;
        const uint RegOptionNonVolatile = 0;
        const int MaxValueBytes = 65536;

        static readonly IntPtr HkeyCurrentUser = new IntPtr(unchecked((int)0x80000001));

        public bool TryGetEnabled(string valueName, out bool enabled)
        {
            enabled = false;
            if (string.IsNullOrEmpty(valueName))
                return false;

            var key = IntPtr.Zero;
            try
            {
                var opened = RegOpenKeyExW(HkeyCurrentUser, WindowsStartupEntry.RunSubKey, 0, KeyQueryValue, out key);
                if (opened == ErrorFileNotFound)
                    return true;
                if (opened != ErrorSuccess || key == IntPtr.Zero)
                    return false;
            }
            catch (Exception)
            {
                return false;
            }

            try
            {
                string command;
                if (!TryReadString(key, valueName, out command))
                    return false;
                enabled = !string.IsNullOrWhiteSpace(command);
                return true;
            }
            catch (Exception)
            {
                enabled = false;
                return false;
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        public bool TrySetEnabled(string valueName, bool enabled, string command)
        {
            if (string.IsNullOrEmpty(valueName))
                return false;
            if (enabled && string.IsNullOrWhiteSpace(command))
                return false;

            var key = IntPtr.Zero;
            try
            {
                uint disposition;
                var created = RegCreateKeyExW(
                    HkeyCurrentUser,
                    WindowsStartupEntry.RunSubKey,
                    0,
                    null,
                    RegOptionNonVolatile,
                    KeySetValue,
                    IntPtr.Zero,
                    out key,
                    out disposition);
                if (created != ErrorSuccess || key == IntPtr.Zero)
                    return false;
            }
            catch (Exception)
            {
                return false;
            }

            try
            {
                if (!enabled)
                {
                    var deleted = RegDeleteValueW(key, valueName);
                    return deleted == ErrorSuccess || deleted == ErrorFileNotFound;
                }

                var bytes = Encoding.Unicode.GetBytes(command + "\0");
                if (bytes.Length == 0 || bytes.Length > MaxValueBytes)
                    return false;
                return RegSetValueExW(key, valueName, 0, RegSz, bytes, (uint)bytes.Length) == ErrorSuccess;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        public bool TryReadCommand(string valueName, out string command)
        {
            command = null;
            if (string.IsNullOrEmpty(valueName))
                return false;

            var key = IntPtr.Zero;
            try
            {
                var opened = RegOpenKeyExW(HkeyCurrentUser, WindowsStartupEntry.RunSubKey, 0, KeyQueryValue, out key);
                if (opened == ErrorFileNotFound)
                    return true;
                if (opened != ErrorSuccess || key == IntPtr.Zero)
                    return false;
            }
            catch (Exception)
            {
                return false;
            }

            try
            {
                return TryReadString(key, valueName, out command);
            }
            catch (Exception)
            {
                command = null;
                return false;
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        static bool TryReadString(IntPtr key, string valueName, out string command)
        {
            command = null;
            uint type;
            uint size = 0;
            var probed = RegQueryValueExW(key, valueName, IntPtr.Zero, out type, null, ref size);
            if (probed == ErrorFileNotFound)
                return true;
            if (probed != ErrorSuccess && probed != ErrorMoreData)
                return false;
            if (size == 0)
                return true;
            if (size > MaxValueBytes)
                return false;

            var data = new byte[size];
            var read = RegQueryValueExW(key, valueName, IntPtr.Zero, out type, data, ref size);
            if (read == ErrorFileNotFound)
                return true;
            if (read != ErrorSuccess)
                return false;
            if (type != RegSz && type != RegExpandSz)
                return true;

            var count = (int)Math.Min(size, (uint)data.Length);
            if ((count & 1) != 0)
                count -= 1;
            if (count <= 0)
                return true;

            var text = Encoding.Unicode.GetString(data, 0, count);
            var end = text.IndexOf('\0');
            if (end >= 0)
                text = text.Substring(0, end);
            command = string.IsNullOrWhiteSpace(text) ? null : text;
            return true;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int RegOpenKeyExW(IntPtr hKey, string subKey, uint options, uint samDesired, out IntPtr result);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int RegCreateKeyExW(
            IntPtr hKey,
            string subKey,
            uint reserved,
            string className,
            uint options,
            uint samDesired,
            IntPtr securityAttributes,
            out IntPtr result,
            out uint disposition);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int RegQueryValueExW(
            IntPtr hKey,
            string valueName,
            IntPtr reserved,
            out uint type,
            byte[] data,
            ref uint dataBytes);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int RegSetValueExW(
            IntPtr hKey,
            string valueName,
            int reserved,
            uint type,
            byte[] data,
            uint dataBytes);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int RegDeleteValueW(IntPtr hKey, string valueName);

        [DllImport("advapi32.dll", ExactSpelling = true)]
        static extern int RegCloseKey(IntPtr hKey);
    }
}
#endif
