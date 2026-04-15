namespace WindroseServerManager.Models;

public enum ServerState
{
    NotInstalled,
    Installing,
    Stopped,
    Starting,
    Running,
    Stopping,
    Crashed
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

