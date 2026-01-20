// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Settings for Do Not Disturb module.
    /// </summary>
    public class DoNotDisturbSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "DoNotDisturb";
        public const string ModuleVersion = "1.0.0";

        public DoNotDisturbSettings()
        {
            Name = ModuleName;
            Version = ModuleVersion;
            Properties = new DoNotDisturbProperties();
        }

        [JsonPropertyName("properties")]
        public DoNotDisturbProperties Properties { get; set; }

        public string GetModuleName()
        {
            return Name;
        }

        public ModuleType GetModuleType() => ModuleType.DoNotDisturb;

        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
