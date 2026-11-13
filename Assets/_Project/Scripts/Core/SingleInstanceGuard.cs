using System;
using System.Threading;
using UnityEngine;

namespace AICompanion.Core
{
    /// <summary>
    /// One process at a time. The editor is never blocked, and -allowmulti skips the guard
    /// for two-instance testing. On a second launch the existing window is left alone and
    /// this process logs once before quitting.
    /// </summary>
    public static class SingleInstanceGuard
    {
        public const string MutexName = "BackseatCatgirl.SingleInstance";
        public const string AllowMultiFlag = "-allowmulti";

        static Mutex _mutex;

        /// <summary>True in a second process while it waits to exit. All save I/O is skipped.</summary>
        public static bool IsExiting { get; private set; }

        public static bool IsEditor
        {
            get { return Application.isEditor; }
        }

        public static bool AllowsMultiple(string[] args)
        {
            if (args == null)
                return false;
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], AllowMultiFlag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool TryAcquire()
        {
            if (IsEditor || AllowsMultiple(Environment.GetCommandLineArgs()))
            {
                IsExiting = false;
                return true;
            }

            try
            {
                var mutex = new Mutex(true, MutexName, out var createdNew);
                if (!createdNew)
                {
                    IsExiting = true;
                    mutex.Dispose();
                    return false;
                }

                _mutex = mutex;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Single instance guard failed: " + e.GetType().Name + ": " + e.Message);
                return true;
            }
        }

        /// <summary>Test hook for the exit path; production sets this through TryAcquire.</summary>
        internal static void SetExitingForTests(bool exiting)
        {
            IsExiting = exiting;
        }

        public static void Release()
        {
            if (_mutex == null)
                return;
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (Exception)
            {
            }

            _mutex.Dispose();
            _mutex = null;
        }
    }
}
