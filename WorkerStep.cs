namespace CommonServiceProject;

/// <summary>
/// StepWorkerService에서 실행되는 하나의 스텝 정의.
/// 스텝별로 독립된 재시도 횟수/간격을 가집니다.
/// </summary>
public sealed class WorkerStep
{
    public string Name { get; }
    public Func<IServiceProvider, CancellationToken, Task> ExecuteAsync { get; }

    /// <summary>
    /// 이 스텝이 예외 발생 시 재시도할 최대 횟수. 기본값 0(재시도 없이 바로 실패).
    /// </summary>
    public int RetryCountOnFailure { get; init; } = 0;

    /// <summary>
    /// 이 스텝의 재시도 간격. 기본값 5초.
    /// </summary>
    public TimeSpan RetryInterval { get; init; } = TimeSpan.FromSeconds(5);

    public WorkerStep(string name, Func<IServiceProvider, CancellationToken, Task> executeAsync)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("스텝 이름은 비어 있을 수 없습니다.", nameof(name));
        }

        Name = name;
        ExecuteAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
    }
}
