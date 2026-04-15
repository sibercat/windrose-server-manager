namespace WindroseServerManager.Services;

/// <summary>
/// Manages two separate configs:
///   1. windrose_settings.json  — launcher preferences (crash detection, backup, discord, etc.)
///   2. R5\ServerDescription.json — Windrose server config read by the game on startup
/// </summary>
public class ConfigurationManager
{
    private readonly string _rootDir;
    private readonly FileLogger _logger;

    // ── Paths ────────────────────────────────────────────────────────
    private string SettingsFile          => Path.Combine(_rootDir, "windrose_settings.json");
    public  string ServerFilesDir        => Path.Combine(_rootDir, "ServerFiles");
    public  string R5Dir                 => Path.Combine(ServerFilesDir, "R5");
    public  string ServerDescriptionPath => Path.Combine(R5Dir, "ServerDescription.json");
    /// <summary>World save data — backed up by BackupService.</summary>
    public  string SaveDataPath          => Path.Combine(R5Dir, "Saved", "SaveProfiles");
    /// <summary>UE5 ini override file for R5CoopProxySettings (ports, relay, timeouts).</summary>
    public  string GameIniPath           => Path.Combine(R5Dir, "Saved", "Config", "WindowsServer", "Game.ini");

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() }
    };

    public ConfigurationManager(string rootDir, FileLogger logger)
    {
        _rootDir = rootDir;
        _logger  = logger;
    }

    // ── Launcher Settings ────────────────────────────────────────────

    public ServerConfiguration LoadSettings()
    {
        if (!File.Exists(SettingsFile)) return new ServerConfiguration();
        try
        {
            string json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<ServerConfiguration>(json, _jsonOpts)
                   ?? new ServerConfiguration();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to load launcher settings, using defaults.", ex);
            return new ServerConfiguration();
        }
    }

    public void SaveSettings(ServerConfiguration cfg)
    {
        try
        {
            Directory.CreateDirectory(_rootDir);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(cfg, _jsonOpts));
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to save launcher settings.", ex);
        }
    }

    // ── ServerDescription.json ───────────────────────────────────────

    /// <summary>
    /// Reads the user-editable fields from ServerDescription.json.
    /// Returns null if the file does not exist yet (server hasn't been run once).
    /// </summary>
    public ServerDescriptionData? ReadServerDescription()
    {
        if (!File.Exists(ServerDescriptionPath)) return null;
        try
        {
            using var doc   = JsonDocument.Parse(File.ReadAllText(ServerDescriptionPath));
            var persistent  = doc.RootElement.GetProperty("ServerDescription_Persistent");

            return new ServerDescriptionData
            {
                PersistentServerId  = GetString(persistent, "PersistentServerId"),
                InviteCode          = GetString(persistent, "InviteCode"),
                IsPasswordProtected = persistent.TryGetProperty("IsPasswordProtected", out var ipv) && ipv.GetBoolean(),
                Password            = GetString(persistent, "Password"),
                ServerName          = GetString(persistent, "ServerName"),
                WorldIslandId       = GetString(persistent, "WorldIslandId"),
                MaxPlayerCount      = persistent.TryGetProperty("MaxPlayerCount", out var mc) ? mc.GetInt32() : 8,
                P2pProxyAddress     = GetString(persistent, "P2pProxyAddress"),
            };
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to read ServerDescription.json.", ex);
            return null;
        }
    }

    /// <summary>
    /// Writes the user-editable fields back to ServerDescription.json,
    /// preserving all other fields (Version, DeploymentId, PersistentServerId, WorldIslandId, InviteCode).
    /// The server must be stopped before calling this.
    /// </summary>
    public bool WriteServerDescription(ServerDescriptionData data)
    {
        if (!File.Exists(ServerDescriptionPath))
        {
            _logger.Warning("ServerDescription.json not found — start the server once to generate it.");
            return false;
        }
        try
        {
            // Parse the whole file as a mutable dictionary so we preserve unknown fields
            var root = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                File.ReadAllText(ServerDescriptionPath))
                ?? throw new Exception("Failed to parse ServerDescription.json");

            // Rebuild ServerDescription_Persistent with updated values
            var persistent = new Dictionary<string, object?>
            {
                ["PersistentServerId"]  = data.PersistentServerId,
                ["InviteCode"]          = data.InviteCode,
                ["IsPasswordProtected"] = data.IsPasswordProtected,
                ["Password"]            = data.Password,
                ["ServerName"]          = data.ServerName,
                ["WorldIslandId"]       = data.WorldIslandId,
                ["MaxPlayerCount"]      = data.MaxPlayerCount,
                ["P2pProxyAddress"]     = data.P2pProxyAddress,
            };

            // Write a new JSON document preserving Version and DeploymentId
            var output = new Dictionary<string, object?>
            {
                ["Version"]                      = root.TryGetValue("Version", out var v) ? v.GetInt32() : 1,
                ["DeploymentId"]                 = root.TryGetValue("DeploymentId", out var d) ? d.GetString() : "",
                ["ServerDescription_Persistent"] = persistent
            };

            var writeOpts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(ServerDescriptionPath, JsonSerializer.Serialize(output, writeOpts));
            _logger.Info("ServerDescription.json updated.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to write ServerDescription.json.", ex);
            return false;
        }
    }

    // ── Game.ini (R5CoopProxySettings overrides) ─────────────────────

    private const string CoopSection = "/Script/R5CoopProxy.R5CoopProxySettings";

    /// <summary>
    /// Reads the Game.ini override file. Returns defaults when the file does not exist
    /// (the game runs fine without it — baked-in pak defaults apply).
    /// </summary>
    public GameIniData ReadGameIni()
    {
        var data = new GameIniData();
        if (!File.Exists(GameIniPath)) return data;
        try
        {
            string? section = null;
            foreach (var raw in File.ReadAllLines(GameIniPath))
            {
                var line = raw.Trim();
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    section = line[1..^1];
                    continue;
                }
                if (section != CoopSection) continue;
                var eq = line.IndexOf('=');
                if (eq < 0) continue;
                var key = line[..eq].Trim();
                var val = line[(eq + 1)..].Trim();
                switch (key)
                {
                    case "P2pLocalPortMin":
                        if (int.TryParse(val, out var pmin)) data.P2pLocalPortMin = pmin; break;
                    case "P2pLocalPortMax":
                        if (int.TryParse(val, out var pmax)) data.P2pLocalPortMax = pmax; break;
                    case "IsP2pRelayOnly":
                        data.IsP2pRelayOnly = val.Equals("True", StringComparison.OrdinalIgnoreCase); break;
                    case "IsP2pSecureConnection":
                        data.IsP2pSecureConnection = val.Equals("True", StringComparison.OrdinalIgnoreCase); break;
                    case "StopHostServerAfterOwnerDisconnectDelay":
                        data.DisconnectDelaySecs = ParseTimespanSecs(val); break;
                    case "OwnerConnectionTimeout":
                        data.OwnerTimeoutSecs = ParseTimespanSecs(val); break;
                }
            }
        }
        catch (Exception ex) { _logger.Error("Failed to read Game.ini.", ex); }
        return data;
    }

    /// <summary>
    /// Writes only the keys that differ from "use game default" (i.e. non-zero ports/timeouts
    /// and explicit bool overrides). Deletes the file if nothing needs overriding.
    /// </summary>
    public bool WriteGameIni(GameIniData data)
    {
        try
        {
            var lines = new List<string> { $"[{CoopSection}]" };

            if (data.P2pLocalPortMin > 0) lines.Add($"P2pLocalPortMin={data.P2pLocalPortMin}");
            if (data.P2pLocalPortMax > 0) lines.Add($"P2pLocalPortMax={data.P2pLocalPortMax}");
            if (data.IsP2pRelayOnly)        lines.Add("IsP2pRelayOnly=True");
            if (data.IsP2pSecureConnection) lines.Add("IsP2pSecureConnection=True");
            if (data.DisconnectDelaySecs > 0)
                lines.Add($"StopHostServerAfterOwnerDisconnectDelay={FormatTimespan(data.DisconnectDelaySecs)}");
            if (data.OwnerTimeoutSecs > 0)
                lines.Add($"OwnerConnectionTimeout={FormatTimespan(data.OwnerTimeoutSecs)}");

            // If only the section header remains the file would do nothing — skip creating it
            if (lines.Count == 1)
            {
                if (File.Exists(GameIniPath)) File.Delete(GameIniPath);
                _logger.Info("Game.ini: nothing to override — file removed.");
                return true;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(GameIniPath)!);
            File.WriteAllLines(GameIniPath, lines);
            _logger.Info("Game.ini updated.");
            return true;
        }
        catch (Exception ex) { _logger.Error("Failed to write Game.ini.", ex); return false; }
    }

    public bool DeleteGameIni()
    {
        try
        {
            if (File.Exists(GameIniPath)) File.Delete(GameIniPath);
            _logger.Info("Game.ini deleted — game will use baked-in defaults.");
            return true;
        }
        catch (Exception ex) { _logger.Error("Failed to delete Game.ini.", ex); return false; }
    }

    // UE5 stores FTimespan as hh:mm:ss (or d.hh:mm:ss) in ini files.
    private static int ParseTimespanSecs(string val)
    {
        if (TimeSpan.TryParse(val, out var ts)) return (int)ts.TotalSeconds;
        return 0;
    }

    private static string FormatTimespan(int totalSecs)
    {
        var ts = TimeSpan.FromSeconds(totalSecs);
        return ts.Days > 0
            ? $"{ts.Days}.{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    // ── WorldDescription.json ────────────────────────────────────────

    /// <summary>
    /// Finds the WorldDescription.json for a given world ID by scanning RocksDB version folders.
    /// Returns null if the file doesn't exist (server hasn't created the world yet).
    /// </summary>
    public string? FindWorldDescriptionPath(string worldIslandId)
    {
        if (string.IsNullOrEmpty(worldIslandId)) return null;
        string worldsBase = Path.Combine(R5Dir, "Saved", "SaveProfiles", "Default", "RocksDB");
        if (!Directory.Exists(worldsBase)) return null;
        foreach (var versionDir in Directory.GetDirectories(worldsBase))
        {
            var p = Path.Combine(versionDir, "Worlds", worldIslandId, "WorldDescription.json");
            if (File.Exists(p)) return p;
        }
        return null;
    }

    public WorldDescriptionData? ReadWorldDescription(string worldIslandId)
    {
        string? path = FindWorldDescriptionPath(worldIslandId);
        if (path == null) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var wd = doc.RootElement.GetProperty("WorldDescription");

            var data = new WorldDescriptionData
            {
                IslandId        = GetString(wd, "islandId"),
                WorldName       = GetString(wd, "WorldName"),
                WorldPresetType = GetString(wd, "WorldPresetType"),
            };

            if (wd.TryGetProperty("WorldSettings", out var ws))
            {
                if (ws.TryGetProperty("BoolParameters", out var bools))
                {
                    data.CoopSharedQuests     = GetBoolParam(bools,  "WDS.Parameter.Coop.SharedQuests", true);
                    data.ImmersiveExploration = GetBoolParam(bools,  "WDS.Parameter.EasyExplore",       false);
                }
                if (ws.TryGetProperty("FloatParameters", out var floats))
                {
                    data.MobHealthMultiplier             = GetFloatParam(floats, "WDS.Parameter.MobHealthMultiplier",             1.0);
                    data.MobDamageMultiplier             = GetFloatParam(floats, "WDS.Parameter.MobDamageMultiplier",             1.0);
                    data.ShipHealthMultiplier            = GetFloatParam(floats, "WDS.Parameter.ShipsHealthMultiplier",           1.0);
                    data.ShipDamageMultiplier            = GetFloatParam(floats, "WDS.Parameter.ShipsDamageMultiplier",           1.0);
                    data.BoardingDifficultyMultiplier    = GetFloatParam(floats, "WDS.Parameter.BoardingDifficultyMultiplier",    1.0);
                    data.CoopStatsCorrectionModifier     = GetFloatParam(floats, "WDS.Parameter.Coop.StatsCorrectionModifier",    1.0);
                    data.CoopShipStatsCorrectionModifier = GetFloatParam(floats, "WDS.Parameter.Coop.ShipStatsCorrectionModifier", 0.0);
                }
                if (ws.TryGetProperty("TagParameters", out var tags))
                {
                    data.CombatDifficulty = GetTagParam(tags, "WDS.Parameter.CombatDifficulty",
                                                        "WDS.Parameter.CombatDifficulty.", "Normal");
                }
            }

            return data;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to read WorldDescription.json.", ex);
            return null;
        }
    }

    public bool WriteWorldDescription(string worldIslandId, WorldDescriptionData data)
    {
        string? path = FindWorldDescriptionPath(worldIslandId);
        if (path == null)
        {
            _logger.Warning("WorldDescription.json not found — start the server once to generate it.");
            return false;
        }
        try
        {
            using var existingDoc = JsonDocument.Parse(File.ReadAllText(path));
            var existingWd   = existingDoc.RootElement.GetProperty("WorldDescription");
            double creTime   = existingWd.TryGetProperty("CreationTime", out var ct) ? ct.GetDouble() : 0;
            int    version   = existingDoc.RootElement.TryGetProperty("Version", out var v) ? v.GetInt32() : 1;

            // Always write all WorldSettings so values are preserved across preset switches
            var output = new
            {
                Version = version,
                WorldDescription = new
                {
                    islandId        = data.IslandId,
                    WorldName       = data.WorldName,
                    CreationTime    = creTime,
                    WorldPresetType = data.WorldPresetType,
                    WorldSettings   = new
                    {
                        BoolParameters = new Dictionary<string, bool>
                        {
                            ["{\"TagName\": \"WDS.Parameter.Coop.SharedQuests\"}"] = data.CoopSharedQuests,
                            ["{\"TagName\": \"WDS.Parameter.EasyExplore\"}"]       = data.ImmersiveExploration,
                        },
                        FloatParameters = new Dictionary<string, double>
                        {
                            ["{\"TagName\": \"WDS.Parameter.MobHealthMultiplier\"}"]             = Math.Round(data.MobHealthMultiplier,            2),
                            ["{\"TagName\": \"WDS.Parameter.MobDamageMultiplier\"}"]             = Math.Round(data.MobDamageMultiplier,            2),
                            ["{\"TagName\": \"WDS.Parameter.ShipsHealthMultiplier\"}"]           = Math.Round(data.ShipHealthMultiplier,           2),
                            ["{\"TagName\": \"WDS.Parameter.ShipsDamageMultiplier\"}"]           = Math.Round(data.ShipDamageMultiplier,           2),
                            ["{\"TagName\": \"WDS.Parameter.BoardingDifficultyMultiplier\"}"]    = Math.Round(data.BoardingDifficultyMultiplier,   2),
                            ["{\"TagName\": \"WDS.Parameter.Coop.StatsCorrectionModifier\"}"]   = Math.Round(data.CoopStatsCorrectionModifier,    2),
                            ["{\"TagName\": \"WDS.Parameter.Coop.ShipStatsCorrectionModifier\"}"] = Math.Round(data.CoopShipStatsCorrectionModifier, 2),
                        },
                        TagParameters = new Dictionary<string, object>
                        {
                            ["{\"TagName\": \"WDS.Parameter.CombatDifficulty\"}"] =
                                new { TagName = $"WDS.Parameter.CombatDifficulty.{data.CombatDifficulty}" }
                        }
                    }
                }
            };

            var writeOpts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(output, writeOpts));
            _logger.Info("WorldDescription.json updated.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to write WorldDescription.json.", ex);
            return false;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string GetString(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) ? (p.GetString() ?? "") : "";

    // Use Contains so we're resilient to JSON key formatting variations
    // (spacing, escaping) between game versions.

    private static bool GetBoolParam(JsonElement el, string tagName, bool def)
    {
        foreach (var prop in el.EnumerateObject())
            if (prop.Name.Contains(tagName)) return prop.Value.GetBoolean();
        return def;
    }

    private static double GetFloatParam(JsonElement el, string tagName, double def)
    {
        foreach (var prop in el.EnumerateObject())
            if (prop.Name.Contains(tagName)) return prop.Value.GetDouble();
        return def;
    }

    private static string GetTagParam(JsonElement el, string tagName, string prefix, string def)
    {
        foreach (var prop in el.EnumerateObject())
        {
            if (prop.Name.Contains(tagName) && prop.Value.TryGetProperty("TagName", out var tn))
            {
                string full = tn.GetString() ?? "";
                return full.StartsWith(prefix) ? full[prefix.Length..] : def;
            }
        }
        return def;
    }
}

/// <summary>Fields from ServerDescription.json that the manager can read and write.</summary>
public class ServerDescriptionData
{
    public string PersistentServerId  { get; set; } = "";
    public string InviteCode          { get; set; } = "";
    public bool   IsPasswordProtected { get; set; } = false;
    public string Password            { get; set; } = "";
    public string ServerName          { get; set; } = "";
    public string WorldIslandId       { get; set; } = "";
    public int    MaxPlayerCount      { get; set; } = 8;
    public string P2pProxyAddress     { get; set; } = "0.0.0.0";
}

/// <summary>
/// Overrides written to R5\Saved\Config\WindowsServer\Game.ini
/// under [/Script/R5CoopProxy.R5CoopProxySettings].
/// 0 = "not set" — that key is omitted from the file and the game uses its baked-in default.
/// </summary>
public class GameIniData
{
    /// <summary>Minimum local UDP port. 0 = game default.</summary>
    public int  P2pLocalPortMin      { get; set; } = 0;
    /// <summary>Maximum local UDP port. 0 = game default.</summary>
    public int  P2pLocalPortMax      { get; set; } = 0;
    /// <summary>Force relay-only mode (no direct P2P connections).</summary>
    public bool IsP2pRelayOnly       { get; set; } = false;
    /// <summary>Encrypt P2P connections. False = game default (ZeroConstructor).</summary>
    public bool IsP2pSecureConnection { get; set; } = false;
    /// <summary>Seconds before server stops after all players disconnect. 0 = game default.</summary>
    public int  DisconnectDelaySecs  { get; set; } = 0;
    /// <summary>Seconds to wait for the first player connection. 0 = game default.</summary>
    public int  OwnerTimeoutSecs     { get; set; } = 0;
}

/// <summary>Fields from WorldDescription.json that the manager can read and write.</summary>
public class WorldDescriptionData
{
    // Read-only (preserved on write)
    public string IslandId     { get; set; } = "";

    // Editable
    public string WorldName       { get; set; } = "";
    public string WorldPresetType { get; set; } = "Medium"; // Easy / Medium / Hard / Custom

    // Custom preset settings
    public bool   CoopSharedQuests                { get; set; } = true;
    public bool   ImmersiveExploration            { get; set; } = false;
    public string CombatDifficulty                { get; set; } = "Normal"; // Easy / Normal / Hard
    public double MobHealthMultiplier             { get; set; } = 1.0;
    public double MobDamageMultiplier             { get; set; } = 1.0;
    public double ShipHealthMultiplier            { get; set; } = 1.0;
    public double ShipDamageMultiplier            { get; set; } = 1.0;
    public double BoardingDifficultyMultiplier    { get; set; } = 1.0;
    public double CoopStatsCorrectionModifier     { get; set; } = 1.0;
    public double CoopShipStatsCorrectionModifier { get; set; } = 0.0;
}
