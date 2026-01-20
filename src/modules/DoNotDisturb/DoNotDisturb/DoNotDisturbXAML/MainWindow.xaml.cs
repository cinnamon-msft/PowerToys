// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using DoNotDisturb.Services;
using ManagedCommon;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinUIEx;

namespace DoNotDisturb
{
    /// <summary>
    /// Main window for Do Not Disturb utility.
    /// </summary>
    public sealed partial class MainWindow : WindowEx
    {
        private readonly DndManager _dndManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        /// <param name="dndManager">The DND manager instance.</param>
        public MainWindow(DndManager dndManager)
        {
            _dndManager = dndManager ?? throw new ArgumentNullException(nameof(dndManager));

            InitializeComponent();

            AppWindow.SetIcon("Assets/DoNotDisturb/DoNotDisturb.ico");

            // Subscribe to state changes
            _dndManager.StateChanged += OnDndStateChanged;

            // Initialize UI state
            UpdateUI();
        }

        private void OnDndStateChanged(object? sender, DndStateChangedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(UpdateUI);
        }

        private void UpdateUI()
        {
            bool isDndEnabled = _dndManager.IsDndEnabled;
            DndMode currentMode = _dndManager.Mode;
            MeetingInfo? nextMeeting = _dndManager.NextMeeting;

            // Update status indicator color
            if (isDndEnabled)
            {
                StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                StatusText.Text = "Focus Mode: On";
                ToggleButton.Content = "Disable Do Not Disturb";
            }
            else
            {
                StatusIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
                StatusText.Text = "Focus Mode: Off";
                ToggleButton.Content = "Enable Do Not Disturb";
            }

            // Update auto mode toggle
            AutoModeToggle.IsOn = currentMode == DndMode.Auto;

            // Update next meeting info
            if (nextMeeting != null && nextMeeting.HasMeeting)
            {
                NextMeetingText.Text = nextMeeting.Title;
                NextMeetingTime.Text = nextMeeting.StartTime.ToString("h:mm tt", System.Globalization.CultureInfo.CurrentCulture);
            }
            else
            {
                NextMeetingText.Text = "No upcoming meetings";
                NextMeetingTime.Text = string.Empty;
            }
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _dndManager.ToggleDnd();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to toggle DND", ex);
            }
        }

        private void AutoModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                _dndManager.SetMode(AutoModeToggle.IsOn ? DndMode.Auto : DndMode.Manual);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to set mode", ex);
            }
        }
    }
}
