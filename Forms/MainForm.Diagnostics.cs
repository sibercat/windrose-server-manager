using System.Text.RegularExpressions;

namespace WindroseServerManager.Forms;

partial class MainForm
{
    private const string DiagDomain = "r5coopapigateway-eu-release.windrose.support";
    private const string StunDomain = "windrose.support";
    private const int    StunPort   = 3478;

    private async Task RunDiagnosticsAsync()
    {
        this.InvokeIfRequired(() => btnRunDiagnostics.Enabled = false);

        DiagLine("");
        DiagLine("══════════════════════════════════════════════════════", Color.FromArgb(70, 70, 70));
        DiagLine("  Network Diagnostics", Color.FromArgb(255, 185, 0));
        DiagLine("══════════════════════════════════════════════════════", Color.FromArgb(70, 70, 70));

        await DiagDnsSystemAsync();
        await DiagDnsGoogleAsync();
        await DiagPortAsync();
        await DiagIpv6Async();

        DiagLine("══════════════════════════════════════════════════════", Color.FromArgb(70, 70, 70));
        DiagLine("");

        this.InvokeIfRequired(() => btnRunDiagnostics.Enabled = true);
    }

    // ── DNS via system resolver ───────────────────────────────────────

    private async Task DiagDnsSystemAsync()
    {
        DiagLine($"[DNS] System DNS → {DiagDomain}", Color.FromArgb(160, 160, 160));
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(DiagDomain);
            var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            var ipv6 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6);

            if (ipv4 != null)
            {
                if (ipv4.ToString() == "127.0.0.1")
                    DiagFail("  ✗ Resolved to 127.0.0.1 — DNS spoofed by ISP or VPN. Disable VPN / check router.");
                else
                    DiagPass($"  ✓ Resolved: {ipv4}  (IPv4 — OK)");
            }
            else if (ipv6 != null)
            {
                DiagFail($"  ✗ Only IPv6 returned: {ipv6}");
                DiagWarn("    → Game requires IPv4. See IPv6 check below.");
            }
            else
            {
                DiagFail("  ✗ No addresses returned.");
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound)
        {
            DiagFail("  ✗ Domain not found — ISP may be blocking this domain.");
            DiagWarn("    → Try switching to Google DNS (8.8.8.8) or check the result below.");
        }
        catch (Exception ex)
        {
            DiagFail($"  ✗ Error: {ex.Message}");
        }
    }

    // ── DNS via Google 8.8.8.8 ───────────────────────────────────────

    private async Task DiagDnsGoogleAsync()
    {
        DiagLine($"[DNS] Google DNS (8.8.8.8) → {DiagDomain}", Color.FromArgb(160, 160, 160));
        try
        {
            string output = await RunDiagProcessAsync("nslookup",
                $"{DiagDomain} 8.8.8.8", timeoutMs: 8000);

            bool notFound   = output.Contains("can't find") || output.Contains("Non-existent domain");
            bool timedOut   = output.Contains("timed out", StringComparison.OrdinalIgnoreCase);
            bool spoofed    = output.Contains("127.0.0.1");
            bool resolved   = output.Contains("Name:") && !notFound && !timedOut;

            if (spoofed)
                DiagFail("  ✗ Resolved to 127.0.0.1 via Google DNS — DNS spoofing confirmed.");
            else if (notFound)
                DiagFail("  ✗ Not found via Google DNS — possible Windrose server outage or global block.");
            else if (timedOut)
                DiagFail("  ✗ Request timed out — firewall may be blocking DNS queries.");
            else if (resolved)
                DiagPass("  ✓ Resolved via Google DNS (OK)");
            else
                DiagWarn("  ? Unexpected nslookup output — check manually.");
        }
        catch (Exception ex)
        {
            DiagFail($"  ✗ nslookup error: {ex.Message}");
        }
    }

    // ── STUN/TURN port 3478 ──────────────────────────────────────────

    private async Task DiagPortAsync()
    {
        DiagLine($"[PORT] STUN/TURN {StunDomain}:{StunPort} TCP", Color.FromArgb(160, 160, 160));
        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(StunDomain, StunPort);
            bool reachable  = await Task.WhenAny(connectTask, Task.Delay(5000)) == connectTask
                              && !connectTask.IsFaulted;

            if (reachable)
            {
                DiagPass($"  ✓ Port {StunPort} TCP reachable — STUN/TURN traffic is not blocked");
            }
            else
            {
                DiagFail($"  ✗ Port {StunPort} TCP unreachable — ISP may be blocking STUN/TURN traffic.");
                DiagWarn($"    → To fix: ask ISP to whitelist *.windrose.support port {StunPort} UDP+TCP");
                DiagWarn( "    → Quick test: try connecting via VPN — if that works, ISP is the cause.");
            }
        }
        catch (Exception ex)
        {
            DiagFail($"  ✗ {ex.Message}");
        }
    }

    // ── IPv6 priority ────────────────────────────────────────────────

    private async Task DiagIpv6Async()
    {
        DiagLine("[IPv6] Checking IP protocol priority...", Color.FromArgb(160, 160, 160));
        try
        {
            string output = await RunDiagProcessAsync("netsh",
                "interface ipv6 show prefixpolicies", timeoutMs: 5000);

            int ipv4Prec = ParsePrecedence(output, "::ffff:0:0/96"); // IPv4-mapped
            int ipv6Prec = ParsePrecedence(output, "::/0");           // IPv6 default

            if (ipv4Prec < 0 || ipv6Prec < 0)
            {
                DiagWarn("  ? Could not parse prefix policy table — run manually: netsh interface ipv6 show prefixpolicies");
                return;
            }

            if (ipv4Prec >= ipv6Prec)
            {
                DiagPass($"  ✓ IPv4 preferred (precedence {ipv4Prec} vs IPv6 {ipv6Prec}) — OK");
            }
            else
            {
                DiagFail($"  ✗ IPv6 has higher priority ({ipv6Prec}) than IPv4 ({ipv4Prec}) — may cause connection failures.");
                DiagWarn( "    → Game requires IPv4. To fix, run this in an admin Command Prompt:");
                DiagWarn( "      reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip6\\Parameters\"");
                DiagWarn( "             /v DisabledComponents /t REG_DWORD /d 32 /f");
                DiagWarn( "    → Then restart your PC. IPv6 stays enabled but IPv4 gets priority.");
            }
        }
        catch (Exception ex)
        {
            DiagFail($"  ✗ {ex.Message}");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static int ParsePrecedence(string netshOutput, string prefix)
    {
        foreach (var line in netshOutput.Split('\n'))
        {
            if (!line.Contains(prefix)) continue;
            var parts = line.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1 && int.TryParse(parts[0], out int prec))
                return prec;
        }
        return -1;
    }

    private static async Task<string> RunDiagProcessAsync(string exe, string args, int timeoutMs = 10000)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true
        };

        using var proc = Process.Start(psi) ?? throw new Exception($"Failed to start {exe}");

        var outTask = proc.StandardOutput.ReadToEndAsync();
        var errTask = proc.StandardError.ReadToEndAsync();

        bool finished = await Task.Run(() => proc.WaitForExit(timeoutMs));
        if (!finished) { proc.Kill(); throw new TimeoutException($"{exe} timed out."); }

        return await outTask + await errTask;
    }

    private void DiagPass(string msg) => AppendConsole(msg, ThemeManager.StateRunning);
    private void DiagFail(string msg) => AppendConsole(msg, ThemeManager.StateCrashed);
    private void DiagWarn(string msg) => AppendConsole(msg, Color.FromArgb(255, 185, 0));
    private void DiagLine(string msg,  Color c)  => AppendConsole(msg, c);
    private void DiagLine(string msg)             => AppendConsole(msg, Color.FromArgb(80, 80, 80));
}
