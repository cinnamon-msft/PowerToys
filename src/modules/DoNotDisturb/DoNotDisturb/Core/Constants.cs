// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace DoNotDisturb.Core
{
    internal static class Constants
    {
        public const string AppName = "DoNotDisturb";
        public const string FullAppName = "PowerToys " + AppName;
        public const string BuildId = "2024.01";

        // Exit event name for inter-process communication
        public const string ExitEventName = "Local\\PowerToysDoNotDisturbExitEvent-7f3b8a2c-4d5e-6f7a-8b9c-0d1e2f3a4b5c";

        // Default buffer time before meetings (in minutes)
        public const int DefaultBufferMinutes = 5;

        // Calendar polling interval (in minutes)
        public const int CalendarPollingIntervalMinutes = 5;
    }
}
