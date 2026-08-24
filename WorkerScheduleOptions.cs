namespace CommonServiceProject;

/// <summary>
/// appsettings.json 등 설정 파일에서 워커 스케줄을 재정의할 때 바인딩되는 모델.
/// Type이 지정되지 않았거나 필요한 값이 비어 있으면 코드의 ConfigureSchedule() 기본값을 그대로 사용합니다.
/// </summary>
public sealed class WorkerScheduleOptions
{
    public ScheduleType? Type { get; set; }
    public int? AlignedMinutes { get; set; }
    public string[]? SpecificTimes { get; set; }
    public TimeSpan? Interval { get; set; }

    internal WorkerSchedule? ToWorkerSchedule()
    {
        return Type switch
        {
            ScheduleType.AlignedMinuteInterval when AlignedMinutes is { } minutes => WorkerSchedule.FromAlignedMinutes(minutes),
            ScheduleType.SpecificTimes when SpecificTimes is { Length: > 0 } times => WorkerSchedule.FromSpecificTimes(times),
            ScheduleType.Interval when Interval is { } interval => WorkerSchedule.FromInterval(interval),
            _ => null
        };
    }
}
