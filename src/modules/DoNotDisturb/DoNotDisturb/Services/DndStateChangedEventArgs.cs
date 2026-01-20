// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace DoNotDisturb.Services
{
    /// <summary>
    /// Event arguments for DND state changes.
    /// </summary>
    public class DndStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a value indicating whether DND is currently enabled.
        /// </summary>
        public bool IsEnabled { get; init; }

        /// <summary>
        /// Gets the current DND mode.
        /// </summary>
        public DndMode Mode { get; init; }

        /// <summary>
        /// Gets the next meeting information (if available).
        /// </summary>
        public MeetingInfo? NextMeeting { get; init; }
    }
}
