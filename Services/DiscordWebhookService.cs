using System.Net.Http;
using System.Net.Http.Json;

namespace WindroseServerManager.Services;

public class DiscordWebhookService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly FileLogger _logger;

    public DiscordWebhookService(FileLogger logger) => _logger = logger;

    public static bool IsValidUrl(string url) =>
        !string.IsNullOrWhiteSpace(url) &&
        url.StartsWith("https://discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> SendAsync(string webhookUrl, string title, string description,
        int color = 0x007ACC)
    {
        if (!IsValidUrl(webhookUrl)) return false;
        try
        {
            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title,
                        description,
                        color,
                        timestamp = DateTime.UtcNow.ToString("o"),
                        footer    = new { text = "Windrose Server Manager" }
                    }
                }
            };
            var response = await _http.PostAsJsonAsync(webhookUrl, payload);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Discord webhook failed: {ex.Message}");
            return false;
        }
    }

    public Task<bool> NotifyServerStarted(string url, string serverName) =>
        SendAsync(url, "✅ Server Started", $"**{serverName}** is now online.", 0x4EC9B0);

    public Task<bool> NotifyServerStopped(string url, string serverName) =>
        SendAsync(url, "🔴 Server Stopped", $"**{serverName}** has been stopped.", 0x9E9E9E);

    public Task<bool> NotifyCrash(string url, string serverName) =>
        SendAsync(url, "💥 Server Crashed", $"**{serverName}** crashed unexpectedly.", 0xF44336);

    public Task<bool> NotifyRestart(string url, string serverName, string reason) =>
        SendAsync(url, "🔄 Server Restarting", $"**{serverName}** is restarting. Reason: {reason}", 0xFF9800);

    public Task<bool> NotifyBackup(string url, string serverName, string backupFile) =>
        SendAsync(url, "💾 Backup Created",
            $"**{serverName}** backup saved: `{Path.GetFileName(backupFile)}`", 0x9C6BC9);

    public Task<bool> TestAsync(string url) =>
        SendAsync(url, "🔔 Test Notification",
            "Windrose Server Manager webhook is configured correctly!", 0x007ACC);
}
