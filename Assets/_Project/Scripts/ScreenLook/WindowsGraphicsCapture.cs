#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AICompanion.ScreenLook
{
    /// <summary>
    /// Read-only Windows Graphics Capture for one HWND. A monitor is not a capture target.
    /// A failure returns an error string and does not fall back to the desktop.
    /// This file is compiled into the Windows player. It is not exercised on the Linux VM.
    /// </summary>
    static class WindowsGraphicsCaptureBackend
    {
        const int MaxWindows = 40;
        const int MaxTitle = 160;
        const int DwmCloaked = 14;
        const uint BgraSupport = 0x20;
        const int DriverHardware = 1;
        const uint SdkVersion = 7;
        const int FormatBgra = 87;
        const int UsageStaging = 3;
        const int CpuRead = 0x20000;
        const int MapRead = 1;

        static readonly Guid ItemInteropIid = new Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
        static readonly Guid ItemIid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
        static readonly Guid PoolStatics2Iid = new Guid("589B103F-6BBC-5DF5-A991-02E28B3B66D5");
        static readonly Guid SessionStaticsIid = new Guid("2224A540-5974-49AA-B232-0882536F4CB5");
        static readonly Guid DxgiAccessIid = CaptureAbi.DxgiAccessIid;
        static readonly Guid Texture2DIid = CaptureAbi.Texture2DIid;
        static readonly Guid DxgiDeviceIid = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
        static readonly Guid ClosableIid = new Guid("30D5A829-7FA4-4026-83BB-D75BAE4EA99E");

        static readonly EnumWindowsProc EnumThunk = OnEnum;
        static readonly List<WindowTarget> Enumerated = new List<WindowTarget>(64);
        static readonly object CaptureLock = new object();
        static int _ownProcess;
        static int _abort;

        public static void CancelActive()
        {
            Interlocked.Exchange(ref _abort, 1);
        }

        public static bool IsSupported => true;

        public static IReadOnlyList<WindowTarget> ListWindows()
        {
            var copy = new List<WindowTarget>();
            try
            {
                Enumerated.Clear();
                _ownProcess = GetCurrentProcessId();
                EnumWindows(EnumThunk, IntPtr.Zero);
                Enumerated.Sort((a, b) => (b.Width * b.Height).CompareTo(a.Width * a.Height));
                var limit = Enumerated.Count < MaxWindows ? Enumerated.Count : MaxWindows;
                for (var i = 0; i < limit; i++)
                    copy.Add(Enumerated[i]);
            }
            catch (Exception)
            {
                copy.Clear();
            }
            finally
            {
                Enumerated.Clear();
            }

            return copy;
        }

        public static CapturedWindowFrame CaptureWindow(WindowTarget target, long hwndValue)
        {
            var hwnd = new IntPtr(hwndValue);
            if (!IsWindow(hwnd))
                return CapturedWindowFrame.Fail(ScreenLookPolicy.Closed);
            if (IsIconic(hwnd))
                return CapturedWindowFrame.Fail(ScreenLookPolicy.Minimized);

            GetWindowThreadProcessId(hwnd, out var pid);
            if (target != null && target.ProcessId > 0 && pid != target.ProcessId)
                return CapturedWindowFrame.Fail(ScreenLookPolicy.Closed);
            if (pid == GetCurrentProcessId())
                return CapturedWindowFrame.Fail(ScreenLookPolicy.OwnWindow);

            var className = ReadClass(hwnd);
            if (WindowTarget.IsShellClass(className) || hwnd == GetShellWindow() || hwnd == GetDesktopWindow())
                return CapturedWindowFrame.Fail(ScreenLookPolicy.DesktopRefused);

            if (!Monitor.TryEnter(CaptureLock, 3000))
                return CapturedWindowFrame.Fail(CaptureDiagnostics.Stopped("Busy", 0));

            try
            {
                return Grab(hwnd, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                return CapturedWindowFrame.Fail(CaptureDiagnostics.Stopped(ex.GetType().Name, 0));
            }
            finally
            {
                Monitor.Exit(CaptureLock);
            }
        }

        /// <summary>
        /// One frame of a whole display. This path exists only after the separate
        /// whole-monitor consent; a failed window grab never widens to it.
        /// </summary>
        public static CapturedWindowFrame CaptureMonitor(MonitorTarget target)
        {
            if (target == null || target.Handle == IntPtr.Zero)
                return CapturedWindowFrame.Fail(ScreenLookPolicy.NeedMonitor);

            if (!Monitor.TryEnter(CaptureLock, 3000))
                return CapturedWindowFrame.Fail(CaptureDiagnostics.Stopped("Busy", 0));

            try
            {
                return Grab(IntPtr.Zero, target.Handle);
            }
            catch (Exception ex)
            {
                return CapturedWindowFrame.Fail(CaptureDiagnostics.Stopped(ex.GetType().Name, 0));
            }
            finally
            {
                Monitor.Exit(CaptureLock);
            }
        }

        static CapturedWindowFrame Grab(IntPtr hwnd, IntPtr hmon)
        {
            var epoch = Volatile.Read(ref _abort);
            var hrInit = RoInitialize(1);
            if (hrInit < 0 && unchecked((uint)hrInit) != 0x80010106)
                return Stopped("RoInitialize", hrInit);

            if (!QuerySupported())
                return CapturedWindowFrame.Fail(CaptureDiagnostics.Stopped("NotSupported", 0));

            IntPtr device = IntPtr.Zero;
            IntPtr context = IntPtr.Zero;
            IntPtr dxgi = IntPtr.Zero;
            IntPtr winrtDevice = IntPtr.Zero;
            IntPtr itemFactory = IntPtr.Zero;
            IntPtr item = IntPtr.Zero;
            IntPtr poolFactory = IntPtr.Zero;
            IntPtr pool = IntPtr.Zero;
            IntPtr session = IntPtr.Zero;
            IntPtr frame = IntPtr.Zero;
            IntPtr surface = IntPtr.Zero;
            IntPtr access = IntPtr.Zero;
            IntPtr texture = IntPtr.Zero;
            IntPtr staging = IntPtr.Zero;
            var mapped = false;
            var life = new CaptureLifetime();
            var width = 0;
            var height = 0;

            try
            {
                var hr = CreateDevice(out device, out context);
                if (hr < 0 || device == IntPtr.Zero || context == IntPtr.Zero)
                    return Stopped("CreateDevice", hr);
                hr = Query(device, DxgiDeviceIid, out dxgi);
                if (hr < 0 || dxgi == IntPtr.Zero)
                    return Stopped("DxgiDevice", hr);
                hr = CreateDirect3D11DeviceFromDXGIDevice(dxgi, out winrtDevice);
                if (hr < 0 || winrtDevice == IntPtr.Zero)
                    return Stopped("WinrtDevice", hr);

                hr = Activate("Windows.Graphics.Capture.GraphicsCaptureItem", ItemInteropIid, out itemFactory);
                if (hr < 0 || itemFactory == IntPtr.Zero)
                    return Stopped("ItemFactory", hr);
                var itemIid = ItemIid;
                if (hmon != IntPtr.Zero)
                {
                    hr = Fn<CreateForMonitorFn>(itemFactory, 4)(itemFactory, hmon, ref itemIid, out item);
                    if (hr < 0 || item == IntPtr.Zero)
                        return Stopped("CreateMonitorItem", hr);
                }
                else
                {
                    hr = Fn<CreateForWindowFn>(itemFactory, 3)(itemFactory, hwnd, ref itemIid, out item);
                    if (hr < 0 || item == IntPtr.Zero)
                        return Stopped("CreateItem", hr);
                }

                var sizeHr = Fn<GetSizeFn>(item, 7)(item, out var size);
                var clientW = 0;
                var clientH = 0;
                if (hmon != IntPtr.Zero)
                {
                    if (sizeHr < 0 || size.Width <= 0 || size.Height <= 0)
                        return Stopped("MonitorSize", sizeHr);
                    width = size.Width;
                    height = size.Height;
                }
                else
                {
                    if (!TryClientSize(hwnd, out clientW, out clientH))
                        return Stopped("ClientSize", 0);
                    if (!CaptureDiagnostics.TryChooseSize(sizeHr, size.Width, size.Height, clientW, clientH, out width, out height))
                        return Stopped("ItemSize", sizeHr);
                }

                hr = Activate("Windows.Graphics.Capture.Direct3D11CaptureFramePool", PoolStatics2Iid, out poolFactory);
                if (hr < 0 || poolFactory == IntPtr.Zero)
                    return Stopped("PoolFactory", hr);
                var poolSize = new SizeInt32 { Width = width, Height = height };
                hr = Fn<CreateFreeThreadedFn>(poolFactory, 6)(poolFactory, winrtDevice, FormatBgra, 2, poolSize, out pool);
                if (hr < 0 || pool == IntPtr.Zero)
                    return Stopped("CreatePool", hr);
                hr = Fn<CreateSessionFn>(pool, 10)(pool, item, out session);
                if (hr < 0 || session == IntPtr.Zero)
                    return Stopped("CreateSession", hr);

                hr = Fn<StartFn>(session, 6)(session);
                if (hr < 0)
                    return Stopped("StartCapture", hr);

                for (var i = 0; i < 40 && frame == IntPtr.Zero; i++)
                {
                    if (Volatile.Read(ref _abort) != epoch)
                        return Stopped("Cancelled", 0);
                    if (hmon == IntPtr.Zero)
                    {
                        if (!IsWindow(hwnd))
                            return Stopped("WindowClosed", 0);
                        if (IsIconic(hwnd))
                            return Stopped("Minimized", 0);
                        if (!TryClientSize(hwnd, out var nowW, out var nowH) || CaptureDiagnostics.SizeChanged(clientW, clientH, nowW, nowH))
                            return Stopped("WindowResized", 0);
                    }

                    var frameHr = Fn<TryFrameFn>(pool, 7)(pool, out frame);
                    if (frameHr < 0)
                        return Stopped("WaitFrame", frameHr);
                    if (frame == IntPtr.Zero)
                        Thread.Sleep(15);
                }

                if (frame == IntPtr.Zero)
                    return Stopped("WaitFrame", 0);
                if (hmon == IntPtr.Zero &&
                    !CaptureDiagnostics.AllowsUpload(true, false, Volatile.Read(ref _abort) != epoch, !IsWindow(hwnd), IsIconic(hwnd)))
                    return Stopped(Volatile.Read(ref _abort) != epoch ? "Cancelled" : "WindowClosed", 0);
                if (hmon != IntPtr.Zero && Volatile.Read(ref _abort) != epoch)
                    return Stopped("Cancelled", 0);

                hr = Fn<GetSurfaceFn>(frame, 6)(frame, out surface);
                if (hr < 0 || surface == IntPtr.Zero)
                    return Stopped("FrameSurface", hr);
                hr = Query(surface, DxgiAccessIid, out access);
                if (hr < 0 || access == IntPtr.Zero)
                    return Stopped("DxgiAccess", hr);
                var textureIid = Texture2DIid;
                hr = Fn<GetInterfaceFn>(access, CaptureAbi.DxgiGetInterfaceSlot)(access, ref textureIid, out texture);
                if (hr < 0 || texture == IntPtr.Zero)
                    return Stopped("Texture.GetInterface", hr, texture == IntPtr.Zero);

                Fn<GetDescFn>(texture, 10)(texture, out var desc);
                if (!CaptureDiagnostics.Matches(width, height, desc.Width, desc.Height))
                    return Stopped("TextureSize", 0);

                var stagingDesc = desc;
                stagingDesc.MipLevels = 1;
                stagingDesc.ArraySize = 1;
                stagingDesc.Sample.Count = 1;
                stagingDesc.Sample.Quality = 0;
                stagingDesc.Usage = UsageStaging;
                stagingDesc.BindFlags = 0;
                stagingDesc.CpuAccessFlags = CpuRead;
                stagingDesc.MiscFlags = 0;
                hr = Fn<CreateTextureFn>(device, 5)(device, ref stagingDesc, IntPtr.Zero, out staging);
                if (hr < 0 || staging == IntPtr.Zero)
                    return Stopped("StagingTexture", hr);

                Fn<CopyFn>(context, 47)(context, staging, texture);
                hr = Fn<MapFn>(context, 14)(context, staging, 0, MapRead, 0, out var mappedBox);
                if (hr < 0 || mappedBox.Data == IntPtr.Zero)
                    return Stopped("Map", hr);
                mapped = true;
                if (mappedBox.RowPitch < desc.Width * 4)
                    return Stopped("MapPitch", 0);

                var bgra = new byte[desc.Width * desc.Height * 4];
                for (var y = 0; y < desc.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(mappedBox.Data, y * mappedBox.RowPitch), bgra, y * desc.Width * 4, desc.Width * 4);
                }

                Fn<UnmapFn>(context, 15)(context, staging, 0);
                mapped = false;
                return new CapturedWindowFrame(true, string.Empty, bgra, desc.Width, desc.Height);
            }
            finally
            {
                if (mapped && context != IntPtr.Zero && staging != IntPtr.Zero)
                {
                    try
                    {
                        Fn<UnmapFn>(context, 15)(context, staging, 0);
                    }
                    catch (Exception)
                    {
                    }
                }

                Release(ref staging);
                Release(ref texture);
                Release(ref access);
                Release(ref surface);
                life.TryClose(
                    () => Release(ref frame),
                    () => CloseAndRelease(ref session),
                    () => CloseAndRelease(ref pool));
                Release(ref item);
                Release(ref poolFactory);
                Release(ref itemFactory);
                Release(ref winrtDevice);
                Release(ref dxgi);
                Release(ref context);
                Release(ref device);
            }
        }

        static bool QuerySupported()
        {
            IntPtr factory = IntPtr.Zero;
            try
            {
                if (Activate("Windows.Graphics.Capture.GraphicsCaptureSession", SessionStaticsIid, out factory) < 0 || factory == IntPtr.Zero)
                    return false;
                var supported = 0;
                if (Fn<IsSupportedFn>(factory, 6)(factory, out supported) < 0)
                    return false;
                return supported != 0;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                Release(ref factory);
            }
        }

        static int CreateDevice(out IntPtr device, out IntPtr context)
        {
            return D3D11CreateDevice(IntPtr.Zero, DriverHardware, IntPtr.Zero, BgraSupport, IntPtr.Zero, 0, SdkVersion, out device, out _, out context);
        }

        static bool TryClientSize(IntPtr hwnd, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (!GetClientRect(hwnd, out var rect))
                return false;
            width = rect.Right - rect.Left;
            height = rect.Bottom - rect.Top;
            return width > 0 && height > 0;
        }

        static CapturedWindowFrame Stopped(string stage, int hr, bool nullPointer = false)
        {
            return CapturedWindowFrame.Fail(CaptureDiagnostics.Stopped(stage, hr, nullPointer));
        }

        static int Activate(string className, Guid iid, out IntPtr factory)
        {
            factory = IntPtr.Zero;
            var hr = WindowsCreateString(className, className.Length, out var name);
            if (hr < 0)
                return hr;
            try
            {
                return RoGetActivationFactory(name, ref iid, out factory);
            }
            finally
            {
                WindowsDeleteString(name);
            }
        }

        static int Query(IntPtr com, Guid iid, out IntPtr result)
        {
            result = IntPtr.Zero;
            if (com == IntPtr.Zero)
                return -1;
            return Fn<QueryFn>(com, 0)(com, ref iid, out result);
        }

        static void CloseAndRelease(ref IntPtr com)
        {
            var ptr = com;
            com = IntPtr.Zero;
            if (ptr == IntPtr.Zero)
                return;

            IntPtr closable = IntPtr.Zero;
            try
            {
                if (Query(ptr, ClosableIid, out closable) >= 0 && closable != IntPtr.Zero)
                    Fn<StartFn>(closable, 6)(closable);
            }
            catch (Exception)
            {
            }

            Release(ref closable);
            var leftover = ptr;
            Release(ref leftover);
        }

        static void Release(ref IntPtr com)
        {
            if (com == IntPtr.Zero)
                return;
            try
            {
                Fn<ReleaseFn>(com, 2)(com);
            }
            catch (Exception)
            {
            }

            com = IntPtr.Zero;
        }

        static int OnEnum(IntPtr hwnd, IntPtr lparam)
        {
            try
            {
                if (!IsWindowVisible(hwnd) || IsIconic(hwnd))
                    return 1;
                if (hwnd == GetShellWindow() || hwnd == GetDesktopWindow())
                    return 1;

                var cloaked = 0;
                if (DwmGetWindowAttribute(hwnd, DwmCloaked, out cloaked, 4) == 0 && cloaked != 0)
                    return 1;

                GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == 0 || pid == _ownProcess)
                    return 1;

                var className = ReadClass(hwnd);
                if (WindowTarget.IsShellClass(className) || className == "IME" || className == "MSCTFIME UI")
                    return 1;

                if (!GetWindowRect(hwnd, out var rect))
                    return 1;
                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;
                if (width < 48 || height < 48)
                    return 1;

                var title = ReadTitle(hwnd);
                if (title.Length == 0 && (width < 200 || height < 200))
                    return 1;
                if (title.Length == 0)
                    title = "(untitled window)";

                var monitor = 0;
                var screenW = GetSystemMetrics(0);
                var screenH = GetSystemMetrics(1);
                if (screenW > 0 && screenH > 0 && width >= screenW - 8 && height >= screenH - 8)
                    monitor = 1;

                Enumerated.Add(new WindowTarget
                {
                    Hwnd = hwnd.ToInt64(),
                    ProcessId = pid,
                    Title = title,
                    ClassName = className,
                    Minimized = false,
                    IsShell = false,
                    IsOwnProcess = false,
                    IsAlive = true,
                    Width = width,
                    Height = height,
                    CoversMonitor = monitor == 1
                });
            }
            catch (Exception)
            {
            }

            return 1;
        }

        static string ReadTitle(IntPtr hwnd)
        {
            var buffer = new StringBuilder(MaxTitle);
            var length = GetWindowText(hwnd, buffer, buffer.Capacity);
            if (length <= 0)
                return string.Empty;
            return buffer.ToString().Trim();
        }

        static string ReadClass(IntPtr hwnd)
        {
            var buffer = new StringBuilder(128);
            if (GetClassName(hwnd, buffer, buffer.Capacity) <= 0)
                return string.Empty;
            return buffer.ToString();
        }

        static T Fn<T>(IntPtr com, int index) where T : class
        {
            var table = Marshal.ReadIntPtr(com);
            var slot = Marshal.ReadIntPtr(table, index * IntPtr.Size);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(slot, typeof(T));
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SizeInt32
        {
            public int Width;
            public int Height;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RectInt
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SampleDesc
        {
            public int Count;
            public int Quality;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct TextureDesc
        {
            public int Width;
            public int Height;
            public int MipLevels;
            public int ArraySize;
            public int Format;
            public SampleDesc Sample;
            public int Usage;
            public int BindFlags;
            public int CpuAccessFlags;
            public int MiscFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MappedBox
        {
            public IntPtr Data;
            public int RowPitch;
            public int DepthPitch;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int EnumWindowsProc(IntPtr hwnd, IntPtr lparam);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int QueryFn(IntPtr self, ref Guid iid, out IntPtr result);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate uint ReleaseFn(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int CreateForWindowFn(IntPtr self, IntPtr hwnd, ref Guid iid, out IntPtr result);

        /// <summary>IGraphicsCaptureItemInterop.CreateForMonitor follows CreateForWindow in the vtable.</summary>
        delegate int CreateForMonitorFn(IntPtr self, IntPtr monitor, ref Guid iid, out IntPtr result);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetSizeFn(IntPtr self, out SizeInt32 size);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int CreateFreeThreadedFn(IntPtr self, IntPtr device, int format, int buffers, SizeInt32 size, out IntPtr pool);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int CreateSessionFn(IntPtr self, IntPtr item, out IntPtr session);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int StartFn(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int TryFrameFn(IntPtr self, out IntPtr frame);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetSurfaceFn(IntPtr self, out IntPtr surface);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int GetInterfaceFn(IntPtr self, ref Guid iid, out IntPtr result);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int IsSupportedFn(IntPtr self, out int supported);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void GetDescFn(IntPtr self, out TextureDesc desc);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int CreateTextureFn(IntPtr self, ref TextureDesc desc, IntPtr initialData, out IntPtr texture);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void CopyFn(IntPtr self, IntPtr destination, IntPtr source);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int MapFn(IntPtr self, IntPtr resource, int subresource, int mapType, int mapFlags, out MappedBox mapped);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void UnmapFn(IntPtr self, IntPtr resource, int subresource);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lparam);

        [DllImport("user32.dll")]
        static extern bool IsWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hwnd, out RectInt rect);

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hwnd, out RectInt rect);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int processId);

        [DllImport("user32.dll")]
        static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
        static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
        static extern int GetClassName(IntPtr hwnd, StringBuilder text, int max);

        [DllImport("kernel32.dll")]
        static extern int GetCurrentProcessId();

        [DllImport("dwmapi.dll")]
        static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

        [DllImport("d3d11.dll")]
        static extern int D3D11CreateDevice(
            IntPtr adapter,
            int driverType,
            IntPtr software,
            uint flags,
            IntPtr featureLevels,
            uint featureLevelCount,
            uint sdkVersion,
            out IntPtr device,
            out int featureLevel,
            out IntPtr context);

        [DllImport("d3d11.dll")]
        static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        [DllImport("combase.dll")]
        static extern int RoInitialize(int initType);

        [DllImport("combase.dll")]
        static extern int RoGetActivationFactory(IntPtr className, ref Guid iid, out IntPtr factory);

        [DllImport("combase.dll")]
        static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string source, int length, out IntPtr hstring);

        [DllImport("combase.dll")]
        static extern int WindowsDeleteString(IntPtr hstring);
    }
}
#endif
