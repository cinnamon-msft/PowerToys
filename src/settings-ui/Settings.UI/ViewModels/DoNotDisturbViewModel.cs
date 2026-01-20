// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json;
using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class DoNotDisturbViewModel : PageViewModelBase
    {
        protected override string ModuleName => DoNotDisturbSettings.ModuleName;

        private SettingsUtils SettingsUtils { get; set; }

        private GeneralSettings GeneralSettingsConfig { get; set; }

        private DoNotDisturbSettings Settings { get; set; }

        private Func<string, int> SendConfigMSG { get; }

        private GpoRuleConfigured _enabledGpoRuleConfiguration;
        private bool _enabledStateIsGPOConfigured;
        private bool _isEnabled;
        private bool _autoMode;
        private int _bufferMinutes;

        public DoNotDisturbViewModel(SettingsUtils settingsUtils, ISettingsRepository<GeneralSettings> settingsRepository, ISettingsRepository<DoNotDisturbSettings> moduleSettingsRepository, Func<string, int> ipcMSGCallBackFunc)
        {
            ArgumentNullException.ThrowIfNull(settingsUtils);

            SettingsUtils = settingsUtils;

            // To obtain the general settings configurations of PowerToys Settings.
            ArgumentNullException.ThrowIfNull(settingsRepository);

            GeneralSettingsConfig = settingsRepository.SettingsConfig;

            InitializeEnabledValue();

            // To obtain the settings configurations of DoNotDisturb.
            ArgumentNullException.ThrowIfNull(moduleSettingsRepository);

            Settings = moduleSettingsRepository.SettingsConfig;

            _autoMode = Settings.Properties.AutoMode.Value;
            _bufferMinutes = Settings.Properties.BufferMinutes.Value;

            // set the callback functions value to handle outgoing IPC message.
            SendConfigMSG = ipcMSGCallBackFunc;
        }

        private void InitializeEnabledValue()
        {
            _enabledGpoRuleConfiguration = GPOWrapper.GetConfiguredDoNotDisturbEnabledValue();
            if (_enabledGpoRuleConfiguration == GpoRuleConfigured.Disabled || _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled)
            {
                // Get the enabled state from GPO.
                _enabledStateIsGPOConfigured = true;
                _isEnabled = _enabledGpoRuleConfiguration == GpoRuleConfigured.Enabled;
            }
            else
            {
                _isEnabled = GeneralSettingsConfig.Enabled.DoNotDisturb;
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;

            set
            {
                if (_enabledStateIsGPOConfigured)
                {
                    // If it's GPO configured, shouldn't be able to change this state.
                    return;
                }

                if (value != _isEnabled)
                {
                    _isEnabled = value;

                    // Set the status in the general settings configuration
                    GeneralSettingsConfig.Enabled.DoNotDisturb = value;
                    OutGoingGeneralSettings snd = new OutGoingGeneralSettings(GeneralSettingsConfig);

                    SendConfigMSG(snd.ToString());
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public bool IsEnabledGpoConfigured
        {
            get => _enabledStateIsGPOConfigured;
        }

        public bool AutoMode
        {
            get => _autoMode;

            set
            {
                if (value != _autoMode)
                {
                    _autoMode = value;
                    Settings.Properties.AutoMode.Value = value;
                    NotifyPropertyChanged();
                    NotifySettingsChanged();
                }
            }
        }

        public int BufferMinutes
        {
            get => _bufferMinutes;

            set
            {
                if (value != _bufferMinutes)
                {
                    _bufferMinutes = Math.Max(1, Math.Min(30, value));
                    Settings.Properties.BufferMinutes.Value = _bufferMinutes;
                    NotifyPropertyChanged();
                    NotifySettingsChanged();
                }
            }
        }

        private void NotifySettingsChanged()
        {
            // Using InvariantCulture as this is an IPC message
            SendConfigMSG(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{ \"powertoys\": {{ \"{0}\": {1} }} }}",
                    DoNotDisturbSettings.ModuleName,
                    JsonSerializer.Serialize(Settings)));
        }
    }
}
