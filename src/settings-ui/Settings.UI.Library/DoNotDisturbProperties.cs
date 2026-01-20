// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Properties for Do Not Disturb settings.
    /// </summary>
    public class DoNotDisturbProperties
    {
        public const bool DefaultAutoMode = false;
        public const int DefaultBufferMinutes = 5;

        public DoNotDisturbProperties()
        {
            AutoMode = new BoolProperty(DefaultAutoMode);
            BufferMinutes = new IntProperty(DefaultBufferMinutes);
        }

        /// <summary>
        /// Gets or sets whether Auto Mode is enabled (calendar-based DND).
        /// </summary>
        [JsonPropertyName("auto-mode")]
        public BoolProperty AutoMode { get; set; }

        /// <summary>
        /// Gets or sets the buffer time before meetings (in minutes) to disable DND.
        /// </summary>
        [JsonPropertyName("buffer-minutes")]
        public IntProperty BufferMinutes { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
