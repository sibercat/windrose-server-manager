using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;

namespace WindroseServerManager.Services;

public class SteamCmdService
{
    private const int    WINDROSE_APP_ID   = 4129620;
    private const string STEAMCMD_ZIP_URL  = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";

    private readonly string _steamCmdDir;
    private readonly string _steamCmdExe;
    private readonly string _serverFilesDir;
    private readonly FileLogger _logger;

    private static readonly HttpClient _http = new();

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<int>?    DownloadProgressChanged; // 0-100

    public bool IsSteamCmdInstalled => File.Exists(_steamCmdExe);

    /// <summary>Full path to the server wrapper executable.</summary>
    public string ServerExePath => Path.Combine(_serverFilesDir, "WindroseServer.exe");

    public bool IsServerInstalled => File.Exists(ServerExePath);

    public string ServerFilesDirectory => _serverFilesDir;

    public SteamCmdService(string rootDir, FileLogger logger)
    {
        _steamCmdDir    = Path.Combine(rootDir, "steamcmd");
        _steamCmdExe    = Path.Combine(_steamCmdDir, "steamcmd.exe");
        _serverFilesDir = Path.Combine(rootDir, "ServerFiles");
        _logger         = logger;
    }

    // ── SteamCMD Download ────────────────────────────────────────────

    public async Task DownloadSteamCmdAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_steamCmdDir);
        string zipPath = Path.Combine(_steamCmdDir, "steamcmd.zip");

        Emit("Downloading SteamCMD...");
        _logger.Info("Downloading SteamCMD from Valve CDN.");

        using var response = await _http.GetAsync(STEAMCMD_ZIP_URL,
            HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength;
        await using (var src  = await response.Content.ReadAsStreamAsync(ct))
        await using (var dest = File.Create(zipPath))
        {
            byte[] buf = new byte[81920];
            long  read = 0;
            int   n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dest.WriteAsync(buf.AsMemory(0, n), ct);
                read += n;
                if (total > 0)
                    DownloadProgressChanged?.Invoke(this, (int)(read * 100 / total.Value));
            }
        }

        Emit("Extracting SteamCMD...");
        ZipFile.ExtractToDirectory(zipPath, _steamCmdDir, overwriteFiles: true);
        File.Delete(zipPath);

        Emit("Running SteamCMD first-time bootstrap (may take 30–60 seconds)...");
        await RunSteamCmdAsync("+quit", ct);

        _logger.Info("SteamCMD ready.");
        Emit("SteamCMD installed successfully.");
    }

    // ── Server Install / Update ──────────────────────────────────────

    public async Task InstallOrUpdateServerAsync(bool validate = false, CancellationToken ct = default)
    {
        if (!IsSteamCmdInstalled)
            await DownloadSteamCmdAsync(ct);

        Directory.CreateDirectory(_serverFilesDir);

        string args = $"+force_install_dir \"{_serverFilesDir}\" " +
                      $"+login anonymous " +
                      $"+app_update {WINDROSE_APP_ID}" +
                      (validate ? " validate" : "") +
                      " +quit";

        Emit(validate ? "Validating server files..." : "Installing/updating server files...");
        _logger.Info($"Running SteamCMD: {args}");

        bool success = false;
        void TrackSuccess(object? s, string line)
        {
            if (line.Contains($"App '{WINDROSE_APP_ID}'") && line.Contains("fully installed"))
                success = true;
        }
        OutputReceived += TrackSuccess;

        int exitCode = await RunSteamCmdAsync(args, ct);
        if (exitCode != 0 && !success)
        {
            Emit($"SteamCMD exited with code {exitCode}. Running +quit settle pass and retrying...");
            _logger.Info($"SteamCMD non-zero exit ({exitCode}) — running +quit settle pass, then retrying.");
            await RunSteamCmdAsync("+quit", ct);
            Emit("Retrying install...");
            await RunSteamCmdAsync(args, ct);
        }

        OutputReceived -= TrackSuccess;

        if (IsServerInstalled || success)
        {
            Emit("Server installation complete.");
            _logger.Info("Server installation complete.");
        }
        else
        {
            Emit("SteamCMD finished. Check the log above for errors.");
            _logger.Warning("SteamCMD finished but WindroseServer.exe not found.");
        }
    }

    // ── Internal ─────────────────────────────────────────────────────

    private async Task<int> RunSteamCmdAsync(string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = _steamCmdExe,
            Arguments              = arguments,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            WorkingDirectory       = _steamCmdDir
        };

        using var proc = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true };

        long lastOutputTick = Environment.TickCount64;

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lastOutputTick = Environment.TickCount64;
            Emit(e.Data);
            ParseProgress(e.Data);
            if (e.Data.Contains("type 'quit' to exit"))
                Emit("SteamCMD is self-updating — this can take 1-2 minutes, please wait...");
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) { lastOutputTick = Environment.TickCount64; Emit(e.Data); }
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(async () =>
        {
            while (!heartbeatCts.Token.IsCancellationRequested)
            {
                await Task.Delay(30_000, heartbeatCts.Token).ContinueWith(_ => { });
                if (heartbeatCts.Token.IsCancellationRequested) break;
                long silentMs = Environment.TickCount64 - lastOutputTick;
                if (silentMs >= 28_000 && !proc.HasExited)
                    Emit($"  ... still working ({silentMs / 1000}s since last output) ...");
            }
        }, heartbeatCts.Token);

        await proc.WaitForExitAsync(ct);
        heartbeatCts.Cancel();
        return proc.ExitCode;
    }

    private void ParseProgress(string line)
    {
        var match = System.Text.RegularExpressions.Regex.Match(line,
            @"progress:\s*([\d.]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && double.TryParse(match.Groups[1].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double pct))
        {
            DownloadProgressChanged?.Invoke(this, (int)Math.Min(pct, 100));
        }
    }

    private void Emit(string msg) => OutputReceived?.Invoke(this, msg);
}
