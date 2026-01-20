// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.Win32;

namespace DoNotDisturb.Services
{
    /// <summary>
    /// Represents the Focus Assist (Do Not Disturb) state.
    /// </summary>
    public enum FocusAssistState
    {
        /// <summary>
        /// Focus Assist is off - all notifications are shown.
        /// </summary>
        Off = 0,

        /// <summary>
        /// Priority only mode - only priority notifications are shown.
        /// </summary>
        PriorityOnly = 1,

        /// <summary>
        /// Alarms only mode - only alarms are shown.
        /// </summary>
        AlarmsOnly = 2,
    }

    /// <summary>
    /// Controller for Windows Focus Assist (Do Not Disturb) feature.
    /// Uses registry to toggle notifications.
    /// </summary>
    public class FocusAssistController
    {
        private const string NotificationsSettingsPath = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";
        private const string ToastsEnabledKey = "NOC_GLOBAL_SETTING_TOASTS_ENABLED";

        // For broadcasting settings change
        private const int HwndBroadcast = 0xFFFF;
        private const int WmSettingchange = 0x001A;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint msg,
            UIntPtr wParam,
            string lParam,
            uint fuFlags,
            uint uTimeout,
            out UIntPtr lpdwResult);

        /// <summary>
        /// Event raised when the focus state changes.
        /// </summary>
        public event EventHandler<bool>? FocusStateChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="FocusAssistController"/> class.
        /// </summary>
        public FocusAssistController()
        {
            Logger.LogInfo($"FocusAssistController initialized. Build: {Environment.OSVersion.Version.Build}");
        }

        /// <summary>
        /// Gets the current Focus Assist state by reading registry.
        /// </summary>
        /// <returns>The current Focus Assist state.</returns>
        public FocusAssistState GetCurrentState()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(NotificationsSettingsPath);
                if (key != null)
                {
                    var value = key.GetValue(ToastsEnabledKey);
                    if (value is int intValue && intValue == 0)
                    {
                        return FocusAssistState.PriorityOnly;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to read notification registry: {ex.Message}");
            }

            return FocusAssistState.Off;
        }

        /// <summary>
        /// Gets whether DND is currently enabled.
        /// </summary>
        public bool IsDndEnabled => GetCurrentState() != FocusAssistState.Off;

        /// <summary>
        /// Enables Focus Assist by disabling toast notifications via registry.
        /// </summary>
        /// <param name="endTime">Optional end time (not used for registry approach).</param>
        /// <returns>True if successful.</returns>
        public bool EnableFocusAssist(DateTimeOffset? endTime = null)
        {
            Logger.LogInfo($"Enabling Focus Assist via registry... EndTime: {endTime}");

            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(NotificationsSettingsPath))
                {
                    if (key != null)
                    {
                        key.SetValue(ToastsEnabledKey, 0, RegistryValueKind.DWord);
                        Logger.LogInfo("Toast notifications disabled via registry (NOC_GLOBAL_SETTING_TOASTS_ENABLED = 0)");
                    }
                }

                // Broadcast settings change to notify Windows
                BroadcastSettingsChange();

                // Notify state change
                FocusStateChanged?.Invoke(this, true);
                Logger.LogInfo("DND enabled");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to enable Focus Assist: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disables Focus Assist by enabling toast notifications via registry.
        /// </summary>
        /// <returns>True if successful.</returns>
        public bool DisableFocusAssist()
        {
            Logger.LogInfo("Disabling Focus Assist via registry...");

            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(NotificationsSettingsPath))
                {
                    if (key != null)
                    {
                        key.SetValue(ToastsEnabledKey, 1, RegistryValueKind.DWord);
                        Logger.LogInfo("Toast notifications enabled via registry (NOC_GLOBAL_SETTING_TOASTS_ENABLED = 1)");
                    }
                }

                // Broadcast settings change to notify Windows
                BroadcastSettingsChange();

                // Notify state change
                FocusStateChanged?.Invoke(this, false);
                Logger.LogInfo("DND disabled");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to disable Focus Assist: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Broadcasts a WM_SETTINGCHANGE message to notify Windows of the change.
        /// </summary>
        private void BroadcastSettingsChange()
        {
            try
            {
                SendMessageTimeout(
                    (IntPtr)HwndBroadcast,
                    WmSettingchange,
                    UIntPtr.Zero,
                    "ImmersiveColorSet",
                    0x0002, // SMTO_ABORTIFHUNG
                    1000,
                    out _);

                Logger.LogInfo("Broadcast WM_SETTINGCHANGE sent");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to broadcast settings change: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggles Focus Assist.
        /// </summary>
        /// <returns>True if successful.</returns>
        public bool ToggleFocusAssist()
        {
            return IsDndEnabled ? DisableFocusAssist() : EnableFocusAssist();
        }

        /// <summary>
        /// Opens the Windows Focus Settings.
        /// </summary>
        public void OpenFocusSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:quiethours",
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to open Focus Settings: {ex.Message}");
            }
        }
    }
}
