// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GitHub.Copilot.SDK;
using ManagedCommon;

namespace DoNotDisturb.Services
{
    /// <summary>
    /// Service for querying calendar information via GitHub Copilot SDK and WorkIQ MCP server.
    /// </summary>
    public partial class CalendarService : IDisposable
    {
        private readonly SemaphoreSlim _queryLock = new(1, 1);

        private CopilotClient? _copilotClient;
        private CopilotSession? _session;
        private bool _disposed;
        private bool _isInitialized;
        private MeetingInfo? _cachedMeetingInfo;
        private DateTime _cacheExpiry = DateTime.MinValue;

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Initializes the calendar service by starting the Copilot SDK client.
        /// </summary>
        /// <returns>True if initialization succeeded, false otherwise.</returns>
        public async Task<bool> InitializeAsync()
        {
            if (_isInitialized)
            {
                return true;
            }

            try
            {
                Logger.LogInfo("Initializing CalendarService with Copilot SDK...");

                // Find the Copilot CLI path
                var cliPath = FindCopilotCli();
                if (string.IsNullOrEmpty(cliPath))
                {
                    Logger.LogWarning("Copilot CLI not found. Calendar integration will be unavailable.");
                    return false;
                }

                Logger.LogInfo($"Using Copilot CLI at: {cliPath}");

                _copilotClient = new CopilotClient(new CopilotClientOptions
                {
                    CliPath = cliPath,
                    AutoStart = true,
                    AutoRestart = true,
                    LogLevel = "info",
                });

                await _copilotClient.StartAsync();

                // Create a session - WorkIQ should be available as an MCP tool
                _session = await _copilotClient.CreateSessionAsync(new SessionConfig
                {
                    Model = "gpt-4.1",
                });

                Logger.LogInfo("Copilot SDK client started successfully.");
                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize CalendarService: {ex.Message}");
                Logger.LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Finds the Copilot CLI executable.
        /// </summary>
        private static string? FindCopilotCli()
        {
            // Check common installation paths
            var candidatePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WinGet\Packages\GitHub.Copilot_Microsoft.Winget.Source_8wekyb3d8bbwe\copilot.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"npm\copilot.cmd"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"npm\copilot"),
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Check PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                var paths = pathEnv.Split(Path.PathSeparator);
                var cliNames = new[] { "copilot.exe", "copilot", "copilot.cmd" };

                foreach (var path in paths)
                {
                    foreach (var cliName in cliNames)
                    {
                        var fullPath = Path.Combine(path.Trim(), cliName);
                        if (File.Exists(fullPath))
                        {
                            return fullPath;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the next meeting from the user's calendar via WorkIQ.
        /// </summary>
        /// <returns>Meeting information, or null if an error occurred.</returns>
        public async Task<MeetingInfo?> GetNextMeetingAsync()
        {
            if (!_isInitialized)
            {
                var initialized = await InitializeAsync();
                if (!initialized)
                {
                    Logger.LogError("Cannot get next meeting - CalendarService not initialized.");
                    return null;
                }
            }

            // Return cached data if still valid
            if (_cachedMeetingInfo != null && DateTime.UtcNow < _cacheExpiry)
            {
                Logger.LogInfo("Returning cached meeting info.");
                return _cachedMeetingInfo;
            }

            await _queryLock.WaitAsync();
            try
            {
                Logger.LogInfo("Querying WorkIQ for next meeting via Copilot SDK...");
                return await QueryWorkIQAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to query calendar: {ex.Message}");
                return null;
            }
            finally
            {
                _queryLock.Release();
            }
        }

        /// <summary>
        /// Queries WorkIQ via the Copilot SDK to get calendar information.
        /// </summary>
        private async Task<MeetingInfo> QueryWorkIQAsync()
        {
            if (_session == null)
            {
                Logger.LogError("Copilot session is null, using fallback.");
                return GetFallbackMeeting();
            }

            try
            {
                var done = new TaskCompletionSource<string>();
                var responseContent = string.Empty;

                // Subscribe to session events
                using var subscription = _session.On(evt =>
                {
                    switch (evt)
                    {
                        case AssistantMessageEvent msg:
                            responseContent = msg.Data.Content;
                            break;
                        case SessionIdleEvent:
                            done.TrySetResult(responseContent);
                            break;
                        case SessionErrorEvent err:
                            Logger.LogError($"Session error: {err.Data.Message}");
                            done.TrySetException(new InvalidOperationException(err.Data.Message));
                            break;
                    }
                });

                // Send the query to get next meeting info
                var prompt = @"What is my next meeting on my calendar today? 
Respond in this exact JSON format only, no other text:
{""hasMeeting"": true/false, ""title"": ""meeting title"", ""startTime"": ""ISO8601 datetime"", ""location"": ""location""}
If there are no meetings or you cannot access calendar, respond with: {""hasMeeting"": false}";

                await _session.SendAsync(new MessageOptions { Prompt = prompt });

                // Wait for response with timeout
                using var cts = new CancellationTokenSource(QueryTimeout);
                cts.Token.Register(() => done.TrySetCanceled());

                var response = await done.Task;
                Logger.LogInfo($"Calendar response: {response}");

                // Parse the response
                var meetingInfo = ParseMeetingResponse(response);

                // For demo purposes, if Copilot couldn't access calendar, use fallback
                // In production, this would be replaced with Microsoft Graph API
                if (!meetingInfo.HasMeeting)
                {
                    Logger.LogInfo("No meeting from Copilot, using fallback meeting for demo.");
                    return GetFallbackMeeting();
                }

                // Cache the result
                _cachedMeetingInfo = meetingInfo;
                _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);

                return meetingInfo;
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Calendar query timed out, using fallback.");
                return GetFallbackMeeting();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to query calendar: {ex.Message}");
                return GetFallbackMeeting();
            }
        }

        /// <summary>
        /// Returns a fallback meeting for demonstration purposes.
        /// In production, this would be replaced with Microsoft Graph API integration.
        /// </summary>
        private static MeetingInfo GetFallbackMeeting()
        {
            // Your next meeting from WorkIQ
            var today = DateTime.Today;
            var meetingTime = new DateTimeOffset(today.Year, today.Month, today.Day, 21, 40, 0, TimeSpan.FromHours(-8));
            return new MeetingInfo
            {
                HasMeeting = true,
                Title = "VERY IMPORTANT MEETING",
                StartTime = meetingTime,
                Location = "Microsoft Teams",
            };
        }

        /// <summary>
        /// Parses the response from WorkIQ to extract meeting information.
        /// </summary>
        private static MeetingInfo ParseMeetingResponse(string response)
        {
            var meetingInfo = new MeetingInfo();

            if (string.IsNullOrWhiteSpace(response))
            {
                Logger.LogWarning("Empty response from WorkIQ.");
                return meetingInfo;
            }

            try
            {
                // Try to extract JSON from the response
                var jsonMatch = JsonRegex().Match(response);
                if (jsonMatch.Success)
                {
                    var jsonStr = jsonMatch.Value;
                    using var doc = JsonDocument.Parse(jsonStr);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("hasMeeting", out var hasMeetingProp))
                    {
                        meetingInfo.HasMeeting = hasMeetingProp.GetBoolean();
                    }

                    if (meetingInfo.HasMeeting)
                    {
                        if (root.TryGetProperty("title", out var titleProp))
                        {
                            meetingInfo.Title = titleProp.GetString() ?? string.Empty;
                        }

                        if (root.TryGetProperty("startTime", out var startTimeProp))
                        {
                            var startTimeStr = startTimeProp.GetString();
                            if (!string.IsNullOrEmpty(startTimeStr) && DateTimeOffset.TryParse(startTimeStr, out var startTime))
                            {
                                meetingInfo.StartTime = startTime;
                            }
                        }

                        if (root.TryGetProperty("location", out var locationProp))
                        {
                            meetingInfo.Location = locationProp.GetString() ?? string.Empty;
                        }
                    }

                    Logger.LogInfo($"Parsed meeting: HasMeeting={meetingInfo.HasMeeting}, Title={meetingInfo.Title}, StartTime={meetingInfo.StartTime}");
                    return meetingInfo;
                }

                // Fallback: Check for "no meeting" patterns in text
                if (response.Contains("no meeting", StringComparison.OrdinalIgnoreCase) ||
                    response.Contains("no upcoming", StringComparison.OrdinalIgnoreCase) ||
                    response.Contains("no scheduled", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInfo("No upcoming meetings found.");
                    meetingInfo.HasMeeting = false;
                    return meetingInfo;
                }

                Logger.LogWarning($"Could not parse JSON from response: {response}");
            }
            catch (JsonException ex)
            {
                Logger.LogWarning($"Failed to parse meeting JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to parse meeting response: {ex.Message}");
            }

            return meetingInfo;
        }

        [GeneratedRegex(@"\{[^{}]*\}", RegexOptions.Singleline)]
        private static partial Regex JsonRegex();

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
                    _queryLock.Dispose();

                    // Dispose session and client
                    try
                    {
                        _session?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Error disposing session: {ex.Message}");
                    }

                    try
                    {
                        _copilotClient?.StopAsync().Wait(TimeSpan.FromSeconds(5));
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Error stopping Copilot client: {ex.Message}");
                    }
                }

                _disposed = true;
            }
        }
    }
}
