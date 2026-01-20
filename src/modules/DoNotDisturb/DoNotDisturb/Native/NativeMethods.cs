// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace DoNotDisturb.Native
{
    /// <summary>
    /// Native Win32 interop methods for tray icon.
    /// </summary>
    internal static class NativeMethods
    {
        internal const int NimAdd = 0x00;
        internal const int NimModify = 0x01;
        internal const int NimDelete = 0x02;

        internal const int NifMessage = 0x01;
        internal const int NifIcon = 0x02;
        internal const int NifTip = 0x04;

        internal const int WmUser = 0x0400;
        internal const int WmLbuttondown = 0x0201;
        internal const int WmRbuttondown = 0x0204;
        internal const int WmLbuttondblclk = 0x0203;
        internal const int WmCommand = 0x0111;
        internal const int WmDestroy = 0x0002;

        // Window subclassing constants
        internal const int GwlpWndproc = -4;

        /// <summary>
        /// Delegate for window procedure.
        /// </summary>
        /// <param name="hWnd">Window handle.</param>
        /// <param name="msg">Message identifier.</param>
        /// <param name="wParam">First message parameter.</param>
        /// <param name="lParam">Second message parameter.</param>
        /// <returns>Result of message processing.</returns>
        internal delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
        internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        internal static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        internal const uint TpmLeftalign = 0x0000;
        internal const uint TpmBottomalign = 0x0020;
        internal const uint TpmLeftbutton = 0x0000;

        internal const uint MfString = 0x00000000;
        internal const uint MfSeparator = 0x00000800;
        internal const uint MfChecked = 0x00000008;
        internal const uint MfGrayed = 0x00000001;

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool Shell_NotifyIcon(int dwMessage, ref NotifyIconData pnid);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, uint uIdNewItem, string lpNewItem);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern void PostQuitMessage(int nExitCode);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct NotifyIconData
        {
            public int CbSize;
            public IntPtr HWnd;
            public int UId;
            public int UFlags;
            public int UCallbackMessage;
            public IntPtr HIcon;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string SzTip;
        }
    }
}
