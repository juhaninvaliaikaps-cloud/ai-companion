using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AICompanion.ScreenLook
{
    /// <summary>
    /// Orders and de-duplicates native capture teardown. The frame is released,
    /// then the session is closed, then the pool. A second call does nothing.
    /// Closing the frame itself is intentionally not a step: that unlink plus
    /// the pool close is a double remove on the capture list.
    /// </summary>
    public sealed class CaptureLifetime
    {
        readonly List<string> _steps = new List<string>(3);
        int _closed;

        public IReadOnlyList<string> Steps => _steps;

        public bool TryClose(Action releaseFrame, Action closeSession, Action closePool)
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
                return false;

            Run("release-frame", releaseFrame);
            Run("close-session", closeSession);
            Run("close-pool", closePool);
            return true;
        }

        void Run(string step, Action action)
        {
            _steps.Add(step);
            if (action != null)
                action();
        }
    }

    /// <summary>
    /// Slots and IIDs shared with the Windows capture backend. Tests lock these
    /// so a WinRT offset is not used on an IUnknown interface.
    /// </summary>
    public static class CaptureAbi
    {
        public static readonly Guid DxgiAccessIid = new Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");
        public static readonly Guid Texture2DIid = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

        /// <summary>
        /// IDirect3DDxgiInterfaceAccess derives from IUnknown. QueryInterface,
        /// AddRef, and Release occupy slots 0-2. GetInterface is slot 3.
        /// Slot 6 is the first method of an IInspectable interface and is not this call.
        /// </summary>
        public const int DxgiGetInterfaceSlot = 3;
    }

    /// <summary>
    /// Failure text for Player.log and the chat note. Stages and HRESULTs only.
    /// Window titles and pixels do not belong in these strings.
    /// </summary>
    public static class CaptureDiagnostics
    {
        public static string Stopped(string stage, int hresult, bool nullPointer = false)
        {
            var name = string.IsNullOrEmpty(stage) ? "Unknown" : stage.Trim();
            if (name.Length > 48)
                name = name.Substring(0, 48);
            var detail = string.Empty;
            if (hresult < 0)
                detail = " (" + unchecked((uint)hresult).ToString("X8") + ")";
            if (nullPointer)
                detail += " null pointer";
            return "Capture stopped at " + name + detail + ". Nothing was sent.";
        }

        public static string Ready(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return Stopped("FrameSize", 0);
            return "Screen capture: frame ready " + width.ToString() + "x" + height.ToString();
        }

        public static string LogLine(bool ok, int width, int height, string error)
        {
            if (!ok)
            {
                if (string.IsNullOrEmpty(error))
                    return "Screen capture: " + Stopped("Unknown", 0);
                return "Screen capture: " + error;
            }

            return Ready(width, height);
        }

        public static bool TryChooseSize(int itemHr, int itemW, int itemH, int clientW, int clientH, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (!Fits(clientW, clientH))
                return false;
            if (itemHr >= 0 && Fits(itemW, itemH) && Near(itemW, clientW) && Near(itemH, clientH))
            {
                width = itemW;
                height = itemH;
                return true;
            }

            width = clientW;
            height = clientH;
            return true;
        }

        public static bool SizeChanged(int startW, int startH, int nowW, int nowH)
        {
            if (nowW <= 0 || nowH <= 0)
                return true;
            return Math.Abs(nowW - startW) > 8 || Math.Abs(nowH - startH) > 8;
        }

        public static bool Matches(int expectedW, int expectedH, int actualW, int actualH)
        {
            if (expectedW <= 0 || expectedH <= 0 || actualW <= 0 || actualH <= 0)
                return false;
            return Math.Abs(actualW - expectedW) <= 64 && Math.Abs(actualH - expectedH) <= 64;
        }

        public static bool AllowsUpload(bool nativeOk, bool sizeChanged, bool cancelled, bool windowClosed, bool minimized)
        {
            return nativeOk && !sizeChanged && !cancelled && !windowClosed && !minimized;
        }

        public static string RouteLine(string route, bool send, bool cloudConsent, bool privateMode, string providerId, string visionModel, string chatModel)
        {
            return "Screen look: route=" + Token(route)
                + " send=" + Bit(send)
                + " cloudConsent=" + Bit(cloudConsent)
                + " private=" + Bit(privateMode)
                + " provider=" + ProviderToken(providerId)
                + " vision=" + ModelToken(visionModel)
                + " chat=" + ModelToken(chatModel);
        }

        public static string FrameLine(string route, int captureW, int captureH, int sentW, int sentH, int scale, int quality, int jpegBytes, int imageFieldChars, int promptChars = 0)
        {
            var line = "Screen look: frame route=" + Token(route)
                + " capture=" + NonNeg(captureW) + "x" + NonNeg(captureH)
                + " sent=" + NonNeg(sentW) + "x" + NonNeg(sentH)
                + " scale=" + NonNeg(scale)
                + " quality=" + NonNeg(quality)
                + " jpegBytes=" + NonNeg(jpegBytes)
                + " imageFieldChars=" + NonNeg(imageFieldChars);
            if (promptChars > 0)
                line += " promptChars=" + promptChars;
            return line;
        }

        static int NonNeg(int value)
        {
            return value < 0 ? 0 : value;
        }

        public static string VisionLine(int statusCode, bool ok, bool cancelled, int observationChars)
        {
            if (observationChars < 0)
                observationChars = 0;
            return "Screen look: localVision status=" + statusCode.ToString()
                + " ok=" + Bit(ok)
                + " cancelled=" + Bit(cancelled)
                + " observationChars=" + observationChars.ToString();
        }

        public static string ChatLine(string provider, bool imageAttached, string measure, int amount, bool started)
        {
            if (amount < 0)
                amount = 0;
            var name = measure == "jpegBytes" ? "jpegBytes" : "observationChars";
            return "Screen look: chat provider=" + ProviderToken(provider)
                + " imageAttached=" + Bit(imageAttached)
                + " " + name + "=" + amount.ToString()
                + " started=" + Bit(started);
        }

        static string Bit(bool value)
        {
            return value ? "1" : "0";
        }

        static string ProviderToken(string providerId)
        {
            if (string.Equals(providerId, "local", StringComparison.OrdinalIgnoreCase))
                return "local";
            if (string.Equals(providerId, "grok", StringComparison.OrdinalIgnoreCase))
                return "grok";
            return "other";
        }

        static string ModelToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unset";
            var sb = new StringBuilder();
            for (var i = 0; i < value.Length && sb.Length < 48; i++)
            {
                var c = value[i];
                if (c < 0x20 || c == '"' || c == '\'' )
                    continue;
                sb.Append(c);
            }

            return sb.Length == 0 ? "unset" : sb.ToString();
        }

        static string Token(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "none";
            var name = value.Trim().ToLowerInvariant();
            if (name != "local" && name != "cloud" && name != "none")
                return "none";
            return name;
        }

        static bool Fits(int width, int height)
        {
            return width > 0 && height > 0 && width <= 4096 && height <= 4096;
        }

        static bool Near(int item, int client)
        {
            var delta = item - client;
            if (delta < 0)
                delta = -delta;
            return delta <= 64;
        }
    }
}
