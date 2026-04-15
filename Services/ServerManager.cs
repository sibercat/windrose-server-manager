using System.Diagnostics;

namespace WindroseServerManager.Services;

public class ServerManager
{
    private readonly string _serverFilesDir;
    private readonly FileLogger _logger;

    // WindroseServer.exe (wrapper) launches WindroseServer-Win64-Shipping.exe as a child.
    // We track the shipping process for crash detection and graceful stop.
    private Process? _wrapperProcess;
    private ServerState _state = ServerState.NotInstalled;
    private bool _isStopping;
    private int  _restartAttempts;
    private ServerConfiguration? _lastConfig;
    private string? _lastExePath;

    private bool _crashDetectionEnabled = true;
    private bool _autoRestart           = true;
    private int  _maxRestartAttempts    = 3;

    // ── Events ───────────────────────────────────────────────────────
    public event EventHandler<ServerState>? StateChanged;
    public event EventHandler<string>?      OutputReceived;
    public event EventHandler?              CrashDetected;
    public event EventHandler?              AutoRestarted;

    // ── Properties ───────────────────────────────────────────────────
    public ServerState State    => _state;
    public bool        IsRunning => _state == ServerState.Running;

    public ServerManager(string rootDir, FileLogger logger)
    {
        _serverFilesDir = Path.Combine(rootDir, "ServerFiles");
        _logger         = logger;
    }

    private string ServerExePath => Path.Combine(_serverFilesDir, "WindroseServer.exe");
    private bool   IsInstalled   => File.Exists(ServerExePath);

    // ── Initialization ───────────────────────────────────────────────

    public void InitializeState()
    {
        // Check if the shipping process is already running from this install
        var shipping = FindShippingProcess();
        if (shipping != null)
        {
            _logger.Info($"Attached to existing server process (PID {shipping.Id}).");
            shipping.EnableRaisingEvents = true;
            shipping.Exited += OnShippingExited;
            SetState(ServerState.Running);
            return;
        }

        SetState(IsInstalled ? ServerState.Stopped : ServerState.NotInstalled);
    }

    // ── Start ────────────────────────────────────────────────────────

    public void Start(ServerConfiguration cfg)
    {
        if (_state is ServerState.Running or ServerState.Starting) return;

        _lastConfig      = cfg;
        _lastExePath     = ServerExePath;
        _isStopping      = false;
        _restartAttempts = 0;

        ConfigureCrashDetection(cfg.EnableCrashDetection, cfg.AutoRestart, cfg.MaxRestartAttempts);
        DoStart(cfg);
    }

    private void DoStart(ServerConfiguration cfg)
    {
        SetState(ServerState.Starting);

        // Build args — Windrose reads all settings from ServerDescription.json,
        // so the only arg needed is -log to keep a visible console window.
        string args = "-log";
        if (!string.IsNullOrWhiteSpace(cfg.CustomLaunchArgs))
            args += " " + cfg.CustomLaunchArgs.Trim();

        _logger.Info($"Starting server: {ServerExePath} {args}");
        Emit("Starting Windrose Dedicated Server...");
        Emit($"Args: {args}");

        var psi = new ProcessStartInfo
        {
            FileName         = ServerExePath,
            Arguments        = args,
            UseShellExecute  = true,                  // gives the server its own visible console
            WorkingDirectory = _serverFilesDir        // same as StartServerForeground.bat
        };

        _wrapperProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _wrapperProcess.Start();
        _logger.Info($"Wrapper process started (PID {_wrapperProcess.Id}).");

        ApplyPriority(cfg.ProcessPriority);

        // The wrapper (WindroseServer.exe) immediately spawns the shipping exe and may exit.
        // Poll for the shipping process, then attach crash detection to it.
        _ = WatchForShippingProcessAsync();
    }

    // ── Shipping Process Watcher ─────────────────────────────────────

    private async Task WatchForShippingProcessAsync()
    {
        // Wait up to 60 s for the shipping process to appear
        Process? shipping = null;
        for (int i = 0; i < 60; i++)
        {
            await Task.Delay(1000);
            shipping = FindShippingProcess();
            if (shipping != null) break;

            // If wrapper already exited and shipping never appeared — launch failed
            if (_wrapperProcess is { HasExited: true } && shipping == null && i > 5)
            {
                Emit("Server process failed to start (wrapper exited before shipping process appeared).");
                _logger.Warning("WindroseServer.exe exited before shipping process was found.");
                SetState(ServerState.Stopped);
                return;
            }
        }

        if (shipping == null)
        {
            Emit("Shipping process not found after 60 s — marking as Running anyway.");
            _logger.Warning("Shipping process not detected; assuming server is Running.");
            SetState(ServerState.Running);
            return;
        }

        _logger.Info($"Shipping process found (PID {shipping.Id}).");
        Emit("Server is loading...");

        shipping.EnableRaisingEvents = true;
        shipping.Exited += OnShippingExited;

        ApplyPriorityToProcess(shipping, _lastConfig?.ProcessPriority ?? "Normal");

        // Give it another few seconds to finish initialization
        await Task.Delay(3000);
        if (_state == ServerState.Starting)
            SetState(ServerState.Running);
    }

    // ── Stop ─────────────────────────────────────────────────────────

    public async Task StopAsync()
    {
        if (_state is ServerState.Stopped or ServerState.NotInstalled) return;

        _isStopping = true;
        SetState(ServerState.Stopping);
        Emit("Stopping server...");

        // Try graceful Ctrl+C on the shipping process first
        var shipping = FindShippingProcess();
        if (shipping != null)
        {
            Emit("Sending graceful shutdown signal to server...");
            SendCtrlC(shipping.Id);
            await Task.Run(() => shipping.WaitForExit(10_000));
        }

        // Force-kill shipping if still alive
        if (shipping != null && !shipping.HasExited)
            await KillProcessAsync(shipping);

        // Force-kill wrapper if still alive
        await KillProcessAsync(_wrapperProcess);

        SetState(ServerState.Stopped);
        Emit("Server stopped.");
        _logger.Info("Server stopped by user.");
    }

    public async Task RestartAsync(ServerConfiguration cfg)
    {
        await StopAsync();
        await Task.Delay(2000);
        DoStart(cfg);
    }

    // ── Crash Detection ──────────────────────────────────────────────

    public void ConfigureCrashDetection(bool enabled, bool autoRestart, int maxAttempts)
    {
        _crashDetectionEnabled = enabled;
        _autoRestart           = autoRestart;
        _maxRestartAttempts    = maxAttempts;
    }

    private void OnShippingExited(object? sender, EventArgs e)
    {
        if (_isStopping) return;

        _logger.Warning("Server shipping process exited unexpectedly.");
        Emit("Server process exited unexpectedly!");

        if (_crashDetectionEnabled)
        {
            CrashDetected?.Invoke(this, EventArgs.Empty);
            SetState(ServerState.Crashed);

            if (_autoRestart && _lastConfig != null && _restartAttempts < _maxRestartAttempts)
            {
                _restartAttempts++;
                Emit($"Auto-restarting... attempt {_restartAttempts}/{_maxRestartAttempts}");
                _logger.Info($"Auto-restart attempt {_restartAttempts}/{_maxRestartAttempts}");
                Thread.Sleep(5000);
                DoStart(_lastConfig);
                AutoRestarted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Emit("Max restart attempts reached or auto-restart disabled.");
                SetState(ServerState.Stopped);
            }
        }
        else
        {
            SetState(ServerState.Stopped);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>Finds WindroseServer-Win64-Shipping.exe running from this install.</summary>
    private Process? FindShippingProcess()
    {
        string expectedDir = Path.GetFullPath(_serverFilesDir);
        foreach (var proc in Process.GetProcessesByName("WindroseServer-Win64-Shipping"))
        {
            try
            {
                string exeDir = Path.GetFullPath(Path.GetDirectoryName(proc.MainModule!.FileName)!);
                // The shipping exe lives in R5\Binaries\Win64\ under our ServerFiles dir
                if (exeDir.StartsWith(expectedDir, StringComparison.OrdinalIgnoreCase))
                    return proc;
            }
            catch { /* access denied or already exited */ }
        }
        return null;
    }

    private void ApplyPriority(string priority)
    {
        if (_wrapperProcess == null || _wrapperProcess.HasExited) return;
        try { ApplyPriorityToProcess(_wrapperProcess, priority); }
        catch { }
    }

    private static void ApplyPriorityToProcess(Process proc, string priority)
    {
        try
        {
            proc.PriorityClass = priority switch
            {
                "AboveNormal" => ProcessPriorityClass.AboveNormal,
                "High"        => ProcessPriorityClass.High,
                _             => ProcessPriorityClass.Normal
            };
        }
        catch { }
    }

    private static async Task KillProcessAsync(Process? proc)
    {
        if (proc == null || proc.HasExited) return;
        try
        {
            proc.Kill(entireProcessTree: true);
            await Task.Run(() => proc.WaitForExit(5000));
        }
        catch { }
    }

    private static bool SendCtrlC(int pid)
    {
        try
        {
            NativeMethods.FreeConsole();
            if (!NativeMethods.AttachConsole(pid)) return false;
            NativeMethods.SetConsoleCtrlHandler(null, true);
            NativeMethods.GenerateConsoleCtrlEvent(NativeMethods.CTRL_C_EVENT, 0);
            Thread.Sleep(500);
            NativeMethods.FreeConsole();
            NativeMethods.SetConsoleCtrlHandler(null, false);
            return true;
        }
        catch { return false; }
    }

    private void SetState(ServerState s)
    {
        _state = s;
        StateChanged?.Invoke(this, s);
    }

    private void Emit(string msg) => OutputReceived?.Invoke(this, msg);
}
