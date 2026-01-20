// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using DoNotDisturb.Core;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace DoNotDisturb
{
    internal sealed class Program
    {
        public static Mutex? LockMutex { get; set; }

        [STAThread]
        private static void Main(string[] args)
        {
            LockMutex = new Mutex(true, Constants.AppName, out bool instantiated);

            Logger.InitializeLogger(Path.Combine("\\", Constants.AppName, "Logs"));

            if (!instantiated)
            {
                Logger.LogInfo($"{Constants.AppName} is already running! Exiting the application.");
                return;
            }

            if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredDoNotDisturbEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                Logger.LogInfo("PowerToys.DoNotDisturb tried to start with a group policy setting that disables the tool.");
                return;
            }

            Logger.LogInfo($"Launching {Constants.AppName}...");
            Logger.LogInfo(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
            Logger.LogInfo($"Build: {Constants.BuildId}");
            Logger.LogInfo($"OS: {Environment.OSVersion}");

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                LockMutex?.ReleaseMutex();
            };

            ComWrappersSupport.InitializeComWrappers();
            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }
    }
}
