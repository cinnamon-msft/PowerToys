// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using DoNotDisturb.Native;
using DoNotDisturb.Services;
using ManagedCommon;

namespace DoNotDisturb
{
    /// <summary>
    /// Helper class to manage the system tray icon.
    /// </summary>
    internal sealed class TrayHelper : IDisposable
    {
        private const int TrayIconId = 1001;
        private const int MenuIdToggle = 1;
        private const int MenuIdAutoMode = 2;
        private const int MenuIdOpen = 3;
        private const int MenuIdExit = 4;

        private readonly DndManager _dndManager;
        private readonly Action _showWindowAction;
        private readonly Action _exitAction;

        private NativeMethods.NotifyIconData _notifyIconData;
        private IntPtr _windowHandle;
        private Icon? _iconOn;
        private Icon? _iconOff;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrayHelper"/> class.
        /// </summary>
        /// <param name="dndManager">The DND manager.</param>
        /// <param name="windowHandle">The window handle for callbacks.</param>
        /// <param name="showWindowAction">Action to show the main window.</param>
        /// <param name="exitAction">Action to exit the application.</param>
        public TrayHelper(DndManager dndManager, IntPtr windowHandle, Action showWindowAction, Action exitAction)
        {
            _dndManager = dndManager ?? throw new ArgumentNullException(nameof(dndManager));
            _windowHandle = windowHandle;
            _showWindowAction = showWindowAction ?? throw new ArgumentNullException(nameof(showWindowAction));
            _exitAction = exitAction ?? throw new ArgumentNullException(nameof(exitAction));

            LoadIcons();
            _dndManager.StateChanged += OnStateChanged;
        }

        /// <summary>
        /// Initializes the tray icon.
        /// </summary>
        public void Initialize()
        {
            try
            {
                var icon = _dndManager.IsDndEnabled ? _iconOn : _iconOff;

                _notifyIconData = new NativeMethods.NotifyIconData
                {
                    CbSize = Marshal.SizeOf<NativeMethods.NotifyIconData>(),
                    HWnd = _windowHandle,
                    UId = TrayIconId,
                    UFlags = NativeMethods.NifIcon | NativeMethods.NifTip | NativeMethods.NifMessage,
                    UCallbackMessage = NativeMethods.WmUser + 1,
                    HIcon = icon?.Handle ?? IntPtr.Zero,
                    SzTip = GetTooltip(),
                };

                if (!NativeMethods.Shell_NotifyIcon(NativeMethods.NimAdd, ref _notifyIconData))
                {
                    Logger.LogError($"Failed to add tray icon. Error: {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Logger.LogInfo("Tray icon added successfully");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize tray icon: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the tray icon callback message.
        /// </summary>
        /// <param name="lParam">The message lParam containing the mouse event.</param>
        public void HandleCallback(nint lParam)
        {
            var msg = (int)(lParam & 0xFFFF);

            switch (msg)
            {
                case NativeMethods.WmLbuttondown:
                case NativeMethods.WmLbuttondblclk:
                    _showWindowAction();
                    break;

                case NativeMethods.WmRbuttondown:
                    ShowContextMenu();
                    break;
            }
        }

        /// <summary>
        /// Handles menu command.
        /// </summary>
        /// <param name="commandId">The menu command ID.</param>
        public void HandleMenuCommand(int commandId)
        {
            switch (commandId)
            {
                case MenuIdToggle:
                    _dndManager.ToggleDnd();
                    break;

                case MenuIdAutoMode:
                    var newMode = _dndManager.Mode == DndMode.Auto ? DndMode.Manual : DndMode.Auto;
                    _dndManager.SetMode(newMode);
                    break;

                case MenuIdOpen:
                    _showWindowAction();
                    break;

                case MenuIdExit:
                    _exitAction();
                    break;
            }
        }

        private void ShowContextMenu()
        {
            var menu = NativeMethods.CreatePopupMenu();
            if (menu == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Toggle DND
                var toggleText = _dndManager.IsDndEnabled ? "Disable Do Not Disturb" : "Enable Do Not Disturb";
                NativeMethods.InsertMenu(menu, 0, NativeMethods.MfString, MenuIdToggle, toggleText);

                // Separator
                NativeMethods.InsertMenu(menu, 1, NativeMethods.MfSeparator, 0, string.Empty);

                // Auto Mode
                var autoModeFlags = NativeMethods.MfString;
                if (_dndManager.Mode == DndMode.Auto)
                {
                    autoModeFlags |= NativeMethods.MfChecked;
                }

                NativeMethods.InsertMenu(menu, 2, autoModeFlags, MenuIdAutoMode, "Auto Mode (Calendar-based)");

                // Separator
                NativeMethods.InsertMenu(menu, 3, NativeMethods.MfSeparator, 0, string.Empty);

                // Open Window
                NativeMethods.InsertMenu(menu, 4, NativeMethods.MfString, MenuIdOpen, "Open Do Not Disturb");

                // Exit
                NativeMethods.InsertMenu(menu, 5, NativeMethods.MfString, MenuIdExit, "Exit");

                // Show menu
                NativeMethods.SetForegroundWindow(_windowHandle);
                NativeMethods.GetCursorPos(out var cursorPos);
                NativeMethods.TrackPopupMenuEx(
                    menu,
                    NativeMethods.TpmLeftalign | NativeMethods.TpmBottomalign | NativeMethods.TpmLeftbutton,
                    cursorPos.X,
                    cursorPos.Y,
                    _windowHandle,
                    IntPtr.Zero);
            }
            finally
            {
                NativeMethods.DestroyMenu(menu);
            }
        }

        private void OnStateChanged(object? sender, DndStateChangedEventArgs e)
        {
            UpdateIcon();
        }

        private void UpdateIcon()
        {
            try
            {
                var icon = _dndManager.IsDndEnabled ? _iconOn : _iconOff;

                _notifyIconData.HIcon = icon?.Handle ?? IntPtr.Zero;
                _notifyIconData.SzTip = GetTooltip();

                NativeMethods.Shell_NotifyIcon(NativeMethods.NimModify, ref _notifyIconData);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to update tray icon: {ex.Message}");
            }
        }

        private string GetTooltip()
        {
            var status = _dndManager.IsDndEnabled ? "ON" : "OFF";
            var mode = _dndManager.Mode == DndMode.Auto ? " (Auto)" : string.Empty;
            return $"Do Not Disturb: {status}{mode}";
        }

        private void LoadIcons()
        {
            try
            {
                var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                var onPath = Path.Combine(basePath, "Assets", "DoNotDisturb", "dnd-on.ico");
                var offPath = Path.Combine(basePath, "Assets", "DoNotDisturb", "dnd-off.ico");

                if (File.Exists(onPath))
                {
                    _iconOn = new Icon(onPath);
                }

                if (File.Exists(offPath))
                {
                    _iconOff = new Icon(offPath);
                }

                // Fallback to default app icon
                if (_iconOn == null || _iconOff == null)
                {
                    var defaultPath = Path.Combine(basePath, "Assets", "DoNotDisturb", "DoNotDisturb.ico");
                    if (File.Exists(defaultPath))
                    {
                        var defaultIcon = new Icon(defaultPath);
                        _iconOn ??= defaultIcon;
                        _iconOff ??= defaultIcon;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load tray icons: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!_disposed)
            {
                _dndManager.StateChanged -= OnStateChanged;

                // Remove tray icon
                NativeMethods.Shell_NotifyIcon(NativeMethods.NimDelete, ref _notifyIconData);

                _iconOn?.Dispose();
                _iconOff?.Dispose();

                _disposed = true;
            }
        }
    }
}
