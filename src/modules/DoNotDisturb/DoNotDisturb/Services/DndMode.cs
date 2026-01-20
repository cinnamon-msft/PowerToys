// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace DoNotDisturb.Services
{
    /// <summary>
    /// Represents the current DND mode.
    /// </summary>
    public enum DndMode
    {
        /// <summary>
        /// DND is controlled manually by the user.
        /// </summary>
        Manual,

        /// <summary>
        /// DND is automatically managed based on calendar.
        /// </summary>
        Auto,
    }
}
