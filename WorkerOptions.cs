namespace CommonServiceProject;

/// <summary>
/// 워커 실행 동작(중복 방지, 타임아웃, 재시도)을 제어하는 옵션.
/// </summary>
public sealed class WorkerOptions
{
    /// <summary>
    /// 이전 차수 작업이 진행 중일 때 다음 차수의 중복 실행을 허용할지 여부.
    /// </summary>
    public bool AllowConcurrentExecution { get; set; } = false;

    /// <summary>
    /// 1회 실행 시 최대 허용 타임아웃. null이면 타임아웃 없음.
    /// </summary>
    public TimeSpan? ExecutionTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 작업 예외 발생 시 최대 재시도 횟수.
    /// </summary>
    public int RetryCountOnFailure { get; set; } = 2;

    /// <summary>
    /// 재시도 간격.
    /// </summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 서비스 종료 요청 시 진행 중인 작업의 완료를 최대 얼마나 기다릴지.
    /// null이면 별도 상한 없이 호스트의 종료 대기 시간(예: 제네릭 호스트의 ShutdownTimeout)을 그대로 따릅니다.
    /// </summary>
    public TimeSpan? GracefulShutdownTimeout { get; set; } = null;
}
