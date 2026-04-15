namespace WindroseServerManager.Services;

public class ScheduledRestartService
{
    private readonly FileLogger _logger;
    private System.Threading.Timer? _timer;

    private bool _enabled;
    private bool _useFixedTimes;
    private int  _intervalHours;
    private List<TimeSpan> _fixedTimes = [];
    private int  _warningMinutes;
    private DateTime _nextRestart = DateTime.MaxValue;
    private readonly HashSet<int> _warnedMinutes = [];

    public event EventHandler<int>? WarningIssued;   // arg = minutes remaining
    public event EventHandler?      RestartTriggered;

    public ScheduledRestartService(FileLogger logger) => _logger = logger;

    public void Configure(bool enabled, bool useFixedTimes, int intervalHours,
        string fixedTimesStr, int warningMinutes)
    {
        _enabled        = enabled;
        _useFixedTimes  = useFixedTimes;
        _intervalHours  = Math.Clamp(intervalHours, 1, 168);
        _warningMinutes = Math.Clamp(warningMinutes, 1, 60);
        _fixedTimes     = ParseFixedTimes(fixedTimesStr);
        _warnedMinutes.Clear();

        _timer?.Dispose();
        if (!enabled) { _nextRestart = DateTime.MaxValue; return; }

        ScheduleNext(DateTime.Now);
        _timer = new System.Threading.Timer(Tick, null,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        _logger.Info($"Scheduled restart configured. Next: {_nextRestart:HH:mm:ss}");
    }

    private void Tick(object? _)
    {
        if (!_enabled) return;
        double remaining = (_nextRestart - DateTime.Now).TotalMinutes;

        foreach (int warnAt in new[] { _warningMinutes, 5, 1 })
        {
            if (remaining <= warnAt && remaining > warnAt - 0.2 && _warnedMinutes.Add(warnAt))
                WarningIssued?.Invoke(this, warnAt);
        }

        if (DateTime.Now >= _nextRestart)
        {
            _warnedMinutes.Clear();
            ScheduleNext(DateTime.Now.AddSeconds(1));
            _logger.Info("Scheduled restart triggered.");
            RestartTriggered?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ScheduleNext(DateTime after)
    {
        if (_useFixedTimes && _fixedTimes.Count > 0)
        {
            var now = after.TimeOfDay;
            var nextToday = _fixedTimes.Where(t => t > now).OrderBy(t => t).FirstOrDefault();
            _nextRestart = nextToday != default
                ? after.Date.Add(nextToday)
                : after.Date.AddDays(1).Add(_fixedTimes.Min());
        }
        else
        {
            _nextRestart = after.AddHours(_intervalHours);
        }
    }

    private static List<TimeSpan> ParseFixedTimes(string raw)
    {
        var result = new List<TimeSpan>();
        if (string.IsNullOrWhiteSpace(raw)) return result;
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TimeSpan.TryParseExact(part, "hh\\:mm", null, out var ts))
                result.Add(ts);
        }
        return result;
    }

    public DateTime NextRestart => _nextRestart;

    public void Dispose() => _timer?.Dispose();
}
