namespace CommonServiceProject;

/// <summary>
/// 지원되는 스케줄 계산 방식.
/// </summary>
public enum ScheduleType
{
    AlignedMinuteInterval,
    SpecificTimes,
    Interval
}

/// <summary>
/// 워커의 다음 실행 시각까지 남은 대기 시간을 계산하는 스케줄러.
/// </summary>
public sealed class WorkerSchedule
{
    public ScheduleType Type { get; }

    private readonly int _alignedMinutes;
    private readonly List<TimeOnly> _specificTimes = new();
    private readonly TimeSpan _interval;

    private WorkerSchedule(ScheduleType type, int alignedMinutes = 0, List<TimeOnly>? specificTimes = null, TimeSpan interval = default)
    {
        Type = type;
        _alignedMinutes = alignedMinutes;
        if (specificTimes is not null)
        {
            _specificTimes = specificTimes.OrderBy(t => t).ToList();
        }
        _interval = interval;
    }

    /// <summary>
    /// 매시 정각 기준 N분 간격 스케줄을 생성합니다. (예: 10 -> 00,10,20,30,40,50분 00초)
    /// </summary>
    public static WorkerSchedule FromAlignedMinutes(int minutes)
    {
        if (minutes < 1 || minutes > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), "minutes는 1~60 사이여야 합니다.");
        }

        return new WorkerSchedule(ScheduleType.AlignedMinuteInterval, alignedMinutes: minutes);
    }

    /// <summary>
    /// 특정 시각 리스트 기반 스케줄을 생성합니다. (예: "09:00:00", "18:00:00")
    /// </summary>
    public static WorkerSchedule FromSpecificTimes(params string[] times)
    {
        if (times is null || times.Length == 0)
        {
            throw new ArgumentException("times는 최소 1개 이상이어야 합니다.", nameof(times));
        }

        var parsed = times.Select(t => TimeOnly.Parse(t)).ToList();
        return new WorkerSchedule(ScheduleType.SpecificTimes, specificTimes: parsed);
    }

    /// <summary>
    /// 고정 간격 스케줄을 생성합니다.
    /// </summary>
    public static WorkerSchedule FromInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "interval은 0보다 커야 합니다.");
        }

        return new WorkerSchedule(ScheduleType.Interval, interval: interval);
    }

    /// <summary>
    /// 현재 시각을 기준으로 다음 실행까지 남은 대기 시간을 계산합니다.
    /// </summary>
    public TimeSpan GetNextDelay(DateTime now)
    {
        return Type switch
        {
            ScheduleType.AlignedMinuteInterval => GetAlignedMinuteDelay(now),
            ScheduleType.SpecificTimes => GetSpecificTimesDelay(now),
            ScheduleType.Interval => _interval,
            _ => throw new InvalidOperationException($"지원하지 않는 ScheduleType: {Type}")
        };
    }

    private TimeSpan GetAlignedMinuteDelay(DateTime now)
    {
        var hourStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Kind);
        var minuteBlock = (now.Minute / _alignedMinutes) * _alignedMinutes;
        var candidate = hourStart.AddMinutes(minuteBlock);

        while (candidate <= now)
        {
            candidate = candidate.AddMinutes(_alignedMinutes);
        }

        return candidate - now;
    }

    private TimeSpan GetSpecificTimesDelay(DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var nowTimeOnly = TimeOnly.FromDateTime(now);

        foreach (var t in _specificTimes)
        {
            if (t > nowTimeOnly)
            {
                var candidate = today.ToDateTime(t);
                return candidate - now;
            }
        }

        var tomorrow = today.AddDays(1);
        var firstCandidate = tomorrow.ToDateTime(_specificTimes[0]);
        return firstCandidate - now;
    }
}
