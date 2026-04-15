#nullable enable
namespace WindroseServerManager.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;

    // ── Menu & Status ────────────────────────────────────────────────
    protected MenuStrip menuStrip;
    protected ToolStripMenuItem menuFile;
    protected ToolStripMenuItem menuOpenServerFolder, menuOpenBackupFolder, menuOpenConfigFile, menuExit;
    protected StatusStrip statusStrip;
    protected ToolStripStatusLabel tsStatus, tsUptime, tsNextRestart, tsNextBackup, tsUpdateAvailable;

    // ── Main Tab ─────────────────────────────────────────────────────
    protected DarkTabControl tabMain;
    protected TabPage tabDashboard, tabSettings, tabAutomation, tabBackups, tabAbout;

    // ── Dashboard ────────────────────────────────────────────────────
    protected Label lblStatusDot, lblStatus;
    protected Button btnServerAction, btnUpdate, btnRestart;
    protected ProgressBar progressBar;
    protected Label lblProgress;
    protected Button btnClearConsole;
    protected Button btnRunDiagnostics;
    protected CheckBox chkAutoScroll;
    protected TextBox rtbConsole;

    // ── Settings — ServerDescription.json ────────────────────────────
    protected TextBox txtInviteCode, txtServerName, txtPassword, txtP2pProxy;
    protected CheckBox chkPasswordProtected;
    protected NumericUpDown numMaxPlayers;
    protected Button btnRefreshInviteCode, btnCopyInviteCode, btnWriteServerDesc;

    // ── Settings — WorldDescription.json ─────────────────────────────
    protected TextBox txtWorldName;
    protected RadioButton rdoEasy, rdoMedium, rdoHard, rdoCustomPreset;
    protected ComboBox cmbCombatDifficulty;
    protected CheckBox chkCoopQuests, chkImmersiveExploration;
    protected NumericUpDown numMobHealth, numMobDamage, numShipHealth, numShipDamage,
                            numBoarding, numCoopStats, numCoopShips;
    protected Button btnSaveWorldDesc, btnRefreshWorldDesc;

    // ── Settings — Advanced Network (Game.ini) ───────────────────────
    protected NumericUpDown numPortMin, numPortMax;
    protected CheckBox chkRelayOnly, chkSecureConnection;
    protected NumericUpDown numDisconnectDelay, numOwnerTimeout;
    protected Button btnSaveGameIni, btnResetGameIni;

    // ── Settings — Launcher prefs ─────────────────────────────────────
    protected ComboBox cmbProcessPriority;
    protected CheckBox chkCrashDetection, chkAutoRestart;
    protected NumericUpDown numMaxRestarts;
    protected TextBox txtCustomArgs;
    protected Button btnSaveSettings, btnReloadSettings;
    protected Label lblDirtyIndicator;

    // ── Automation ────────────────────────────────────────────────────
    protected CheckBox chkScheduleEnabled;
    protected RadioButton rdoInterval, rdoFixedTimes;
    protected NumericUpDown numIntervalHours, numWarningMins;
    protected TextBox txtFixedTimes;
    protected CheckBox chkAutoBackup;
    protected ComboBox cmbBackupInterval;
    protected NumericUpDown numBackupKeep;
    protected Button btnCreateBackupNow;
    protected CheckBox chkDiscordEnabled;
    protected TextBox txtWebhookUrl;
    protected Button btnTestWebhook;
    protected CheckBox chkNotifyStart, chkNotifyStop, chkNotifyCrash, chkNotifyRestart, chkNotifyBackup;

    // ── Tooltips ──────────────────────────────────────────────────────
    protected ToolTip toolTip;

    // ── Backups ───────────────────────────────────────────────────────
    protected DataGridView dgvBackups;
    protected Button btnCreateBackup, btnRestoreBackup, btnDeleteBackup, btnOpenBackupsFolder;
    protected Label lblBackupCount;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        SuspendLayout();

        Text          = $"Windrose Server Manager  v{Application.ProductVersion}";
        Size          = new Size(1100, 750);
        MinimumSize   = new Size(900, 640);
        StartPosition = FormStartPosition.CenterScreen;
        Font          = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        Icon          = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        BuildMenuStrip();
        BuildStatusStrip();

        tabMain = new DarkTabControl
        {
            Dock     = DockStyle.Fill,
            Font     = new Font("Segoe UI", 9.5f),
            Padding  = new Point(14, 4),
            ItemSize = new Size(120, 28)
        };

        tabDashboard  = new TabPage("  Dashboard");
        tabSettings   = new TabPage("  Settings");
        tabAutomation = new TabPage("  Automation");
        tabBackups    = new TabPage("  Backups");
        tabAbout      = new TabPage("  About");

        BuildDashboardTab();
        BuildSettingsTab();
        BuildAutomationTab();
        BuildBackupsTab();
        BuildAboutTab();

        tabMain.TabPages.AddRange([tabDashboard, tabSettings, tabAutomation, tabBackups, tabAbout]);

        BuildTooltips();

        Controls.Add(tabMain);
        Controls.Add(statusStrip);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;

        ResumeLayout(false);
        PerformLayout();
    }

    // ── MenuStrip ─────────────────────────────────────────────────────

    private void BuildMenuStrip()
    {
        menuStrip = new MenuStrip { Dock = DockStyle.Top };

        menuFile             = new ToolStripMenuItem("File");
        menuOpenServerFolder = new ToolStripMenuItem("Open Server Folder");
        menuOpenBackupFolder = new ToolStripMenuItem("Open Backup Folder");
        menuOpenConfigFile   = new ToolStripMenuItem("Open ServerDescription.json");
        menuExit             = new ToolStripMenuItem("Exit");

        menuFile.DropDownItems.AddRange([
            menuOpenServerFolder,
            menuOpenBackupFolder,
            menuOpenConfigFile,
            new ToolStripSeparator(),
            menuExit
        ]);

        menuStrip.Items.Add(menuFile);
    }

    // ── StatusStrip ───────────────────────────────────────────────────

    private void BuildStatusStrip()
    {
        statusStrip   = new StatusStrip { Dock = DockStyle.Bottom, SizingGrip = false };
        tsStatus      = new ToolStripStatusLabel("  Status: Not Installed") { Spring = false };
        tsUptime      = new ToolStripStatusLabel("  Uptime: --:--:--")      { Spring = false };
        tsNextRestart = new ToolStripStatusLabel("")                         { Spring = false };
        tsNextBackup  = new ToolStripStatusLabel("")                         { Spring = true  };
        tsUpdateAvailable = new ToolStripStatusLabel
        {
            Visible         = false,
            IsLink          = true,
            LinkBehavior    = LinkBehavior.HoverUnderline,
            ForeColor       = Color.FromArgb(78, 201, 176),
            ActiveLinkColor = Color.White,
            Font            = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            ToolTipText     = "Click to open the releases page",
            Spring          = false
        };
        statusStrip.Items.AddRange([tsStatus, tsUptime, tsNextRestart, tsNextBackup, tsUpdateAvailable]);
    }

    // ── Dashboard Tab ─────────────────────────────────────────────────

    private void BuildDashboardTab()
    {
        var pnlTop = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(12, 10, 12, 0) };

        lblStatusDot = new Label
        {
            Text      = "●",
            Font      = new Font("Segoe UI", 20f, FontStyle.Bold),
            ForeColor = ThemeManager.StateStopped,
            AutoSize  = true,
            Location  = new Point(12, 12)
        };

        lblStatus = new Label
        {
            Text     = "Not Installed",
            Font     = new Font("Segoe UI", 14f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(46, 20)
        };

        btnServerAction = new Button
        {
            Text     = "Install Server",
            Size     = new Size(140, 36),
            Location = new Point(220, 14),
            Tag      = "accent"
        };

        btnUpdate = new Button
        {
            Text     = "Update / Validate",
            Size     = new Size(140, 36),
            Location = new Point(368, 14)
        };

        btnRestart = new Button
        {
            Text     = "Restart",
            Size     = new Size(100, 36),
            Location = new Point(516, 14),
            Enabled  = false
        };

        progressBar = new ProgressBar
        {
            Minimum  = 0, Maximum = 100,
            Size     = new Size(300, 14),
            Location = new Point(220, 56),
            Visible  = false,
            Style    = ProgressBarStyle.Continuous
        };

        lblProgress = new Label
        {
            Text = "", AutoSize = true,
            Location = new Point(528, 56),
            Visible  = false
        };

        pnlTop.Controls.AddRange([lblStatusDot, lblStatus, btnServerAction,
            btnUpdate, btnRestart, progressBar, lblProgress]);

        var pnlBar = new Panel { Dock = DockStyle.Top, Height = 32 };
        btnClearConsole   = new Button   { Text = "Clear",            Size = new Size(70,  24), Location = new Point(8,   4) };
        chkAutoScroll     = new CheckBox { Text = "Auto-scroll", Checked = true, AutoSize = true, Location = new Point(88, 8) };
        btnRunDiagnostics = new Button   { Text = "Run Diagnostics",  Size = new Size(130, 24), Location = new Point(188, 4) };
        pnlBar.Controls.AddRange([btnClearConsole, chkAutoScroll, btnRunDiagnostics]);

        rtbConsole = new TextBox
        {
            Dock       = DockStyle.Fill,
            Multiline  = true,
            ReadOnly   = true,
            ScrollBars = ScrollBars.Vertical,
            Font       = new Font("Consolas", 9f),
            BackColor  = Color.FromArgb(12, 12, 12),
            ForeColor  = Color.FromArgb(204, 204, 204),
            Tag        = "console",
            WordWrap   = false
        };

        tabDashboard.Controls.Add(rtbConsole);
        tabDashboard.Controls.Add(pnlBar);
        tabDashboard.Controls.Add(pnlTop);
    }

    // ── Settings Tab ──────────────────────────────────────────────────

    private void BuildSettingsTab()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };
        int y = 8;

        // ── ServerDescription.json ────────────────────────────────────
        var grpDesc = MakeGroupBox("Server Config  (ServerDescription.json)", 8, y, 620, 200);

        var lblNote = new Label
        {
            Text      = "Edit here, then click Save to Server Config. Server must be stopped first. Restart to apply.",
            AutoSize  = false,
            Size      = new Size(600, 16),
            Location  = new Point(8, 20),
            ForeColor = Color.Gray,
            Font      = new Font("Segoe UI", 8f)
        };
        grpDesc.Controls.Add(lblNote);

        // Invite Code row (editable)
        var lblInv = new Label { Text = "Invite Code:", AutoSize = true, Location = new Point(8, 44) };
        txtInviteCode = new TextBox
        {
            Location  = new Point(110, 41),
            Size      = new Size(160, 24),
            Font      = new Font("Consolas", 10f, FontStyle.Bold),
            BackColor = Color.FromArgb(37, 37, 38)
        };
        btnCopyInviteCode   = new Button { Text = "Copy",    Size = new Size(60,  24), Location = new Point(278, 41) };
        btnRefreshInviteCode = new Button { Text = "Refresh", Size = new Size(70, 24), Location = new Point(346, 41) };
        grpDesc.Controls.AddRange([lblInv, txtInviteCode, btnCopyInviteCode, btnRefreshInviteCode]);

        // Server Name
        var lblName = new Label { Text = "Server Name:", AutoSize = true, Location = new Point(8, 74) };
        txtServerName = new TextBox { Location = new Point(110, 71), Size = new Size(250, 24) };
        grpDesc.Controls.AddRange([lblName, txtServerName]);

        // Password
        chkPasswordProtected = new CheckBox { Text = "Password Protected", AutoSize = true, Location = new Point(8, 104) };
        var lblPw = new Label { Text = "Password:", AutoSize = true, Location = new Point(180, 104) };
        txtPassword = new TextBox { Location = new Point(255, 101), Size = new Size(160, 24), UseSystemPasswordChar = true };
        grpDesc.Controls.AddRange([chkPasswordProtected, lblPw, txtPassword]);

        // Max Players + P2P proxy
        var lblMp = new Label { Text = "Max Players:", AutoSize = true, Location = new Point(8, 134) };
        numMaxPlayers = new NumericUpDown
        {
            Location = new Point(110, 131), Size = new Size(70, 24), Minimum = 1, Maximum = 200, Value = 8
        };
        var lblProxy = new Label { Text = "P2P Proxy Address:", AutoSize = true, Location = new Point(200, 134) };
        txtP2pProxy = new TextBox { Location = new Point(330, 131), Size = new Size(160, 24), Text = "0.0.0.0" };
        var lblProxyHint = new Label
        {
            Text      = "Use 0.0.0.0 to listen on all network interfaces (recommended for dedicated servers).",
            AutoSize  = false, Size = new Size(600, 15),
            Location  = new Point(8, 158),
            ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f)
        };
        grpDesc.Controls.AddRange([lblMp, numMaxPlayers, lblProxy, txtP2pProxy, lblProxyHint]);

        // Save button
        btnWriteServerDesc = new Button
        {
            Text = "Save to Server Config",
            Size = new Size(160, 28),
            Location = new Point(8, 176),
            Tag  = "accent"
        };
        grpDesc.Controls.Add(btnWriteServerDesc);

        scroll.Controls.Add(grpDesc);
        y += 210;

        // ── World Settings ────────────────────────────────────────────
        var grpWorld = MakeGroupBox("World Settings  (WorldDescription.json)", 8, y, 620, 338);

        var lblWorldNote = new Label
        {
            Text      = "Edit here, then click Save to World Config. Server must be stopped first. Restart to apply.",
            AutoSize  = false, Size = new Size(600, 16),
            Location  = new Point(8, 20),
            ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f)
        };
        grpWorld.Controls.Add(lblWorldNote);

        // World Name
        var lblWName = new Label { Text = "World Name:", AutoSize = true, Location = new Point(8, 44) };
        txtWorldName = new TextBox { Location = new Point(100, 41), Size = new Size(250, 24) };
        grpWorld.Controls.AddRange([lblWName, txtWorldName]);

        // Difficulty Preset
        var lblPreset = new Label { Text = "Difficulty:", AutoSize = true, Location = new Point(8, 74) };
        rdoEasy         = new RadioButton { Text = "Easy",   AutoSize = true, Location = new Point(85,  72) };
        rdoMedium       = new RadioButton { Text = "Medium", AutoSize = true, Location = new Point(148, 72), Checked = true };
        rdoHard         = new RadioButton { Text = "Hard",   AutoSize = true, Location = new Point(224, 72) };
        rdoCustomPreset = new RadioButton { Text = "Custom", AutoSize = true, Location = new Point(290, 72) };
        grpWorld.Controls.AddRange([lblPreset, rdoEasy, rdoMedium, rdoHard, rdoCustomPreset]);

        // Combat Difficulty
        var lblCombat = new Label { Text = "Combat Difficulty:", AutoSize = true, Location = new Point(8, 104) };
        cmbCombatDifficulty = new ComboBox
        {
            Location = new Point(130, 101), Size = new Size(110, 24), DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbCombatDifficulty.Items.AddRange(["Easy", "Normal", "Hard"]);
        cmbCombatDifficulty.SelectedIndex = 1;
        grpWorld.Controls.AddRange([lblCombat, cmbCombatDifficulty]);

        // Section separator
        var lblCustomSection = new Label
        {
            Text      = "── Custom Preset Settings ──────────────────────────────────",
            AutoSize  = false, Size = new Size(600, 18),
            Location  = new Point(8, 132),
            ForeColor = Color.FromArgb(90, 90, 90), Font = new Font("Segoe UI", 8f)
        };
        grpWorld.Controls.Add(lblCustomSection);

        // Bool params
        chkCoopQuests           = new CheckBox { Text = "Shared Co-op Quests",  AutoSize = true, Location = new Point(8,   154), Checked = true };
        chkImmersiveExploration = new CheckBox { Text = "Immersive Exploration", AutoSize = true, Location = new Point(210, 154) };
        grpWorld.Controls.AddRange([chkCoopQuests, chkImmersiveExploration]);

        // Float multipliers — fixed 2-column layout (no dynamic font measurement)
        // Left col: label x=8 w=100, NUD x=110 w=90 | Right col: label x=220 w=105, NUD x=327 w=90
        AddWorldMultiplier(grpWorld, "Mob Health:",   8,   180, out numMobHealth,  0.2m, 5.0m, 1.0m);
        AddWorldMultiplier(grpWorld, "Mob Damage:",  220,  180, out numMobDamage,  0.2m, 5.0m, 1.0m);

        AddWorldMultiplier(grpWorld, "Ship Health:",  8,   208, out numShipHealth, 0.4m, 5.0m, 1.0m);
        AddWorldMultiplier(grpWorld, "Ship Damage:", 220,  208, out numShipDamage, 0.2m, 2.5m, 1.0m);

        AddWorldMultiplier(grpWorld, "Boarding:",     8,   236, out numBoarding,   0.2m, 5.0m, 1.0m);
        AddWorldMultiplier(grpWorld, "Co-op Stats:", 220,  236, out numCoopStats,  0.0m, 2.0m, 1.0m);

        AddWorldMultiplier(grpWorld, "Co-op Ships:",  8,   264, out numCoopShips,  0.0m, 2.0m, 0.0m);

        // Save / Refresh buttons
        btnSaveWorldDesc    = new Button { Text = "Save to World Config", Size = new Size(160, 28), Location = new Point(8,   308), Tag = "accent" };
        btnRefreshWorldDesc = new Button { Text = "Refresh",              Size = new Size(80,  28), Location = new Point(176, 308) };
        grpWorld.Controls.AddRange([btnSaveWorldDesc, btnRefreshWorldDesc]);

        scroll.Controls.Add(grpWorld);
        y += 348;

        // ── Performance ───────────────────────────────────────────────
        var grpPerf = MakeGroupBox("Performance", 8, y, 620, 60);
        var lblPri  = new Label { Text = "Process Priority:", AutoSize = true, Location = new Point(8, 28) };
        cmbProcessPriority = new ComboBox
        {
            Location = new Point(130, 25), Size = new Size(130, 24), DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbProcessPriority.Items.AddRange(["Normal", "Above Normal", "High"]);
        cmbProcessPriority.SelectedIndex = 0;
        grpPerf.Controls.AddRange([lblPri, cmbProcessPriority]);
        scroll.Controls.Add(grpPerf);
        y += 70;

        // ── Crash Detection ───────────────────────────────────────────
        var grpCrash = MakeGroupBox("Crash Detection && Auto-Restart", 8, y, 620, 100);
        chkCrashDetection = new CheckBox { Text = "Enable crash detection", AutoSize = true, Location = new Point(8, 24), Checked = true };
        chkAutoRestart    = new CheckBox { Text = "Auto-restart on crash",  AutoSize = true, Location = new Point(8, 50), Checked = true };

        // Fixed-position label + NUD to prevent label bleeding into the spinner
        var lblMaxRestarts = new Label
        {
            Text = "Max restart attempts:", AutoSize = false, Size = new Size(145, 20),
            Location = new Point(250, 28), TextAlign = ContentAlignment.MiddleLeft
        };
        numMaxRestarts = new NumericUpDown
        {
            Location = new Point(397, 24), Size = new Size(80, 24), Minimum = 1, Maximum = 20, Value = 3
        };

        grpCrash.Controls.AddRange([chkCrashDetection, chkAutoRestart, lblMaxRestarts, numMaxRestarts]);
        scroll.Controls.Add(grpCrash);
        y += 110;

        // ── Custom Launch Args ────────────────────────────────────────
        var grpArgs = MakeGroupBox("Custom Launch Arguments", 8, y, 620, 76);
        var lblArgs = new Label { Text = "Extra args:", AutoSize = true, Location = new Point(8, 28) };
        txtCustomArgs = new TextBox { Location = new Point(90, 25), Size = new Size(518, 24) };
        var lblArgsHint = new Label
        {
            Text      = "Added after -log. Leave blank for default.",
            AutoSize  = false, Size = new Size(600, 15),
            Location  = new Point(8, 54),
            ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f)
        };
        grpArgs.Controls.AddRange([lblArgs, txtCustomArgs, lblArgsHint]);
        scroll.Controls.Add(grpArgs);
        y += 86;

        // ── Advanced Network (Game.ini) ───────────────────────────────
        var grpNet = MakeGroupBox("Advanced Network Settings  (Game.ini)", 8, y, 620, 256);

        var lblNetNote = new Label
        {
            Text      = "Overrides baked-in defaults. Leave ports at 0 to use the game's defaults. Requires server restart.",
            AutoSize  = false, Size = new Size(600, 16),
            Location  = new Point(8, 20),
            ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f)
        };
        grpNet.Controls.Add(lblNetNote);

        // Port range row — fixed-width labels so NUDs never get clipped
        var lblPortRange = new Label
        {
            Text = "P2P Port Range:", AutoSize = false, Size = new Size(115, 20),
            Location = new Point(8, 46), TextAlign = ContentAlignment.MiddleLeft
        };
        var lblPortMin = new Label
        {
            Text = "Min:", AutoSize = false, Size = new Size(32, 20),
            Location = new Point(126, 46), TextAlign = ContentAlignment.MiddleLeft
        };
        numPortMin = new NumericUpDown { Location = new Point(160, 42), Size = new Size(76, 24), Minimum = 0, Maximum = 65535, Value = 0 };
        var lblPortMax = new Label
        {
            Text = "Max:", AutoSize = false, Size = new Size(36, 20),
            Location = new Point(246, 46), TextAlign = ContentAlignment.MiddleLeft
        };
        numPortMax = new NumericUpDown { Location = new Point(284, 42), Size = new Size(76, 24), Minimum = 0, Maximum = 65535, Value = 0 };
        var lblPortHint = new Label
        {
            Text = "0 = game default  |  open these ports (UDP) in your firewall / VPS security group",
            AutoSize = false, Size = new Size(600, 14),
            Location = new Point(8, 70),
            ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f)
        };
        grpNet.Controls.AddRange([lblPortRange, lblPortMin, numPortMin, lblPortMax, numPortMax, lblPortHint]);

        // Connection mode checkboxes
        chkSecureConnection = new CheckBox
        {
            Text = "Encrypt P2P connections  (off by default — enable for private servers)",
            AutoSize = true, Location = new Point(8, 94)
        };
        chkRelayOnly = new CheckBox
        {
            Text = "Force relay-only  (hides player IPs from each other, may add latency)",
            AutoSize = true, Location = new Point(8, 120)
        };
        grpNet.Controls.AddRange([chkSecureConnection, chkRelayOnly]);

        // Disconnect delay row
        var lblDisc = new Label
        {
            Text = "Server stops after all players leave:", AutoSize = false, Size = new Size(232, 20),
            Location = new Point(8, 152), TextAlign = ContentAlignment.MiddleLeft
        };
        numDisconnectDelay = new NumericUpDown { Location = new Point(242, 148), Size = new Size(76, 24), Minimum = 0, Maximum = 86400, Value = 0 };
        var lblDiscUnit = new Label { Text = "seconds  (0 = game default)", AutoSize = true, Location = new Point(326, 152) };
        grpNet.Controls.AddRange([lblDisc, numDisconnectDelay, lblDiscUnit]);

        // Owner timeout row
        var lblOwner = new Label
        {
            Text = "Timeout waiting for first player:", AutoSize = false, Size = new Size(232, 20),
            Location = new Point(8, 180), TextAlign = ContentAlignment.MiddleLeft
        };
        numOwnerTimeout = new NumericUpDown { Location = new Point(242, 176), Size = new Size(76, 24), Minimum = 0, Maximum = 86400, Value = 0 };
        var lblOwnerUnit = new Label { Text = "seconds  (0 = game default)", AutoSize = true, Location = new Point(326, 180) };
        grpNet.Controls.AddRange([lblOwner, numOwnerTimeout, lblOwnerUnit]);

        // Save / Reset buttons
        btnSaveGameIni  = new Button { Text = "Save Network Config", Size = new Size(160, 28), Location = new Point(8,   216), Tag = "accent" };
        btnResetGameIni = new Button { Text = "Reset to Defaults",   Size = new Size(140, 28), Location = new Point(176, 216) };
        grpNet.Controls.AddRange([btnSaveGameIni, btnResetGameIni]);

        scroll.Controls.Add(grpNet);
        y += 266;

        // ── Save/Reload launcher settings ────────────────────────────
        btnSaveSettings   = new Button { Text = "Save Launcher Settings", Size = new Size(160, 30), Location = new Point(8, y), Tag = "accent" };
        btnReloadSettings = new Button { Text = "Reload",                  Size = new Size(80,  30), Location = new Point(176, y) };
        lblDirtyIndicator = new Label  { AutoSize = true, Location = new Point(270, y + 8), ForeColor = Color.FromArgb(255, 185, 0) };
        scroll.Controls.AddRange([btnSaveSettings, btnReloadSettings, lblDirtyIndicator]);

        tabSettings.Controls.Add(scroll);
    }

    // ── Automation Tab ─────────────────────────────────────────────────

    private void BuildAutomationTab()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };
        int y = 8;

        // Scheduled Restart
        var grpSched = MakeGroupBox("Scheduled Restarts", 8, y, 580, 160);
        chkScheduleEnabled = new CheckBox { Text = "Enable scheduled restarts", AutoSize = true, Location = new Point(8, 22) };
        rdoInterval   = new RadioButton { Text = "Restart every N hours",     AutoSize = true, Location = new Point(8, 48), Checked = true };
        rdoFixedTimes = new RadioButton { Text = "Fixed times (HH:mm,HH:mm)", AutoSize = true, Location = new Point(8, 72) };

        // Interval hours — fixed label + NUD (avoids font-measurement clipping)
        var lblIntervalH = new Label
        {
            Text = "Interval (hours):", AutoSize = false, Size = new Size(120, 20),
            Location = new Point(220, 52), TextAlign = ContentAlignment.MiddleLeft
        };
        numIntervalHours = new NumericUpDown
        {
            Location = new Point(342, 48), Size = new Size(80, 24), Minimum = 1, Maximum = 168, Value = 6
        };

        var lblFixed = new Label { Text = "Times:", AutoSize = true, Location = new Point(220, 76) };
        txtFixedTimes = new TextBox { Text = "03:00,15:00", Location = new Point(272, 73), Size = new Size(150, 24) };

        // Warning mins — fixed label + NUD; min=0 (0 = no warning)
        var lblWarn = new Label
        {
            Text = "Warning (mins):", AutoSize = false, Size = new Size(115, 20),
            Location = new Point(8, 104), TextAlign = ContentAlignment.MiddleLeft
        };
        numWarningMins = new NumericUpDown
        {
            Location = new Point(125, 100), Size = new Size(80, 24), Minimum = 0, Maximum = 60, Value = 10
        };

        grpSched.Controls.AddRange([chkScheduleEnabled, rdoInterval, rdoFixedTimes,
            lblIntervalH, numIntervalHours, lblFixed, txtFixedTimes, lblWarn, numWarningMins]);
        scroll.Controls.Add(grpSched);
        y += 170;

        // Auto Backup
        var grpBackup = MakeGroupBox("Auto Backup", 8, y, 580, 110);
        chkAutoBackup = new CheckBox { Text = "Enable automatic backups", AutoSize = true, Location = new Point(8, 22) };
        var lblBkpInterval = new Label { Text = "Backup interval:", AutoSize = true, Location = new Point(8, 52) };
        cmbBackupInterval = new ComboBox { Location = new Point(120, 49), Size = new Size(130, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        cmbBackupInterval.Items.AddRange(["Every 1 hour", "Every 2 hours", "Every 4 hours", "Every 6 hours", "Every 12 hours", "Every 24 hours"]);
        cmbBackupInterval.SelectedIndex = 3;

        // Keep count — fixed label + NUD
        var lblKeep = new Label
        {
            Text = "Keep last N backups:", AutoSize = false, Size = new Size(140, 20),
            Location = new Point(8, 84), TextAlign = ContentAlignment.MiddleLeft
        };
        numBackupKeep = new NumericUpDown
        {
            Location = new Point(150, 80), Size = new Size(80, 24), Minimum = 1, Maximum = 100, Value = 10
        };

        btnCreateBackupNow = new Button { Text = "Backup Now", Size = new Size(110, 26), Location = new Point(330, 49) };
        grpBackup.Controls.AddRange([chkAutoBackup, lblBkpInterval, cmbBackupInterval,
            lblKeep, numBackupKeep, btnCreateBackupNow]);
        scroll.Controls.Add(grpBackup);
        y += 120;

        // Discord
        var grpDiscord = MakeGroupBox("Discord Webhook Notifications", 8, y, 580, 190);
        chkDiscordEnabled = new CheckBox { Text = "Enable Discord webhook", AutoSize = true, Location = new Point(8, 22) };
        var lblUrl = new Label { Text = "Webhook URL:", AutoSize = true, Location = new Point(8, 52) };
        txtWebhookUrl  = new TextBox { Location = new Point(110, 49), Size = new Size(320, 24) };
        btnTestWebhook = new Button  { Text = "Test", Size = new Size(60, 24), Location = new Point(438, 49) };
        var lblNotify = new Label { Text = "Notify on:", AutoSize = true, Location = new Point(8, 84) };
        chkNotifyStart   = new CheckBox { Text = "Server Start",  AutoSize = true, Location = new Point(8,   104), Checked = true };
        chkNotifyStop    = new CheckBox { Text = "Server Stop",   AutoSize = true, Location = new Point(130, 104), Checked = true };
        chkNotifyCrash   = new CheckBox { Text = "Crash",         AutoSize = true, Location = new Point(260, 104), Checked = true };
        chkNotifyRestart = new CheckBox { Text = "Restart",       AutoSize = true, Location = new Point(8,   128), Checked = true };
        chkNotifyBackup  = new CheckBox { Text = "Backup Created",AutoSize = true, Location = new Point(130, 128) };
        grpDiscord.Controls.AddRange([chkDiscordEnabled, lblUrl, txtWebhookUrl, btnTestWebhook,
            lblNotify, chkNotifyStart, chkNotifyStop, chkNotifyCrash, chkNotifyRestart, chkNotifyBackup]);
        scroll.Controls.Add(grpDiscord);

        tabAutomation.Controls.Add(scroll);
    }

    // ── Backups Tab ────────────────────────────────────────────────────

    private void BuildBackupsTab()
    {
        var pnlTop = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 6, 8, 0) };
        btnCreateBackup      = new Button { Text = "Create Backup", Size = new Size(120, 28), Location = new Point(8,   6) };
        btnRestoreBackup     = new Button { Text = "Restore",       Size = new Size(90,  28), Location = new Point(136, 6) };
        btnDeleteBackup      = new Button { Text = "Delete",        Size = new Size(90,  28), Location = new Point(234, 6), Tag = "danger" };
        btnOpenBackupsFolder = new Button { Text = "Open Folder",   Size = new Size(100, 28), Location = new Point(340, 6) };
        lblBackupCount       = new Label  { AutoSize = true,        Location = new Point(460, 12) };
        pnlTop.Controls.AddRange([btnCreateBackup, btnRestoreBackup, btnDeleteBackup, btnOpenBackupsFolder, lblBackupCount]);

        dgvBackups = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        dgvBackups.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFile", HeaderText = "File Name", FillWeight = 60 });
        dgvBackups.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDate", HeaderText = "Date",      FillWeight = 25 });
        dgvBackups.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSize", HeaderText = "Size",      FillWeight = 15 });

        tabBackups.Controls.Add(dgvBackups);
        tabBackups.Controls.Add(pnlTop);
    }

    // ── About Tab ─────────────────────────────────────────────────────

    private void BuildAboutTab()
    {
        var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24) };

        var lblTitle = new Label
        {
            Text     = "Windrose Server Manager",
            Font     = new Font("Segoe UI", 18f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 32)
        };
        var lblSub = new Label
        {
            Text     = "Dedicated server launcher for Windrose (Steam App ID: 4129620)",
            Font     = new Font("Segoe UI", 10f),
            AutoSize = true,
            Location = new Point(24, 72)
        };
        var lblVersion = new Label { Text = $"Version: {Application.ProductVersion}", AutoSize = true, Location = new Point(24, 104) };
        var lblDotNet  = new Label
        {
            Text     = $".NET Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}",
            AutoSize = true,
            Location = new Point(24, 128)
        };
        var lblOs = new Label
        {
            Text     = $"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}",
            AutoSize = true,
            Location = new Point(24, 152)
        };
        var lblPaths = new Label
        {
            Text =
                "Server exe:      ServerFiles\\WindroseServer.exe  (-log)\n" +
                "Server config:   ServerFiles\\R5\\ServerDescription.json\n" +
                "Save data:       ServerFiles\\R5\\Saved\\SaveProfiles\\",
            Font      = new Font("Consolas", 9f),
            AutoSize  = false,
            Size      = new Size(700, 58),
            Location  = new Point(24, 188),
            ForeColor = Color.Gray
        };

        pnl.Controls.AddRange([lblTitle, lblSub, lblVersion, lblDotNet, lblOs, lblPaths]);
        tabAbout.Controls.Add(pnl);
    }

    // ── Tooltips ──────────────────────────────────────────────────────

    private void BuildTooltips()
    {
        toolTip = new ToolTip
        {
            AutoPopDelay = 8000,
            InitialDelay = 500,
            ReshowDelay  = 300,
            ShowAlways   = true
        };

        // ── Dashboard ────────────────────────────────────────────────
        toolTip.SetToolTip(btnServerAction, "Install, start, or stop the server depending on current state.");
        toolTip.SetToolTip(btnUpdate,       "Download and apply updates via SteamCMD. Also validates and repairs existing files.");
        toolTip.SetToolTip(btnRestart,      "Stop the server and start it again immediately.");
        toolTip.SetToolTip(chkAutoScroll,   "Keep the console scrolled to the latest output.");
        toolTip.SetToolTip(btnClearConsole,   "Clear all text from the console window.");
        toolTip.SetToolTip(btnRunDiagnostics,
            "Run connectivity checks and show results in the console:\n" +
            "• DNS resolution for Windrose servers (system DNS + Google 8.8.8.8)\n" +
            "• STUN/TURN port 3478 reachability on windrose.support\n" +
            "• IPv4 vs IPv6 priority (game requires IPv4)\n\n" +
            "Use this if players can't find or connect to the server.");

        // ── Settings — Server Config ─────────────────────────────────
        toolTip.SetToolTip(txtInviteCode,
            "Unique code players use to find and join your server.\n" +
            "Generated automatically on first run — you can change it to\n" +
            "something memorable (e.g. \"myCrew1\").\n\n" +
            "Rules: minimum 6 characters, letters (a-z, A-Z) and numbers only.\n" +
            "Case sensitive — players must enter it exactly as written.");
        toolTip.SetToolTip(btnCopyInviteCode,    "Copy the invite code to the clipboard.");
        toolTip.SetToolTip(btnRefreshInviteCode, "Re-read ServerDescription.json to refresh all server config fields.");
        toolTip.SetToolTip(txtServerName,
            "Optional display name for your server.\n" +
            "Useful when multiple servers share similar invite codes.");
        toolTip.SetToolTip(chkPasswordProtected, "Require players to enter a password before joining.");
        toolTip.SetToolTip(txtPassword,          "Password players must enter to connect. Only used when Password Protected is checked.");
        toolTip.SetToolTip(numMaxPlayers,
            "Maximum number of players allowed on the server at the same time.\n" +
            "Up to 4 players is recommended for smoother performance.\n" +
            "Higher counts place progressively more load on CPU and RAM.");
        toolTip.SetToolTip(txtP2pProxy,
            "IP address the server listens on for connections.\n" +
            "0.0.0.0 = all network interfaces (required for VPS / dedicated servers).\n" +
            "127.0.0.1 = localhost only (LAN / same machine).");
        toolTip.SetToolTip(btnWriteServerDesc,
            "Write your changes to ServerDescription.json.\n" +
            "Server must be stopped. Restart it afterwards for changes to take effect.");

        // ── Settings — World Settings ─────────────────────────────────
        toolTip.SetToolTip(txtWorldName,
            "Internal name for this world save file.\n" +
            "Different from Server Name — this labels the save, not the server.\n" +
            "Shown in logs; not visible to players.");
        toolTip.SetToolTip(rdoEasy,         "Use the Easy difficulty preset. Custom multipliers are ignored.");
        toolTip.SetToolTip(rdoMedium,       "Use the Medium difficulty preset. Custom multipliers are ignored.");
        toolTip.SetToolTip(rdoHard,         "Use the Hard difficulty preset. Custom multipliers are ignored.");
        toolTip.SetToolTip(rdoCustomPreset, "Use fully custom multipliers configured below.");
        toolTip.SetToolTip(cmbCombatDifficulty,
            "Controls boss encounter difficulty and general enemy aggression.\n" +
            "Only applies when Difficulty is set to Custom.");
        toolTip.SetToolTip(chkCoopQuests,
            "If any player completes a co-op quest, it auto-completes for all\n" +
            "players who currently have it active.\n" +
            "Only applies when Difficulty is set to Custom.");
        toolTip.SetToolTip(chkImmersiveExploration,
            "Stored as 'EasyExplore' in the config file — the name is inverted.\n" +
            "When CHECKED: disables map markers for points of interest,\n" +
            "making exploration harder (Immersive Exploration mode).\n" +
            "When UNCHECKED: map markers are shown (default).\n" +
            "Only applies when Difficulty is set to Custom.");
        toolTip.SetToolTip(numMobHealth,  "Enemy health multiplier.  Range: 0.20–5.00  (Custom preset only)");
        toolTip.SetToolTip(numMobDamage,  "Enemy damage multiplier.  Range: 0.20–5.00  (Custom preset only)");
        toolTip.SetToolTip(numShipHealth, "Enemy ship health multiplier.  Range: 0.40–5.00  (Custom preset only)");
        toolTip.SetToolTip(numShipDamage, "Enemy ship damage multiplier.  Range: 0.20–2.50  (Custom preset only)");
        toolTip.SetToolTip(numBoarding,
            "Multiplier for number of enemy sailors to defeat in boarding actions.\n" +
            "Range: 0.20–5.00  (Custom preset only)");
        toolTip.SetToolTip(numCoopStats,
            "Adjusts enemy health and posture recovery based on player count.\n" +
            "Range: 0.00–2.00  (Custom preset only)");
        toolTip.SetToolTip(numCoopShips,
            "Adjusts enemy ship health based on player count.\n" +
            "Range: 0.00–2.00  (Custom preset only)");
        toolTip.SetToolTip(btnSaveWorldDesc,
            "Write your changes to WorldDescription.json.\n" +
            "Server must be stopped. Restart it afterwards for changes to take effect.");
        toolTip.SetToolTip(btnRefreshWorldDesc, "Re-read WorldDescription.json to refresh all world settings fields.");

        // ── Settings — Performance ───────────────────────────────────
        toolTip.SetToolTip(cmbProcessPriority,
            "Windows CPU scheduling priority for the server process.\n" +
            "Normal is recommended for shared machines.\n" +
            "High may improve performance on a dedicated server but can starve other processes.");

        // ── Settings — Crash Detection ───────────────────────────────
        toolTip.SetToolTip(chkCrashDetection, "Monitor the server process and detect unexpected exits.");
        toolTip.SetToolTip(chkAutoRestart,    "Automatically restart the server when a crash is detected.");
        toolTip.SetToolTip(numMaxRestarts,
            "How many times to auto-restart before giving up.\n" +
            "Resets to 0 each time the server runs successfully for a sustained period.");

        // ── Settings — Custom Args ───────────────────────────────────
        toolTip.SetToolTip(txtCustomArgs,
            "Extra command-line arguments appended after -log when launching the server.\n" +
            "Leave blank unless you know what you're adding.");

        // ── Settings — Save / Reload ─────────────────────────────────
        toolTip.SetToolTip(btnSaveSettings,   "Save launcher preferences (crash detection, backup, discord, schedule) to windrose_settings.json.");
        toolTip.SetToolTip(btnReloadSettings, "Discard unsaved changes and reload settings from windrose_settings.json.");

        // ── Automation — Scheduled Restarts ──────────────────────────
        toolTip.SetToolTip(chkScheduleEnabled, "Automatically restart the server on a fixed schedule.");
        toolTip.SetToolTip(rdoInterval,        "Restart every N hours from when the server started.");
        toolTip.SetToolTip(rdoFixedTimes,      "Restart at specific times of day (24-hour format).");
        toolTip.SetToolTip(numIntervalHours,   "Hours between automatic restarts.");
        toolTip.SetToolTip(txtFixedTimes,
            "Comma-separated restart times in 24-hour HH:mm format.\n" +
            "Example: 03:00,15:00 restarts at 3 AM and 3 PM.");
        toolTip.SetToolTip(numWarningMins,
            "Send a warning to the console N minutes before a scheduled restart.\n" +
            "Set to 0 to restart without any warning.");

        // ── Automation — Auto Backup ──────────────────────────────────
        toolTip.SetToolTip(chkAutoBackup,      "Automatically back up save data on a schedule.");
        toolTip.SetToolTip(cmbBackupInterval,  "How often to create an automatic backup.");
        toolTip.SetToolTip(numBackupKeep,
            "Number of most recent backups to keep.\n" +
            "Backups beyond this limit are deleted oldest-first.");
        toolTip.SetToolTip(btnCreateBackupNow, "Create a backup of the save data right now.");

        // ── Automation — Discord ──────────────────────────────────────
        toolTip.SetToolTip(chkDiscordEnabled,
            "Send server event notifications to a Discord channel via webhook.");
        toolTip.SetToolTip(txtWebhookUrl,
            "Discord webhook URL.\n" +
            "Get it from your Discord server: Channel Settings → Integrations → Webhooks.");
        toolTip.SetToolTip(btnTestWebhook,     "Send a test message to verify the webhook URL is working.");
        toolTip.SetToolTip(chkNotifyStart,     "Send a notification when the server starts.");
        toolTip.SetToolTip(chkNotifyStop,      "Send a notification when the server stops.");
        toolTip.SetToolTip(chkNotifyCrash,     "Send a notification when the server crashes.");
        toolTip.SetToolTip(chkNotifyRestart,   "Send a notification on scheduled or crash-recovery restarts.");
        toolTip.SetToolTip(chkNotifyBackup,    "Send a notification when an automatic backup is created.");

        // ── Settings — Advanced Network ───────────────────────────────
        toolTip.SetToolTip(numPortMin,
            "Local UDP port range the server binds to (optional override).\n" +
            "0 = use the game's baked-in default.\n\n" +
            "NOTE: The critical port for player connectivity is 3478 (UDP+TCP)\n" +
            "on *.windrose.support — that's the STUN/TURN relay used for NAT\n" +
            "traversal. This range setting is separate and rarely needs changing.\n\n" +
            "If you do set a range, open those UDP ports in your firewall\n" +
            "or VPS security group.");
        toolTip.SetToolTip(numPortMax,
            "Maximum of the local UDP port bind range (optional override).\n" +
            "0 = use the game's baked-in default.\n\n" +
            "The port players actually need reachable is 3478 (UDP+TCP)\n" +
            "on *.windrose.support for STUN/TURN — not this range.");
        toolTip.SetToolTip(chkSecureConnection,
            "Encrypt the connection between players and the server.\n\n" +
            "OFF by default (game default = unencrypted UDP).\n" +
            "With encryption off, packet contents can be captured by Wireshark,\n" +
            "though the proprietary UE5 protocol makes them hard to interpret.\n\n" +
            "Enable for private/competitive servers where data integrity matters.");
        toolTip.SetToolTip(chkRelayOnly,
            "Force all traffic through Windrose's relay servers instead of direct UDP.\n\n" +
            "PRIVACY: In direct mode, player IPs can appear in STUN packets —\n" +
            "someone running Wireshark could see other players' public IPs.\n" +
            "Relay mode hides all player IPs behind the relay server address.\n\n" +
            "TRADE-OFF: Relay adds latency (traffic bounces through Windrose's\n" +
            "servers). Fine for private friend groups; less ideal for low-latency play.\n\n" +
            "Also use this if players can't connect due to strict NAT / firewall.");
        toolTip.SetToolTip(numDisconnectDelay,
            "How many seconds the server stays running after all players disconnect.\n" +
            "0 = use the game's baked-in default.\n\n" +
            "Set to a higher value (e.g. 300 = 5 min) if you want the server\n" +
            "to stay up so players can quickly reconnect after a disconnect.");
        toolTip.SetToolTip(numOwnerTimeout,
            "How many seconds the server waits for the first player to connect\n" +
            "after startup before giving up.\n" +
            "0 = use the game's baked-in default.");
        toolTip.SetToolTip(btnSaveGameIni,
            "Write these settings to R5\\Saved\\Config\\WindowsServer\\Game.ini.\n" +
            "Keys at 0 / default are omitted — the game uses its own defaults.\n" +
            "Restart the server for changes to take effect.");
        toolTip.SetToolTip(btnResetGameIni,
            "Delete Game.ini — the game uses its baked-in defaults for all\n" +
            "network settings (unencrypted, direct connections, engine port range).");

        // ── Backups ───────────────────────────────────────────────────
        toolTip.SetToolTip(btnCreateBackup,      "Create a zip backup of the save data right now.");
        toolTip.SetToolTip(btnRestoreBackup,
            "Restore the selected backup. A safety backup of the current\n" +
            "save data is created first. Server must be stopped.");
        toolTip.SetToolTip(btnDeleteBackup,      "Permanently delete the selected backup. Cannot be undone.");
        toolTip.SetToolTip(btnOpenBackupsFolder, "Open the backups folder in Windows Explorer.");
    }

    // ── Layout Helpers ────────────────────────────────────────────────

    private static GroupBox MakeGroupBox(string title, int x, int y, int w, int h) =>
        new() { Text = title, Location = new Point(x, y), Size = new Size(w, h) };

    private static void AddLabeledNumeric(GroupBox grp, string label, int x, int y,
        out NumericUpDown num, int min, int max, int val)
    {
        var lbl = new Label { Text = label, AutoSize = true, Location = new Point(x, y + 4) };
        int lblW = TextRenderer.MeasureText(label, SystemFonts.DefaultFont).Width + 6;
        num = new NumericUpDown
        {
            Location = new Point(x + lblW, y),
            Size     = new Size(80, 24),
            Minimum  = min, Maximum = max, Value = val
        };
        grp.Controls.AddRange([lbl, num]);
    }

    private static void AddLabeledFloat(GroupBox grp, string label, int x, int y,
        out NumericUpDown num, decimal min, decimal max, decimal val)
    {
        var lbl = new Label { Text = label, AutoSize = true, Location = new Point(x, y + 4) };
        int lblW = TextRenderer.MeasureText(label, SystemFonts.DefaultFont).Width + 6;
        num = new NumericUpDown
        {
            Location      = new Point(x + lblW, y),
            Size          = new Size(80, 24),
            DecimalPlaces = 2,
            Increment     = 0.05m,
            Minimum       = min,
            Maximum       = max,
            Value         = val
        };
        grp.Controls.AddRange([lbl, num]);
    }

    /// <summary>
    /// Fixed-column multiplier row for World Settings — avoids font-measurement drift.
    /// Label is 105 px wide; NUD starts at x+107 and is 90 px wide.
    /// </summary>
    private static void AddWorldMultiplier(GroupBox grp, string label, int x, int y,
        out NumericUpDown num, decimal min, decimal max, decimal val)
    {
        var lbl = new Label
        {
            Text      = label,
            AutoSize  = false,
            Size      = new Size(105, 20),
            Location  = new Point(x, y + 2),
            TextAlign = ContentAlignment.MiddleLeft
        };
        num = new NumericUpDown
        {
            Location      = new Point(x + 107, y),
            Size          = new Size(90, 24),
            DecimalPlaces = 2,
            Increment     = 0.05m,
            Minimum       = min,
            Maximum       = max,
            Value         = val
        };
        grp.Controls.AddRange([lbl, num]);
    }
}
