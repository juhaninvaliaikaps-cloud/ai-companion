using System;
using System.Collections.Generic;
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
    /// Failure text for Player.log and the chat note. Stages and HRESULTs only.
    /// Window titles and pixels do not belong in these strings.
    /// </summary>
    public static class CaptureDiagnostics
    {
        public static string Stopped(string stage, int hresult)
        {
            var name = string.IsNullOrEmpty(stage) ? "Unknown" : stage.Trim();
            if (name.Length > 40)
                name = name.Substring(0, 40);
            if (hresult >= 0)
                return "Capture stopped at " + name + ". Nothing was sent.";
            return "Capture stopped at " + name + " (" + unchecked((uint)hresult).ToString("X8") + "). Nothing was sent.";
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
