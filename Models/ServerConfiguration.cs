namespace WindroseServerManager.Models;

public class ServerConfiguration
{
    // ── ServerDescription.json fields ────────────────────────────────
    // Written to R5\ServerDescription.json (only read by server on startup)
    public string ServerName        { get; set; } = "Windrose Server";
    public string InviteCode        { get; set; } = "";        // read-only — set by server on first run
    public bool   IsPasswordProtected { get; set; } = false;
    public string Password          { get; set; } = "";
    public int    MaxPlayerCount    { get; set; } = 8;
    public string P2pProxyAddress   { get; set; } = "0.0.0.0";

    // ── Performance ──────────────────────────────────────────────────
    public string ProcessPriority { get; set; } = "Normal";

    // ── Crash Detection ──────────────────────────────────────────────
    public bool EnableCrashDetection { get; set; } = true;
    public bool AutoRestart          { get; set; } = true;
    public int  MaxRestartAttempts   { get; set; } = 3;

    // ── Scheduled Restarts ───────────────────────────────────────────
    public bool   ScheduledRestartEnabled { get; set; } = false;
    public bool   UseFixedRestartTimes    { get; set; } = false;
    public int    RestartIntervalHours    { get; set; } = 6;
    public string FixedRestartTimes       { get; set; } = "03:00,15:00";
    public int    RestartWarningMinutes   { get; set; } = 10;

    // ── Discord Webhooks ─────────────────────────────────────────────
    public bool   EnableDiscordWebhook { get; set; } = false;
    public string DiscordWebhookUrl    { get; set; } = "";
    public bool   NotifyOnStart        { get; set; } = true;
    public bool   NotifyOnStop         { get; set; } = true;
    public bool   NotifyOnCrash        { get; set; } = true;
    public bool   NotifyOnRestart      { get; set; } = true;
    public bool   NotifyOnBackup       { get; set; } = false;

    // ── Auto Backup ──────────────────────────────────────────────────
    public bool AutoBackupEnabled   { get; set; } = false;
    public int  BackupIntervalHours { get; set; } = 6;
    public int  BackupKeepCount     { get; set; } = 10;

    // ── App / Launch ─────────────────────────────────────────────────
    public string CustomLaunchArgs { get; set; } = "";
}
