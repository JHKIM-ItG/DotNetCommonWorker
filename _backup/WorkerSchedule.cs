using System;
using System.Collections.Generic;

namespace CommonServiceProject;

public enum ScheduleType
{
    AlignedMinutes,
    SpecificTimes,
    Interval
}

public class WorkerSchedule
{
    public ScheduleType Type { get; private set; }
    public int AlignedMinuteInterval { get; private set; } = 10;
    public List<TimeOnly> SpecificTimes { get; private set; } = new();
    public TimeSpan FixedInterval { get; private set; } = TimeSpan.FromMinutes(1);

    private WorkerSchedule() { }

    public static WorkerSchedule FromAlignedMinutes(int minutes)
    {
        if (minutes <= 0 || minutes > 60)
            throw new ArgumentOutOfRangeException(nameof(minutes), "분 간격은 1분에서 60분 사이여야 합니다.");

        return new WorkerSchedule
        {
            Type = ScheduleType.AlignedMinutes,
            AlignedMinuteInterval = minutes
        };
    }

    public static WorkerSchedule FromSpecificTimes(params string[] timeStrings)
    {
        var times = new List<TimeOnly>();
        foreach (var ts in timeStrings)
        {
            if (TimeOnly.TryParse(ts, out var parsed))
            {
                times.Add(parsed);
            }
        }
        times.Sort();

        return new WorkerSchedule
        {
            Type = ScheduleType.SpecificTimes,
            SpecificTimes = times
        };
    }

    public static WorkerSchedule FromInterval(TimeSpan interval)
    {
        return new WorkerSchedule
        {
            Type = ScheduleType.Interval,
            FixedInterval = interval
        };
    }

    public TimeSpan GetNextDelay(DateTime now)
    {
        switch (Type)
        {
            case ScheduleType.AlignedMinutes:
                return CalculateAlignedMinuteDelay(now);
            case ScheduleType.SpecificTimes:
                return CalculateSpecificTimeDelay(now);
            case ScheduleType.Interval:
            default:
                return FixedInterval;
        }
    }

    private TimeSpan CalculateAlignedMinuteDelay(DateTime now)
    {
        int interval = AlignedMinuteInterval;
        int currentMinute = now.Minute;
        int nextAlignedMinute = ((currentMinute / interval) + 1) * interval;

        DateTime nextRun;
        if (nextAlignedMinute >= 60)
        {
            var nextHour = now.AddHours(1);
            nextRun = new DateTime(nextHour.Year, nextHour.Month, nextHour.Day, nextHour.Hour, 0, 0, DateTimeKind.Local);
        }
        else
        {
            nextRun = new DateTime(now.Year, now.Month, now.Day, now.Hour, nextAlignedMinute, 0, DateTimeKind.Local);
        }

        var delay = nextRun - now;
        return delay > TimeSpan.Zero ? delay : TimeSpan.FromMilliseconds(500);
    }

    private TimeSpan CalculateSpecificTimeDelay(DateTime now)
    {
        if (SpecificTimes.Count == 0) return TimeSpan.FromMinutes(1);

        TimeOnly currentTime = TimeOnly.FromDateTime(now);
        foreach (var targetTime in SpecificTimes)
        {
            if (targetTime > currentTime)
            {
                DateTime targetDt = now.Date.Add(targetTime.ToTimeSpan());
                return targetDt - now;
            }
        }

        DateTime tomorrowFirst = now.Date.AddDays(1).Add(SpecificTimes[0].ToTimeSpan());
        return tomorrowFirst - now;
    }
}
