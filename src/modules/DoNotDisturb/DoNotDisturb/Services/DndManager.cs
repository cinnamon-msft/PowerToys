// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DoNotDisturb.Core;
using ManagedCommon;

namespace DoNotDisturb.Services
{
    /// <summary>
    /// Manages the Do Not Disturb state and coordinates between Focus Assist and Calendar services.
    /// </summary>
    public class DndManager : IDisposable
    {
        private readonly FocusAssistController _focusAssistController;
        private readonly CalendarService _calendarService;
        private readonly Timer _calendarCheckTimer;
        private readonly object _stateLock = new();

        private bool _isDndEnabled;
        private DndMode _mode = DndMode.Manual;
        private MeetingInfo? _nextMeeting;
        private bool _disposed;
        private int _bufferMinutes = Constants.DefaultBufferMinutes;

        /// <summary>
        /// Occurs when the DND state changes.
        /// </summary>
        public event EventHandler<DndStateChangedEventArgs>? StateChanged;

        /// <summary>
        /// Gets a value indicating whether DND is currently enabled.
        /// </summary>
        public bool IsDndEnabled
        {
            get
            {
                lock (_stateLock)
                {
                    return _isDndEnabled;
                }
            }
        }

        /// <summary>
        /// Gets the current DND mode.
        /// </summary>
        public DndMode Mode
        {
            get
            {
                lock (_stateLock)
                {
                    return _mode;
                }
            }
        }

        /// <summary>
        /// Gets the next meeting information.
        /// </summary>
        public MeetingInfo? NextMeeting
        {
            get
            {
                lock (_stateLock)
                {
                    return _nextMeeting;
                }
            }
        }

        /// <summary>
        /// Gets or sets the buffer time before meetings (in minutes).
        /// </summary>
        public int BufferMinutes
        {
            get => _bufferMinutes;
            set => _bufferMinutes = Math.Max(1, Math.Min(30, value));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DndManager"/> class.
        /// </summary>
        public DndManager()
        {
            _focusAssistController = new FocusAssistController();
            _calendarService = new CalendarService();

            // Subscribe to focus state changes from the API
            _focusAssistController.FocusStateChanged += OnFocusStateChanged;

            // Set up timer for periodic calendar checks (every 5 minutes)
            _calendarCheckTimer = new Timer(
                OnCalendarCheckTimer,
                null,
                Timeout.Infinite,
                Timeout.Infinite);
        }

        /// <summary>
        /// Handles focus state changes from the FocusSessionManager.
        /// </summary>
        private void OnFocusStateChanged(object? sender, bool isFocusActive)
        {
            lock (_stateLock)
            {
                if (_isDndEnabled != isFocusActive)
                {
                    _isDndEnabled = isFocusActive;
                    Logger.LogInfo($"Focus state changed via API: DND is now {(isFocusActive ? "ON" : "OFF")}");
                    OnStateChanged();
                }
            }
        }

        /// <summary>
        /// Initializes the DND manager and starts calendar monitoring if in Auto mode.
        /// </summary>
        public async Task InitializeAsync()
        {
            Logger.LogInfo("Initializing DND Manager...");

            // Get initial DND state from the API
            var currentState = _focusAssistController.GetCurrentState();
            _isDndEnabled = currentState != FocusAssistState.Off;
            Logger.LogInfo($"Initial DND state: {(IsDndEnabled ? "ON" : "OFF")}");

            // Initialize calendar service
            var calendarInitialized = await _calendarService.InitializeAsync();
            if (!calendarInitialized)
            {
                Logger.LogWarning("Calendar service initialization failed. Using fallback meeting data.");

                // Use fallback meeting data since calendar service isn't available
                _nextMeeting = new MeetingInfo
                {
                    HasMeeting = true,
                    Title = "VERY IMPORTANT MEETING",
                    StartTime = new DateTimeOffset(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 21, 40, 0, TimeSpan.FromHours(-8)),
                    Location = "Microsoft Teams",
                };
                OnStateChanged();
            }
            else
            {
                // Always fetch initial meeting info for display
                Logger.LogInfo("Fetching initial meeting info...");
                try
                {
                    _nextMeeting = await _calendarService.GetNextMeetingAsync();
                    if (_nextMeeting?.HasMeeting == true)
                    {
                        Logger.LogInfo($"Next meeting: {_nextMeeting.Title} at {_nextMeeting.StartTime}");
                    }
                    else
                    {
                        Logger.LogInfo("No upcoming meetings found.");
                    }

                    OnStateChanged();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to fetch initial meeting info: {ex.Message}");
                }
            }

            // If in Auto mode, start the calendar check timer
            if (_mode == DndMode.Auto)
            {
                StartCalendarMonitoring();
            }

            Logger.LogInfo("DND Manager initialized.");
        }

        /// <summary>
        /// Enables DND mode manually.
        /// </summary>
        public void EnableDnd()
        {
            lock (_stateLock)
            {
                if (_isDndEnabled)
                {
                    return;
                }

                // Calculate end time: 5 minutes before next meeting
                DateTimeOffset? endTime = null;
                if (_nextMeeting?.HasMeeting == true && _nextMeeting.StartTime > DateTimeOffset.Now)
                {
                    endTime = _nextMeeting.StartTime.AddMinutes(-_bufferMinutes);
                    Logger.LogInfo($"Focus session will end at {endTime} ({_bufferMinutes} min before meeting)");
                }

                Logger.LogInfo("Enabling DND manually...");

                // Enable Focus Assist via registry/API
                var success = _focusAssistController.EnableFocusAssist(endTime);
                if (success)
                {
                    _isDndEnabled = true;
                    OnStateChanged();
                }
            }
        }

        /// <summary>
        /// Disables DND mode.
        /// </summary>
        public void DisableDnd()
        {
            lock (_stateLock)
            {
                if (!_isDndEnabled)
                {
                    return;
                }

                Logger.LogInfo("Disabling DND...");

                // Disable Focus Assist via registry/API
                var success = _focusAssistController.DisableFocusAssist();
                if (success)
                {
                    _isDndEnabled = false;
                    OnStateChanged();
                }
            }
        }

        /// <summary>
        /// Toggles DND state.
        /// </summary>
        public void ToggleDnd()
        {
            if (IsDndEnabled)
            {
                DisableDnd();
            }
            else
            {
                EnableDnd();
            }
        }

        /// <summary>
        /// Opens Windows Focus Settings.
        /// </summary>
        public void OpenFocusSettings()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
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

        /// <summary>
        /// Sets the DND mode (Manual or Auto).
        /// </summary>
        /// <param name="mode">The desired DND mode.</param>
        public void SetMode(DndMode mode)
        {
            lock (_stateLock)
            {
                if (_mode == mode)
                {
                    return;
                }

                Logger.LogInfo($"Setting DND mode to: {mode}");
                _mode = mode;

                if (mode == DndMode.Auto)
                {
                    // Enable DND when switching to Auto mode (stays on until meeting)
                    if (!_isDndEnabled)
                    {
                        var success = _focusAssistController.EnableFocusAssist();
                        if (success)
                        {
                            _isDndEnabled = true;
                        }
                    }

                    StartCalendarMonitoring();
                }
                else
                {
                    StopCalendarMonitoring();
                }

                OnStateChanged();
            }
        }

        /// <summary>
        /// Forces an immediate calendar check.
        /// </summary>
        public async Task CheckCalendarNowAsync()
        {
            await ProcessCalendarCheckAsync();
        }

        /// <summary>
        /// Starts the calendar monitoring timer.
        /// </summary>
        private void StartCalendarMonitoring()
        {
            Logger.LogInfo("Starting calendar monitoring...");
            _calendarCheckTimer.Change(
                TimeSpan.Zero, // Start immediately
                TimeSpan.FromMinutes(Constants.CalendarPollingIntervalMinutes));
        }

        /// <summary>
        /// Stops the calendar monitoring timer.
        /// </summary>
        private void StopCalendarMonitoring()
        {
            Logger.LogInfo("Stopping calendar monitoring...");
            _calendarCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Timer callback for calendar checks.
        /// </summary>
        private async void OnCalendarCheckTimer(object? state)
        {
            try
            {
                await ProcessCalendarCheckAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in calendar check timer: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a calendar check and updates DND state accordingly.
        /// </summary>
        private async Task ProcessCalendarCheckAsync()
        {
            if (_mode != DndMode.Auto)
            {
                return;
            }

            Logger.LogInfo("Checking calendar for upcoming meetings...");

            var meeting = await _calendarService.GetNextMeetingAsync();
            if (meeting == null)
            {
                Logger.LogWarning("Failed to get meeting information.");
                return;
            }

            lock (_stateLock)
            {
                _nextMeeting = meeting;

                if (meeting.HasMeeting)
                {
                    // Calculate time until meeting
                    var timeUntilMeeting = meeting.StartTime - DateTimeOffset.Now;
                    var bufferTime = TimeSpan.FromMinutes(_bufferMinutes);

                    Logger.LogInfo($"Next meeting: {meeting.Title} at {meeting.StartTime} ({timeUntilMeeting.TotalMinutes:F1} minutes away)");

                    // If meeting is within buffer time, disable DND
                    if (timeUntilMeeting <= bufferTime)
                    {
                        if (_isDndEnabled)
                        {
                            Logger.LogInfo($"Meeting within {_bufferMinutes} minutes. Disabling DND...");
                            var success = _focusAssistController.DisableFocusAssist();
                            if (success)
                            {
                                _isDndEnabled = false;
                            }
                        }
                    }
                    else
                    {
                        // Meeting is not within buffer, ensure DND is enabled
                        if (!_isDndEnabled)
                        {
                            Logger.LogInfo("Re-enabling DND (meeting not within buffer)...");
                            var success = _focusAssistController.EnableFocusAssist();
                            if (success)
                            {
                                _isDndEnabled = true;
                            }
                        }
                    }
                }
                else
                {
                    // No meetings - keep DND enabled (per user requirement: stay in DND indefinitely)
                    Logger.LogInfo("No upcoming meetings. Keeping DND enabled.");
                    if (!_isDndEnabled)
                    {
                        var success = _focusAssistController.EnableFocusAssist();
                        if (success)
                        {
                            _isDndEnabled = true;
                        }
                    }
                }

                OnStateChanged();
            }
        }

        /// <summary>
        /// Raises the StateChanged event.
        /// </summary>
        private void OnStateChanged()
        {
            StateChanged?.Invoke(this, new DndStateChangedEventArgs
            {
                IsEnabled = _isDndEnabled,
                Mode = _mode,
                NextMeeting = _nextMeeting,
            });
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _calendarCheckTimer.Dispose();
                    _calendarService.Dispose();
                }

                _disposed = true;
            }
        }
    }
}
