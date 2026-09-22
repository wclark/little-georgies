#if UNITY_EDITOR_WIN
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LittleGeorgies.Economy
{
    public static class AuctionNative
    {
        static IntPtr library;
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);

        public static void Initialize()
        {
            if (library != IntPtr.Zero) return;
            if (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer)
                throw new PlatformNotSupportedException("This auction solver build currently supports Windows x64.");
            string folder = Application.isEditor ? "Plugins/AuctionSolver/x86_64" : "Plugins/x86_64";
            string file = Path.Combine(Application.dataPath, folder, "google-ortools-native.dll");
            // Resolve native dependencies beside the pinned library, without changing the process DLL path.
            library = LoadLibraryEx(file, IntPtr.Zero, 0x100 | 0x1000);
            if (library == IntPtr.Zero) throw new InvalidOperationException("Auction solver could not load (Windows error "
                + Marshal.GetLastWin32Error() + "). Run tools/Install-AuctionSolver.ps1 and rebuild. Path: " + file);
        }
    }
}
#endif
