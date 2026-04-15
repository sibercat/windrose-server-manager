namespace WindroseServerManager.Forms;

partial class MainForm
{
    private async void BtnServerAction_Click(object? sender, EventArgs e)
    {
        switch (_serverManager.State)
        {
            case ServerState.NotInstalled:
                await InstallServerAsync(validate: false);
                break;

            case ServerState.Stopped:
                StartServer();
                break;

            case ServerState.Crashed:
                await InstallServerAsync(validate: true);
                break;

            case ServerState.Running:
            case ServerState.Starting:
                await StopServerAsync();
                break;

            case ServerState.Installing:
                _installCts?.Cancel();
                break;
        }
    }

    private async void BtnUpdate_Click(object? sender, EventArgs e)
    {
        if (_serverManager.IsRunning)
        {
            MessageBox.Show("Stop the server before updating.", "Server Running",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        await InstallServerAsync(validate: true);
    }

    private async void BtnRestart_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show("Restart the server now?", "Confirm Restart",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        AppendConsole("[UI] Restarting server...", ThemeManager.StateStarting);
        await _serverManager.RestartAsync(_config);
    }

    // ── Install / Update ─────────────────────────────────────────────

    private async Task InstallServerAsync(bool validate)
    {
        _installCts = new CancellationTokenSource();
        UpdateServerState(ServerState.Installing);
        progressBar.Visible = true;
        progressBar.Value   = 0;
        lblProgress.Visible = true;
        lblProgress.Text    = "0%";

        try
        {
            AppendConsole(validate
                ? "Validating/updating server files via SteamCMD..."
                : "Installing Windrose Dedicated Server via SteamCMD (App ID: 4129620)...",
                ThemeManager.StateInstalling);

            await _steamCmd.InstallOrUpdateServerAsync(validate, _installCts.Token);

            progressBar.Value = 100;
            lblProgress.Text  = "Complete!";
            AppendConsole("Installation complete!", ThemeManager.StateRunning);

            if (_steamCmd.IsServerInstalled)
                AppendConsole("Start the server once to generate ServerDescription.json, then configure your settings.",
                    Color.FromArgb(255, 185, 0));

            _serverManager.InitializeState();
            UpdateServerState(_serverManager.State);
        }
        catch (OperationCanceledException)
        {
            AppendConsole("Installation cancelled.", Color.Gray);
            _serverManager.InitializeState();
            UpdateServerState(_serverManager.State);
        }
        catch (Exception ex)
        {
            AppendConsole($"Installation failed: {ex.Message}", ThemeManager.StateCrashed);
            _logger.Error("Installation failed.", ex);
            _serverManager.InitializeState();
            UpdateServerState(_serverManager.State);
        }
        finally
        {
            progressBar.Visible = false;
            lblProgress.Visible = false;
            _installCts = null;
        }
    }

    // ── Start ─────────────────────────────────────────────────────────

    private void StartServer()
    {
        if (!_steamCmd.IsServerInstalled)
        {
            MessageBox.Show("Server files are not installed. Use 'Install Server' first.",
                "Not Installed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Save launcher settings before starting
        BuildConfigFromUi();
        _configManager.SaveSettings(_config);

        _serverManager.ConfigureCrashDetection(
            _config.EnableCrashDetection,
            _config.AutoRestart,
            _config.MaxRestartAttempts);

        AppendConsole("[UI] Starting server...", ThemeManager.StateStarting);
        _serverManager.Start(_config);

        if (_config.EnableDiscordWebhook && _config.NotifyOnStart)
            _ = _discordService.NotifyServerStarted(_config.DiscordWebhookUrl, _config.ServerName);

        ApplyScheduleFromConfig();
        ApplyBackupFromConfig();
    }

    // ── Stop ──────────────────────────────────────────────────────────

    private async Task StopServerAsync()
    {
        AppendConsole("[UI] Stopping server...", ThemeManager.StateStopped);
        await _serverManager.StopAsync();

        if (_config.EnableDiscordWebhook && _config.NotifyOnStop)
            await _discordService.NotifyServerStopped(_config.DiscordWebhookUrl, _config.ServerName);
    }

    // ── Save Launcher Settings ────────────────────────────────────────

    private void BtnSaveSettings_Click(object? sender, EventArgs e)
    {
        BuildConfigFromUi();
        _configManager.SaveSettings(_config);
        SetDirty(false);
        AppendConsole("[Settings] Launcher settings saved.", ThemeManager.StateRunning);

        _serverManager.ConfigureCrashDetection(
            _config.EnableCrashDetection, _config.AutoRestart, _config.MaxRestartAttempts);
        ApplyScheduleFromConfig();
        ApplyBackupFromConfig();
    }
}
