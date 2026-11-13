using System;
using System.Runtime.InteropServices;

namespace AICompanion.Core
{
    /// <summary>
    /// Windows DPAPI for the current user. <c>CRYPTPROTECT_LOCAL_MACHINE</c> is not set,
    /// so the blob unseals only for this Windows account. The UI prompt flag is forbidden.
    /// </summary>
    public static class DpapiBlob
    {
        const int CryptProtectUiForbidden = 0x1;

        public static byte[] Protect(byte[] plain)
        {
            if (plain == null)
                throw new ArgumentNullException("plain");
            return Transform(plain, true);
        }

        public static byte[] Unprotect(byte[] encrypted)
        {
            if (encrypted == null)
                throw new ArgumentNullException("encrypted");
            return Transform(encrypted, false);
        }

        static byte[] Transform(byte[] input, bool protect)
        {
            var inputHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
            var output = new DataBlob();
            try
            {
                var incoming = new DataBlob
                {
                    cbData = input.Length,
                    pbData = inputHandle.AddrOfPinnedObject()
                };
                var ok = protect
                    ? CryptProtectData(ref incoming, "ProjectLeia", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref output)
                    : CryptUnprotectData(ref incoming, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, ref output);
                if (!ok || output.pbData == IntPtr.Zero || output.cbData <= 0)
                    throw new InvalidOperationException(protect
                        ? "Windows could not protect the install key."
                        : "Windows could not read the install key.");

                var managed = new byte[output.cbData];
                Marshal.Copy(output.pbData, managed, 0, output.cbData);
                return managed;
            }
            finally
            {
                if (output.pbData != IntPtr.Zero)
                    LocalFree(output.pbData);
                inputHandle.Free();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DataBlob
        {
            public int cbData;
            public IntPtr pbData;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string description,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            ref DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            IntPtr description,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            ref DataBlob dataOut);

        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr handle);
    }
}
