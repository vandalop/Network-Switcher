using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NetworkSwitcher
{
    public static class MemoryHelper
    {
        [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, int dwMinimumWorkingSetSize, int dwMaximumWorkingSetSize);

        [DllImport("psapi.dll", EntryPoint = "EmptyWorkingSet")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        public static void TrimWorkingSet()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);

                IntPtr handle = Process.GetCurrentProcess().Handle;
                EmptyWorkingSet(handle);
                SetProcessWorkingSetSize(handle, -1, -1);
            }
            catch { }
        }
    }
}
