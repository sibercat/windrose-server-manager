using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;

namespace WindroseServerManager.Forms;

partial class MainForm
{
    private static readonly HttpClient _updateHttp = new();

    private async Task CheckForUpdateAsync()
    {
        try
        {
            _updateHttp.DefaultRequestHeaders.UserAgent.TryParseAdd("WindroseServerManager-UpdateCheck/1.0");

            string json = await _updateHttp.GetStringAsync(
                "https://api.github.com/repos/sibercat/windrose-server-manager/releases/latest");

            var match = Regex.Match(json, @"""tag_name""\s*:\s*""([^""]+)""");
            if (!match.Success) return;

            string tag = match.Groups[1].Value.TrimStart('v', 'V');
            if (!Version.TryParse(tag, out var latest)) return;

            var current = Assembly.GetExecutingAssembly().GetName().Version;
            if (current == null || latest <= current) return;

            this.InvokeIfRequired(() =>
            {
                tsUpdateAvailable.Text    = $"  ⬆ Update available: v{latest.Major}.{latest.Minor}.{latest.Build}  ";
                tsUpdateAvailable.Visible = true;
                tsUpdateAvailable.Click  += (_, _) =>
                    Process.Start(new ProcessStartInfo(
                        "https://github.com/sibercat/windrose-server-manager/releases")
                    { UseShellExecute = true });
            });
        }
        catch { /* silent — no internet, API rate limit, etc. */ }
    }
}
