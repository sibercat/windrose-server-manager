using System.Diagnostics;

namespace WindroseServerManager.Forms;

public partial class MainForm : Form
{
    // ── Paths ────────────────────────────────────────────────────────
    private readonly string RootDir;

    // ── Services ─────────────────────────────────────────────────────
    private readonly FileLogger              _logger;
    private readonly SteamCmdService         _steamCmd;
    private readonly ServerManager           _serverManager;
    private readonly ConfigurationManager    _configManager;
    private readonly BackupService           _backupService;
    private readonly ScheduledRestartService _scheduleService;
    private readonly DiscordWebhookService   _discordService;

    // ── State ─────────────────────────────────────────────────────────
    private ServerConfiguration _config = new();
    private bool _isDirty;
    private DateTime _serverStartTime;
    private CancellationTokenSource? _installCts;

    // ── Timers ────────────────────────────────────────────────────────
    private readonly System.Windows.Forms.Timer _uptimeTimer = new() { Interval = 1000 };

    // ── Constructor ───────────────────────────────────────────────────
    public MainForm()
    {
        RootDir = Path.Combine(AppContext.BaseDirectory, "WindroseServer");

        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(Path.Combine(RootDir, "steamcmd"));
        Directory.CreateDirectory(Path.Combine(RootDir, "Backups"));
        Directory.CreateDirectory(Path.Combine(RootDir, "Logs"));

        _logger          = new FileLogger(Path.Combine(RootDir, "Logs"));
        _configManager   = new ConfigurationManager(RootDir, _logger);
        _steamCmd        = new SteamCmdService(RootDir, _logger);
        _serverManager   = new ServerManager(RootDir, _logger);
        _backupService   = new BackupService(RootDir, _logger, _configManager);
        _scheduleService = new ScheduledRestartService(_logger);
        _discordService  = new DiscordWebhookService(_logger);

        InitializeComponent();

        _config = _configManager.LoadSettings();
        LoadSettingsIntoUi();
        WireEvents();

        ThemeManager.Apply(this);

        this.Shown += (_, _) => ThemeManager.ReapplyConsoleThemeOverrides(this);

        _serverManager.InitializeState();
        UpdateServerState(_serverManager.State);

        _uptimeTimer.Tick += UptimeTick;
        _uptimeTimer.Start();

        _logger.Info("Windrose Server Manager started.");
    }

    // ── Wiring ────────────────────────────────────────────────────────

    private void WireEvents()
    {
        FormClosing += OnFormClosing;

        // Menu
        menuOpenServerFolder.Click  += (_, _) => OpenFolder(RootDir);
        menuOpenBackupFolder.Click  += (_, _) => OpenFolder(_backupService.BackupsDirectory);
        menuOpenConfigFile.Click    += (_, _) => OpenFile(_configManager.ServerDescriptionPath);
        menuExit.Click              += (_, _) => Close();

        // Dashboard
        btnServerAction.Click += BtnServerAction_Click;
        btnUpdate.Click       += BtnUpdate_Click;
        btnRestart.Click      += BtnRestart_Click;
        btnClearConsole.Click += (_, _) => rtbConsole.Clear();

        // Settings — launcher prefs
        btnSaveSettings.Click   += BtnSaveSettings_Click;
        btnReloadSettings.Click += (_, _) => { LoadSettingsIntoUi(); SetDirty(false); };

        // Settings — ServerDescription.json
        btnWriteServerDesc.Click  += BtnWriteServerDesc_Click;
        btnRefreshInviteCode.Click += BtnRefreshInviteCode_Click;

        // Settings — WorldDescription.json
        btnSaveWorldDesc.Click    += BtnSaveWorldDesc_Click;
        btnRefreshWorldDesc.Click += BtnRefreshWorldDesc_Click;

        // Settings — Game.ini (Advanced Network)
        btnSaveGameIni.Click  += BtnSaveGameIni_Click;
        btnResetGameIni.Click += BtnResetGameIni_Click;
        rdoEasy.CheckedChanged         += (_, _) => UpdateWorldPresetVisibility();
        rdoMedium.CheckedChanged       += (_, _) => UpdateWorldPresetVisibility();
        rdoHard.CheckedChanged         += (_, _) => UpdateWorldPresetVisibility();
        rdoCustomPreset.CheckedChanged += (_, _) => UpdateWorldPresetVisibility();
        btnCopyInviteCode.Click   += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(txtInviteCode.Text))
                Clipboard.SetText(txtInviteCode.Text);
        };

        // Mark dirty on any launcher settings change
        foreach (Control c in GetAllSettingsControls())
        {
            if (c is TextBox tb)        tb.TextChanged          += (_, _) => SetDirty(true);
            if (c is NumericUpDown nud) nud.ValueChanged        += (_, _) => SetDirty(true);
            if (c is ComboBox cmb)      cmb.SelectedIndexChanged += (_, _) => SetDirty(true);
            if (c is CheckBox chk)      chk.CheckedChanged      += (_, _) => SetDirty(true);
            if (c is RadioButton rdo)   rdo.CheckedChanged      += (_, _) => SetDirty(true);
        }

        // Server manager events
        _serverManager.StateChanged   += OnServerStateChanged;
        _serverManager.OutputReceived += OnServerOutput;
        _serverManager.CrashDetected  += OnCrashDetected;
        _serverManager.AutoRestarted  += OnAutoRestarted;

        // SteamCMD events
        _steamCmd.OutputReceived          += OnSteamCmdOutput;
        _steamCmd.DownloadProgressChanged += OnDownloadProgress;

        // Backup events
        _backupService.BackupCreated += OnBackupCreated;
        _backupService.BackupFailed  += OnBackupFailed;

        // Scheduled restart events
        _scheduleService.WarningIssued    += OnRestartWarning;
        _scheduleService.RestartTriggered += OnScheduledRestart;

        // Automation tab
        btnTestWebhook.Click     += async (_, _) => await TestWebhookAsync();
        btnCreateBackupNow.Click += async (_, _) => await CreateBackupNowAsync();
        rdoInterval.CheckedChanged   += (_, _) => UpdateRestartModeVisibility();
        rdoFixedTimes.CheckedChanged += (_, _) => UpdateRestartModeVisibility();

        // Backups tab
        btnCreateBackup.Click      += async (_, _) => await CreateBackupNowAsync();
        btnRestoreBackup.Click     += async (_, _) => await RestoreBackupAsync();
        btnDeleteBackup.Click      += (_, _) => DeleteSelectedBackup();
        btnOpenBackupsFolder.Click += (_, _) => OpenFolder(_backupService.BackupsDirectory);
        tabMain.SelectedIndexChanged += (_, _) =>
        {
            if (tabMain.SelectedTab == tabBackups) RefreshBackupList();
            if (tabMain.SelectedTab == tabSettings)
            {
                BtnRefreshInviteCode_Click(null, EventArgs.Empty);
                BtnRefreshWorldDesc_Click(null, EventArgs.Empty);
                LoadGameIniIntoUi(_configManager.ReadGameIni());
            }
        };
    }

    // ── Settings Load / Save ──────────────────────────────────────────

    private void LoadSettingsIntoUi()
    {
        // Launcher prefs
        cmbProcessPriority.SelectedIndex = _config.ProcessPriority switch
        {
            "AboveNormal" => 1,
            "High"        => 2,
            _             => 0
        };

        chkCrashDetection.Checked = _config.EnableCrashDetection;
        chkAutoRestart.Checked    = _config.AutoRestart;
        numMaxRestarts.Value      = Math.Clamp(_config.MaxRestartAttempts, 1, 20);
        txtCustomArgs.Text        = _config.CustomLaunchArgs;

        // Automation
        chkScheduleEnabled.Checked = _config.ScheduledRestartEnabled;
        rdoInterval.Checked        = !_config.UseFixedRestartTimes;
        rdoFixedTimes.Checked      = _config.UseFixedRestartTimes;
        numIntervalHours.Value     = Math.Clamp(_config.RestartIntervalHours, 1, 168);
        txtFixedTimes.Text         = _config.FixedRestartTimes;
        numWarningMins.Value       = Math.Clamp(_config.RestartWarningMinutes, 0, 60);

        chkAutoBackup.Checked  = _config.AutoBackupEnabled;
        numBackupKeep.Value    = Math.Clamp(_config.BackupKeepCount, 1, 100);
        SetBackupIntervalCombo(_config.BackupIntervalHours);

        chkDiscordEnabled.Checked = _config.EnableDiscordWebhook;
        txtWebhookUrl.Text        = _config.DiscordWebhookUrl;
        chkNotifyStart.Checked    = _config.NotifyOnStart;
        chkNotifyStop.Checked     = _config.NotifyOnStop;
        chkNotifyCrash.Checked    = _config.NotifyOnCrash;
        chkNotifyRestart.Checked  = _config.NotifyOnRestart;
        chkNotifyBackup.Checked   = _config.NotifyOnBackup;

        // Load ServerDescription.json into Settings tab
        BtnRefreshInviteCode_Click(null, EventArgs.Empty);

        // Load WorldDescription.json into Settings tab (uses WorldIslandId from ServerDescription)
        BtnRefreshWorldDesc_Click(null, EventArgs.Empty);

        // Load Game.ini overrides
        LoadGameIniIntoUi(_configManager.ReadGameIni());

        UpdateRestartModeVisibility();
        UpdateWorldPresetVisibility();
        SetDirty(false);
    }

    private void BuildConfigFromUi()
    {
        _config.ProcessPriority = cmbProcessPriority.SelectedIndex switch
        {
            1 => "AboveNormal",
            2 => "High",
            _ => "Normal"
        };

        _config.EnableCrashDetection = chkCrashDetection.Checked;
        _config.AutoRestart          = chkAutoRestart.Checked;
        _config.MaxRestartAttempts   = (int)numMaxRestarts.Value;
        _config.CustomLaunchArgs     = txtCustomArgs.Text.Trim();

        _config.ScheduledRestartEnabled = chkScheduleEnabled.Checked;
        _config.UseFixedRestartTimes    = rdoFixedTimes.Checked;
        _config.RestartIntervalHours    = (int)numIntervalHours.Value;
        _config.FixedRestartTimes       = txtFixedTimes.Text.Trim();
        _config.RestartWarningMinutes   = (int)numWarningMins.Value;

        _config.AutoBackupEnabled   = chkAutoBackup.Checked;
        _config.BackupKeepCount     = (int)numBackupKeep.Value;
        _config.BackupIntervalHours = GetBackupIntervalHours();

        _config.EnableDiscordWebhook = chkDiscordEnabled.Checked;
        _config.DiscordWebhookUrl    = txtWebhookUrl.Text.Trim();
        _config.NotifyOnStart        = chkNotifyStart.Checked;
        _config.NotifyOnStop         = chkNotifyStop.Checked;
        _config.NotifyOnCrash        = chkNotifyCrash.Checked;
        _config.NotifyOnRestart      = chkNotifyRestart.Checked;
        _config.NotifyOnBackup       = chkNotifyBackup.Checked;
    }

    // ── ServerDescription.json ────────────────────────────────────────

    private void BtnRefreshInviteCode_Click(object? sender, EventArgs e)
    {
        var data = _configManager.ReadServerDescription();
        if (data == null)
        {
            txtInviteCode.Text     = "(not generated yet — start the server once)";
            txtServerName.Text     = "";
            chkPasswordProtected.Checked = false;
            txtPassword.Text       = "";
            numMaxPlayers.Value    = 8;
            txtP2pProxy.Text       = "0.0.0.0";
            return;
        }

        txtInviteCode.Text          = data.InviteCode;
        txtServerName.Text          = data.ServerName;
        chkPasswordProtected.Checked = data.IsPasswordProtected;
        txtPassword.Text            = data.Password;
        numMaxPlayers.Value         = Math.Clamp(data.MaxPlayerCount, 1, 200);
        txtP2pProxy.Text            = data.P2pProxyAddress;
    }

    private void BtnWriteServerDesc_Click(object? sender, EventArgs e)
    {
        if (_serverManager.IsRunning)
        {
            MessageBox.Show(
                "Stop the server before changing ServerDescription.json.\n\n" +
                "The server only reads this file on startup.",
                "Server Running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Read current file to preserve PersistentServerId, WorldIslandId, InviteCode, etc.
        var existing = _configManager.ReadServerDescription();
        if (existing == null)
        {
            MessageBox.Show(
                "ServerDescription.json not found.\nPlease start the server once to generate it first.",
                "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        existing.ServerName          = txtServerName.Text.Trim();
        existing.IsPasswordProtected = chkPasswordProtected.Checked;
        existing.Password            = txtPassword.Text;
        existing.MaxPlayerCount      = (int)numMaxPlayers.Value;
        existing.P2pProxyAddress     = txtP2pProxy.Text.Trim();
        // InviteCode, PersistentServerId, WorldIslandId preserved from existing

        bool ok = _configManager.WriteServerDescription(existing);
        if (ok)
        {
            AppendConsole("[Settings] ServerDescription.json saved. Restart the server for changes to take effect.",
                ThemeManager.StateRunning);
            BtnRefreshInviteCode_Click(null, EventArgs.Empty);
        }
        else
        {
            MessageBox.Show("Failed to write ServerDescription.json. Check the log for details.",
                "Write Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── WorldDescription.json ────────────────────────────────────────

    private void BtnRefreshWorldDesc_Click(object? sender, EventArgs e)
    {
        var serverDesc = _configManager.ReadServerDescription();
        if (serverDesc == null)
        {
            // No ServerDescription.json yet — leave world fields at defaults
            rdoMedium.Checked = true;
            txtWorldName.Text = "";
            return;
        }

        var data = _configManager.ReadWorldDescription(serverDesc.WorldIslandId);
        if (data == null)
        {
            // WorldDescription.json not generated yet
            rdoMedium.Checked = true;
            txtWorldName.Text = "";
            return;
        }

        txtWorldName.Text = data.WorldName;

        // Preset radio buttons
        rdoEasy.Checked         = data.WorldPresetType == "Easy";
        rdoMedium.Checked       = data.WorldPresetType == "Medium";
        rdoHard.Checked         = data.WorldPresetType == "Hard";
        rdoCustomPreset.Checked = data.WorldPresetType == "Custom";
        if (!rdoEasy.Checked && !rdoMedium.Checked && !rdoHard.Checked && !rdoCustomPreset.Checked)
            rdoMedium.Checked = true;

        // Combat difficulty
        cmbCombatDifficulty.SelectedIndex = data.CombatDifficulty switch
        {
            "Easy"  => 0,
            "Hard"  => 2,
            _       => 1
        };

        // Bool params
        chkCoopQuests.Checked           = data.CoopSharedQuests;
        chkImmersiveExploration.Checked = data.ImmersiveExploration;

        // Float params — clamp to NUD ranges
        numMobHealth.Value  = (decimal)Math.Clamp(data.MobHealthMultiplier,             0.2, 5.0);
        numMobDamage.Value  = (decimal)Math.Clamp(data.MobDamageMultiplier,             0.2, 5.0);
        numShipHealth.Value = (decimal)Math.Clamp(data.ShipHealthMultiplier,            0.4, 5.0);
        numShipDamage.Value = (decimal)Math.Clamp(data.ShipDamageMultiplier,            0.2, 2.5);
        numBoarding.Value   = (decimal)Math.Clamp(data.BoardingDifficultyMultiplier,    0.2, 5.0);
        numCoopStats.Value  = (decimal)Math.Clamp(data.CoopStatsCorrectionModifier,     0.0, 2.0);
        numCoopShips.Value  = (decimal)Math.Clamp(data.CoopShipStatsCorrectionModifier, 0.0, 2.0);

        UpdateWorldPresetVisibility();
    }

    private void BtnSaveWorldDesc_Click(object? sender, EventArgs e)
    {
        if (_serverManager.IsRunning)
        {
            MessageBox.Show(
                "Stop the server before changing WorldDescription.json.\n\n" +
                "The server only reads this file on startup.",
                "Server Running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var serverDesc = _configManager.ReadServerDescription();
        if (serverDesc == null)
        {
            MessageBox.Show(
                "ServerDescription.json not found.\nPlease start the server once to generate it first.",
                "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var existing = _configManager.ReadWorldDescription(serverDesc.WorldIslandId);
        if (existing == null)
        {
            MessageBox.Show(
                "WorldDescription.json not found.\nPlease start the server once to generate it first.",
                "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        existing.WorldName       = txtWorldName.Text.Trim();
        existing.WorldPresetType = rdoEasy.Checked ? "Easy"
                                 : rdoHard.Checked ? "Hard"
                                 : rdoCustomPreset.Checked ? "Custom"
                                 : "Medium";

        existing.CombatDifficulty = cmbCombatDifficulty.SelectedIndex switch
        {
            0 => "Easy",
            2 => "Hard",
            _ => "Normal"
        };

        existing.CoopSharedQuests                = chkCoopQuests.Checked;
        existing.ImmersiveExploration            = chkImmersiveExploration.Checked;
        existing.MobHealthMultiplier             = (double)numMobHealth.Value;
        existing.MobDamageMultiplier             = (double)numMobDamage.Value;
        existing.ShipHealthMultiplier            = (double)numShipHealth.Value;
        existing.ShipDamageMultiplier            = (double)numShipDamage.Value;
        existing.BoardingDifficultyMultiplier    = (double)numBoarding.Value;
        existing.CoopStatsCorrectionModifier     = (double)numCoopStats.Value;
        existing.CoopShipStatsCorrectionModifier = (double)numCoopShips.Value;

        bool ok = _configManager.WriteWorldDescription(serverDesc.WorldIslandId, existing);
        if (ok)
        {
            AppendConsole("[Settings] WorldDescription.json saved. Restart the server for changes to take effect.",
                ThemeManager.StateRunning);
            BtnRefreshWorldDesc_Click(null, EventArgs.Empty);
        }
        else
        {
            MessageBox.Show("Failed to write WorldDescription.json. Check the log for details.",
                "Write Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── Game.ini (Advanced Network) ───────────────────────────────────

    private void LoadGameIniIntoUi(GameIniData data)
    {
        numPortMin.Value         = Math.Clamp(data.P2pLocalPortMin,     0, 65535);
        numPortMax.Value         = Math.Clamp(data.P2pLocalPortMax,     0, 65535);
        chkSecureConnection.Checked = data.IsP2pSecureConnection;
        chkRelayOnly.Checked        = data.IsP2pRelayOnly;
        numDisconnectDelay.Value = Math.Clamp(data.DisconnectDelaySecs, 0, 86400);
        numOwnerTimeout.Value    = Math.Clamp(data.OwnerTimeoutSecs,    0, 86400);
    }

    private GameIniData BuildGameIniFromUi() => new()
    {
        P2pLocalPortMin       = (int)numPortMin.Value,
        P2pLocalPortMax       = (int)numPortMax.Value,
        IsP2pSecureConnection = chkSecureConnection.Checked,
        IsP2pRelayOnly        = chkRelayOnly.Checked,
        DisconnectDelaySecs   = (int)numDisconnectDelay.Value,
        OwnerTimeoutSecs      = (int)numOwnerTimeout.Value,
    };

    private void BtnSaveGameIni_Click(object? sender, EventArgs e)
    {
        if (_serverManager.IsRunning)
        {
            MessageBox.Show(
                "Stop the server before changing network settings.\n\nRestart it afterwards for changes to take effect.",
                "Server Running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool ok = _configManager.WriteGameIni(BuildGameIniFromUi());
        if (ok)
        {
            bool fileExists = File.Exists(_configManager.GameIniPath);
            AppendConsole(fileExists
                ? "[Settings] Game.ini saved. Restart the server for changes to take effect."
                : "[Settings] All values are game defaults — Game.ini removed.",
                ThemeManager.StateRunning);
            LoadGameIniIntoUi(_configManager.ReadGameIni());
        }
        else
        {
            MessageBox.Show("Failed to write Game.ini. Check the log for details.",
                "Write Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnResetGameIni_Click(object? sender, EventArgs e)
    {
        if (_serverManager.IsRunning)
        {
            MessageBox.Show("Stop the server before resetting network settings.",
                "Server Running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            "Delete Game.ini and revert all network settings to the game's baked-in defaults?\n\nRestart the server afterwards.",
            "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        bool ok = _configManager.DeleteGameIni();
        if (ok)
        {
            AppendConsole("[Settings] Game.ini deleted — game will use baked-in defaults.", Color.Gray);
            LoadGameIniIntoUi(new GameIniData());
        }
    }

    // ── Helper Methods ────────────────────────────────────────────────

    private IEnumerable<Control> GetAllSettingsControls() =>
        GetAllControls(tabSettings).Concat(GetAllControls(tabAutomation));

    private static IEnumerable<Control> GetAllControls(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            yield return c;
            foreach (var child in GetAllControls(c))
                yield return child;
        }
    }

    private void SetDirty(bool dirty)
    {
        _isDirty = dirty;
        lblDirtyIndicator.Text      = dirty ? "● Unsaved changes" : "";
        lblDirtyIndicator.ForeColor = dirty ? Color.FromArgb(255, 185, 0) : Color.Transparent;
    }

    private void UpdateWorldPresetVisibility()
    {
        bool custom = rdoCustomPreset.Checked;
        cmbCombatDifficulty.Enabled      = custom;
        chkCoopQuests.Enabled            = custom;
        chkImmersiveExploration.Enabled  = custom;
        numMobHealth.Enabled             = custom;
        numMobDamage.Enabled             = custom;
        numShipHealth.Enabled            = custom;
        numShipDamage.Enabled            = custom;
        numBoarding.Enabled              = custom;
        numCoopStats.Enabled             = custom;
        numCoopShips.Enabled             = custom;
    }

    private void UpdateRestartModeVisibility()
    {
        txtFixedTimes.Enabled    = rdoFixedTimes.Checked;
        numIntervalHours.Enabled = rdoInterval.Checked;
    }

    private void SetBackupIntervalCombo(int hours)
    {
        int[] map = [1, 2, 4, 6, 12, 24];
        int idx = Array.FindIndex(map, h => h >= hours);
        cmbBackupInterval.SelectedIndex = Math.Max(0, idx < 0 ? map.Length - 1 : idx);
    }

    private int GetBackupIntervalHours()
    {
        int[] map = [1, 2, 4, 6, 12, 24];
        return map[Math.Clamp(cmbBackupInterval.SelectedIndex, 0, map.Length - 1)];
    }

    private void ApplyScheduleFromConfig()
    {
        _scheduleService.Configure(
            _config.ScheduledRestartEnabled,
            _config.UseFixedRestartTimes,
            _config.RestartIntervalHours,
            _config.FixedRestartTimes,
            _config.RestartWarningMinutes);
    }

    private void ApplyBackupFromConfig()
    {
        _backupService.Configure(
            _config.AutoBackupEnabled,
            _config.BackupIntervalHours,
            _config.BackupKeepCount);
    }

    // ── Server State UI ───────────────────────────────────────────────

    private void UpdateServerState(ServerState state)
    {
        this.InvokeIfRequired(() =>
        {
            (lblStatusDot.Text, lblStatusDot.ForeColor) = state switch
            {
                ServerState.Running    => ("●", ThemeManager.StateRunning),
                ServerState.Starting   => ("●", ThemeManager.StateStarting),
                ServerState.Stopping   => ("●", ThemeManager.StateStarting),
                ServerState.Crashed    => ("●", ThemeManager.StateCrashed),
                ServerState.Installing => ("●", ThemeManager.StateInstalling),
                _                      => ("●", ThemeManager.StateStopped)
            };

            lblStatus.Text = state switch
            {
                ServerState.Running      => "Running",
                ServerState.Starting     => "Starting...",
                ServerState.Stopping     => "Stopping...",
                ServerState.Crashed      => "Crashed",
                ServerState.Installing   => "Installing...",
                ServerState.NotInstalled => "Not Installed",
                _                        => "Stopped"
            };

            btnServerAction.Text = state switch
            {
                ServerState.NotInstalled => "Install Server",
                ServerState.Stopped      => "Start Server",
                ServerState.Crashed      => "Reinstall / Validate",
                ServerState.Running      => "Stop Server",
                ServerState.Starting     => "Stop Server",
                ServerState.Stopping     => "Stopping...",
                ServerState.Installing   => "Cancel",
                _                        => "Start Server"
            };

            btnServerAction.Tag = state is ServerState.Running or ServerState.Stopping
                ? "danger" : "accent";
            ThemeManager.Apply(this); // re-apply button color

            btnRestart.Enabled = state is ServerState.Running;
            btnUpdate.Enabled  = state is ServerState.Stopped or ServerState.NotInstalled or ServerState.Crashed;

            if (state == ServerState.Running && _serverStartTime == default)
                _serverStartTime = DateTime.Now;
            else if (state == ServerState.Stopped || state == ServerState.Crashed)
                _serverStartTime = default;

            tsStatus.Text      = $"  Status: {lblStatus.Text}";
            tsStatus.ForeColor = lblStatusDot.ForeColor;
        });
    }

    // ── Timers ────────────────────────────────────────────────────────

    private void UptimeTick(object? sender, EventArgs e)
    {
        string uptime = _serverStartTime == default
            ? "--:--:--"
            : (DateTime.Now - _serverStartTime).ToString(@"hh\:mm\:ss");
        tsUptime.Text = $"  Uptime: {uptime}";

        tsNextRestart.Text = _scheduleService.NextRestart < DateTime.MaxValue
            ? $"  Next Restart: {_scheduleService.NextRestart:HH:mm}" : "";

        tsNextBackup.Text = _backupService.NextBackupTime < DateTime.MaxValue
            ? $"  Next Backup: {_backupService.NextBackupTime:HH:mm}" : "";
    }

    // ── Server Events ─────────────────────────────────────────────────

    private void OnServerStateChanged(object? sender, ServerState state) =>
        UpdateServerState(state);

    private void OnServerOutput(object? sender, string msg) =>
        AppendConsole(msg, Color.FromArgb(204, 204, 204));

    private void OnCrashDetected(object? sender, EventArgs e)
    {
        AppendConsole("[CRASH] Server process crashed!", ThemeManager.StateCrashed);
        if (_config.EnableDiscordWebhook && _config.NotifyOnCrash)
            _ = _discordService.NotifyCrash(_config.DiscordWebhookUrl, _config.ServerName);
    }

    private void OnAutoRestarted(object? sender, EventArgs e)
    {
        AppendConsole("[AUTO-RESTART] Server auto-restarted.", ThemeManager.StateStarting);
        if (_config.EnableDiscordWebhook && _config.NotifyOnRestart)
            _ = _discordService.NotifyRestart(_config.DiscordWebhookUrl, _config.ServerName, "crash recovery");
    }

    private void OnSteamCmdOutput(object? sender, string msg) =>
        AppendConsole(msg, Color.FromArgb(180, 180, 180));

    private void OnDownloadProgress(object? sender, int pct) =>
        this.InvokeIfRequired(() =>
        {
            progressBar.Value = Math.Clamp(pct, 0, 100);
            lblProgress.Text  = $"{pct}%";
        });

    private void OnBackupCreated(object? sender, string path)
    {
        AppendConsole($"[BACKUP] Backup created: {Path.GetFileName(path)}", ThemeManager.StateRunning);
        if (_config.EnableDiscordWebhook && _config.NotifyOnBackup)
            _ = _discordService.NotifyBackup(_config.DiscordWebhookUrl, _config.ServerName, path);
    }

    private void OnBackupFailed(object? sender, string msg) =>
        AppendConsole($"[BACKUP] Failed: {msg}", ThemeManager.StateCrashed);

    private void OnRestartWarning(object? sender, int minutes)
    {
        AppendConsole($"[RESTART WARNING] Server restarting in {minutes} minute(s).", ThemeManager.StateStarting);
        if (_config.EnableDiscordWebhook && _config.NotifyOnRestart)
            _ = _discordService.NotifyRestart(_config.DiscordWebhookUrl, _config.ServerName,
                $"scheduled restart in {minutes} minute(s)");
    }

    private async void OnScheduledRestart(object? sender, EventArgs e)
    {
        AppendConsole("[SCHEDULED RESTART] Restarting server now...", ThemeManager.StateStarting);
        await _serverManager.RestartAsync(_config);
        if (_config.EnableDiscordWebhook && _config.NotifyOnRestart)
            _ = _discordService.NotifyRestart(_config.DiscordWebhookUrl, _config.ServerName, "scheduled");
    }

    // ── Console Output ────────────────────────────────────────────────

    private void AppendConsole(string msg, Color color)
    {
        this.InvokeIfRequired(() =>
        {
            bool scroll = chkAutoScroll.Checked;
            rtbConsole.AppendConsoleLine(msg, color, scroll);
        });
    }

    // ── Misc Helpers ──────────────────────────────────────────────────

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start("explorer.exe", path);
    }

    private static void OpenFile(string path)
    {
        if (!File.Exists(path))
        {
            MessageBox.Show($"File not found:\n{path}\n\nStart the server once to generate it.",
                "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private async Task TestWebhookAsync()
    {
        string url = txtWebhookUrl.Text.Trim();
        if (!DiscordWebhookService.IsValidUrl(url))
        {
            MessageBox.Show("Please enter a valid Discord webhook URL.", "Invalid URL",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        bool ok = await _discordService.TestAsync(url);
        MessageBox.Show(ok ? "Test message sent successfully!" : "Failed to send test message. Check the URL.",
            "Webhook Test", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    // ── Form Close ────────────────────────────────────────────────────

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_serverManager.IsRunning)
        {
            var result = MessageBox.Show(
                "The server is still running. Stop it before closing?",
                "Server Running", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            if (result == DialogResult.Cancel) { e.Cancel = true; return; }
            if (result == DialogResult.Yes)
            {
                e.Cancel = true;
                await _serverManager.StopAsync();

                if (_config.EnableDiscordWebhook && _config.NotifyOnStop)
                    await _discordService.NotifyServerStopped(_config.DiscordWebhookUrl, _config.ServerName);

                BuildConfigFromUi();
                _configManager.SaveSettings(_config);
                _scheduleService.Dispose();
                _backupService.Dispose();
                _uptimeTimer.Stop();
                _logger.Info("Application closed.");
                Application.Exit();
                return;
            }
        }

        BuildConfigFromUi();
        _configManager.SaveSettings(_config);
        _scheduleService.Dispose();
        _backupService.Dispose();
        _uptimeTimer.Stop();
        _logger.Info("Application closed.");
    }
}
