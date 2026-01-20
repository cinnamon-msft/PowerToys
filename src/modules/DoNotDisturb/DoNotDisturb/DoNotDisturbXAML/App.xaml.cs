// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DoNotDisturb.Native;
using DoNotDisturb.Services;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinUIEx;

namespace DoNotDisturb
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private const int WmTrayCallback = NativeMethods.WmUser + 1;
        private const int WmCommand = 0x0111;

        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        private MainWindow? _window;
        private nint _windowHwnd;
        private bool _disposedValue;
        private DndManager? _dndManager;
        private TrayHelper? _trayHelper;

        // Window subclassing
        private IntPtr _originalWndProc;
        private NativeMethods.WndProcDelegate? _wndProcDelegate;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// </summary>
        public App()
        {
            InitializeComponent();
            UnhandledException += App_UnhandledException;
        }

        /// <summary>
        /// Gets the DndManager instance.
        /// </summary>
        public DndManager? DndManager => _dndManager;

        /// <summary>
        /// Gets the main window.
        /// </summary>
        /// <returns>The main window instance.</returns>
        public MainWindow? GetMainWindow() => _window;

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs?.Length > 1)
            {
                if (int.TryParse(cmdArgs[1], out int powerToysRunnerPid))
                {
                    RunnerHelper.WaitForPowerToysRunner(powerToysRunnerPid, () =>
                    {
                        _dispatcherQueue.TryEnqueue(() =>
                        {
                            Dispose();
                            Environment.Exit(0);
                        });
                    });
                }
            }

            // Initialize the DND manager
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                _dndManager = new DndManager();
                await _dndManager.InitializeAsync();

                // Create main window first (needed for tray icon window handle)
                _dispatcherQueue.TryEnqueue(() =>
                {
                    CreateWindow();
                    SubclassWindow();
                    InitializeTrayIcon();
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize DND manager", ex);
            }
        }

        private void CreateWindow()
        {
            if (_window is null)
            {
                _window = new MainWindow(_dndManager!);
                _windowHwnd = _window.GetWindowHandle();

                // Hook window messages for tray callback
                _window.Closed += OnWindowClosed;
            }
        }

        private void SubclassWindow()
        {
            if (_windowHwnd == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Keep a reference to the delegate to prevent GC
                _wndProcDelegate = new NativeMethods.WndProcDelegate(WndProc);
                IntPtr newWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
                _originalWndProc = NativeMethods.SetWindowLongPtr(_windowHwnd, NativeMethods.GwlpWndproc, newWndProc);

                if (_originalWndProc == IntPtr.Zero)
                {
                    Logger.LogError($"Failed to subclass window. Error: {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Logger.LogInfo("Window subclassed for tray messages");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to subclass window: {ex.Message}");
            }
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WmTrayCallback)
            {
                _trayHelper?.HandleCallback(lParam);
                return IntPtr.Zero;
            }

            if (msg == WmCommand)
            {
                int commandId = (int)(wParam.ToInt64() & 0xFFFF);
                _trayHelper?.HandleMenuCommand(commandId);
                return IntPtr.Zero;
            }

            // Call original window procedure
            return NativeMethods.CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
        }

        private void InitializeTrayIcon()
        {
            if (_dndManager == null || _windowHwnd == IntPtr.Zero)
            {
                return;
            }

            try
            {
                _trayHelper = new TrayHelper(
                    _dndManager,
                    _windowHwnd,
                    ShowWindow,
                    () => Exit());

                _trayHelper.Initialize();
                Logger.LogInfo("Tray icon initialized");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize tray icon: {ex.Message}");
            }
        }

        private void ShowWindow()
        {
            if (_window is null)
            {
                CreateWindow();
            }

            _window?.Activate();
            if (_windowHwnd != IntPtr.Zero)
            {
                WindowHelpers.BringToForeground(_windowHwnd);
            }
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            // Don't close when window is closed, just hide (minimize to tray)
            args.Handled = true;
            _window?.Hide();
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Logger.LogError("Unhandled exception", e.Exception);
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _trayHelper?.Dispose();
                    _dndManager?.Dispose();
                }

                _disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
