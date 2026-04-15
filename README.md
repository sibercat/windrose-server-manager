# Windrose Server Manager

A Windows desktop application for managing a [Windrose](https://store.steampowered.com/app/2609200/Windrose/) dedicated server. Built with .NET 10 and WinForms.

## Features

- **Server Control** — Start, stop, and restart your dedicated server with one click
- **SteamCMD Integration** — Install and update server files automatically via SteamCMD (App ID 4129620)
- **Crash Detection & Auto-Restart** — Monitors the server process and automatically restarts it on crash
- **Scheduled Restarts** — Set interval-based or fixed-time restarts with configurable player warnings
- **Automatic Backups** — Scheduled backups of save data with configurable retention (keep last N backups)
- **Manual Backups** — Create, restore, and delete backups on demand
- **World Settings** — Edit `WorldDescription.json` directly from the UI (difficulty presets, combat difficulty, all gameplay multipliers)
- **Server Config** — Edit `ServerDescription.json` (server name, password, max players, P2P proxy address)
- **Advanced Network Settings** — Configure `Game.ini` overrides for P2P port range, relay mode, encryption, and server timeout behavior
- **Discord Webhooks** — Notifications for server start, stop, crash, restart, and backup events
- **Process Priority** — Set server process priority (Normal, High, etc.)
- **Dark Theme** — Dark UI throughout

## Requirements

- Windows 10/11 (x64)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [SteamCMD](https://developer.valvesoftware.com/wiki/SteamCMD) (for installing/updating server files)

## Installation

1. Download the latest release from the [Releases](../../releases) page
2. Extract and run `WindroseServerManager.exe`
3. On first launch, point the app at your SteamCMD installation and choose a server install directory
4. Click **Install / Update Server** to download the dedicated server files

## Server Files

The dedicated server (Steam App ID `4129620`) installs to the directory you choose. Key files:

| File | Purpose |
|------|---------|
| `WindroseServer.exe` | Server launcher (wrapper) |
| `R5\ServerDescription.json` | Server name, password, max players |
| `R5\Saved\SaveProfiles\Default\...WorldDescription.json` | World difficulty and multipliers |
| `R5\Saved\Config\WindowsServer\Game.ini` | Advanced network overrides (created by manager if needed) |
| `R5\Saved\SaveProfiles\` | Save data (backed up by manager) |

## World Settings (Custom Preset)

When **Difficulty** is set to **Custom**, the following multipliers are available:

| Setting | Default | Range |
|---------|---------|-------|
| Mob Health Multiplier | 1.0 | 0.2 – 5.0 |
| Mob Damage Multiplier | 1.0 | 0.2 – 5.0 |
| Ship Health Multiplier | 1.0 | 0.4 – 5.0 |
| Ship Damage Multiplier | 1.0 | 0.2 – 2.5 |
| Boarding Difficulty Multiplier | 1.0 | 0.2 – 5.0 |
| Co-op Stats Correction | 1.0 | 0.0 – 2.0 |
| Co-op Ship Stats Correction | 0.0 | 0.0 – 2.0 |

## Advanced Network Settings

The manager can write a `Game.ini` override file to configure P2P networking:

- **P2P Port Range** — UDP port range the server binds to (open these in your firewall / VPS security group)
- **Encrypt P2P Connections** — Enable encrypted connections (off by default)
- **Force Relay-Only** — Route all traffic through relay servers (hides player IPs, may add latency)
- **Server Stop Delay** — How long the server stays up after all players leave
- **Owner Timeout** — How long the server waits for the first player after startup

> All settings default to 0 / unchecked (game defaults). Keys are only written to `Game.ini` when changed from default.

## VPS Hosting

For VPS hosting, set **P2P Proxy Address** to `0.0.0.0` in Server Config. The server uses P2P/STUN for NAT traversal — clients connect to the server, not to each other. Only the server's IP is exposed to players.

## Building from Source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

```
dotnet build -c Debug
dotnet publish -c Release
```

Output: `bin\Release\net10.0-windows10.0.26100.0\win-x64\publish\WindroseServerManager.exe`

## License

MIT
