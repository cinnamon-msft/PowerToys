// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace DoNotDisturb.Services
{
    /// <summary>
    /// Represents meeting information retrieved from the calendar.
    /// </summary>
    public class MeetingInfo
    {
        /// <summary>
        /// Gets or sets the meeting title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the meeting start time.
        /// </summary>
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// Gets or sets the meeting location.
        /// </summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether a meeting was found.
        /// </summary>
        public bool HasMeeting { get; set; }

        /// <summary>
        /// Gets the time until the meeting starts.
        /// </summary>
        public TimeSpan TimeUntilMeeting => StartTime - DateTimeOffset.Now;

        /// <summary>
        /// Gets a value indicating whether the meeting is starting soon (within 5 minutes).
        /// </summary>
        public bool IsStartingSoon => HasMeeting && TimeUntilMeeting <= TimeSpan.FromMinutes(5) && TimeUntilMeeting > TimeSpan.Zero;
    }
}
