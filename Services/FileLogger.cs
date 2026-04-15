namespace WindroseServerManager.Services;

public class FileLogger
{
    private readonly string _logDir;
    private readonly LogLevel _minLevel;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event EventHandler<(LogLevel Level, string Message)>? LogWritten;

    public FileLogger(string logDirectory, LogLevel minLevel = LogLevel.Info)
    {
        _logDir   = logDirectory;
        _minLevel = minLevel;
        Directory.CreateDirectory(logDirectory);
    }

    private string LogFilePath =>
        Path.Combine(_logDir, $"launcher_{DateTime.Now:yyyy-MM-dd}.log");

    public void Debug(string message)   => Write(LogLevel.Debug,   message);
    public void Info(string message)    => Write(LogLevel.Info,    message);
    public void Warning(string message) => Write(LogLevel.Warning, message);
    public void Error(string message)   => Write(LogLevel.Error,   message);
    public void Error(string message, Exception ex) => Write(LogLevel.Error, $"{message} | {ex.GetType().Name}: {ex.Message}");

    private void Write(LogLevel level, string message)
    {
        if (level < _minLevel) return;
        string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level,-7}] {message}";

        _lock.Wait();
        try { File.AppendAllText(LogFilePath, entry + Environment.NewLine); }
        catch { /* Never crash the app over a logging failure */ }
        finally { _lock.Release(); }

        LogWritten?.Invoke(this, (level, message));
    }

    public string[] GetTodayLines()
    {
        try { return File.Exists(LogFilePath) ? File.ReadAllLines(LogFilePath) : []; }
        catch { return []; }
    }

    public string LogDirectory => _logDir;
}
