using System.IO.Compression;

namespace WindroseServerManager.Services;

public class BackupService
{
    private readonly string _saveDataPath;   // R5\Saved\SaveProfiles
    private readonly string _backupsDir;
    private readonly FileLogger _logger;

    private System.Threading.Timer? _timer;
    private DateTime _nextBackupTime = DateTime.MaxValue;
    private bool _enabled;
    private int  _intervalHours;
    private int  _keepCount;

    public event EventHandler<string>? BackupCreated;
    public event EventHandler<string>? BackupFailed;

    public BackupService(string rootDir, FileLogger logger, ConfigurationManager configManager)
    {
        _saveDataPath = configManager.SaveDataPath;   // R5\Saved\SaveProfiles
        _backupsDir   = Path.Combine(rootDir, "Backups");
        _logger       = logger;
        Directory.CreateDirectory(_backupsDir);
    }

    public void Configure(bool enabled, int intervalHours, int keepCount)
    {
        _enabled       = enabled;
        _intervalHours = Math.Max(1, intervalHours);
        _keepCount     = Math.Max(1, keepCount);

        _timer?.Dispose();
        if (!enabled) { _nextBackupTime = DateTime.MaxValue; return; }

        _nextBackupTime = DateTime.Now.AddHours(_intervalHours);
        _timer = new System.Threading.Timer(OnTimerTick, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    private async void OnTimerTick(object? _)
    {
        if (_enabled && DateTime.Now >= _nextBackupTime)
        {
            _nextBackupTime = DateTime.Now.AddHours(_intervalHours);
            await CreateBackupAsync();
        }
    }

    public async Task<string?> CreateBackupAsync()
    {
        if (!Directory.Exists(_saveDataPath))
        {
            string msg = $"Save data folder not found: {_saveDataPath}\n" +
                         "Has the server been run at least once?";
            _logger.Warning(msg);
            BackupFailed?.Invoke(this, msg);
            return null;
        }

        Directory.CreateDirectory(_backupsDir);
        string timestamp  = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string backupPath = Path.Combine(_backupsDir, $"WindroseBackup_{timestamp}.zip");

        try
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create);
                foreach (string filePath in Directory.GetFiles(_saveDataPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        string entryName = filePath[((_saveDataPath.Length + 1))..];
                        archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
                    }
                    catch (IOException) { /* File locked — skip */ }
                }
            });

            _logger.Info($"Backup created: {backupPath}");
            CleanOldBackups();
            BackupCreated?.Invoke(this, backupPath);
            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.Error("Backup failed.", ex);
            BackupFailed?.Invoke(this, ex.Message);
            if (File.Exists(backupPath)) File.Delete(backupPath);
            return null;
        }
    }

    public async Task<bool> RestoreBackupAsync(string backupPath)
    {
        try
        {
            // Safety backup first
            await CreateBackupAsync();

            await Task.Run(() =>
            {
                if (Directory.Exists(_saveDataPath))
                    Directory.Delete(_saveDataPath, recursive: true);
                ZipFile.ExtractToDirectory(backupPath, _saveDataPath);
            });

            _logger.Info($"Backup restored from: {backupPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Restore failed.", ex);
            return false;
        }
    }

    public List<(string Path, DateTime Date, long SizeBytes)> GetBackups()
    {
        if (!Directory.Exists(_backupsDir)) return [];
        return Directory.GetFiles(_backupsDir, "*.zip")
            .Select(f => (f, File.GetLastWriteTime(f), new FileInfo(f).Length))
            .OrderByDescending(x => x.Item2)
            .ToList();
    }

    public bool DeleteBackup(string path)
    {
        try { File.Delete(path); return true; }
        catch (Exception ex) { _logger.Error("Failed to delete backup.", ex); return false; }
    }

    private void CleanOldBackups()
    {
        var backups = GetBackups();
        foreach (var (path, _, _) in backups.Skip(_keepCount))
        {
            try { File.Delete(path); _logger.Info($"Deleted old backup: {path}"); }
            catch { }
        }
    }

    public DateTime NextBackupTime   => _nextBackupTime;
    public string   BackupsDirectory => _backupsDir;

    public void Dispose() => _timer?.Dispose();
}
