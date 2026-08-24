using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace CommonServiceProject;

/// <summary>
/// 워커의 최근 실행 상태를 추적하는 헬스체크용 스냅샷.
/// </summary>
public sealed class WorkerHealthCheckStatus
{
    public string WorkerName { get; internal set; } = string.Empty;
    public bool IsRunning { get; internal set; }
    public DateTime? LastRunTime { get; internal set; }
    public DateTime? LastSuccessTime { get; internal set; }
    public TimeSpan? LastElapsed { get; internal set; }
    public int FailureCount { get; internal set; }
    public Exception? LastException { get; internal set; }

    /// <summary>
    /// 현재 재시도 중인 시도 횟수. 재시도 중이 아니면 0.
    /// (재시도 중 = 실패했지만 아직 RetryCountOnFailure를 소진하지 않아 최종 실패로 확정되지 않은 상태)
    /// </summary>
    public int CurrentRetryAttempt { get; internal set; }

    /// <summary>
    /// 가장 최근에 완료된 실행 회차가 재시도를 모두 소진하고 최종 실패했는지 여부.
    /// 이후 회차가 성공하면 false로 초기화됩니다.
    /// </summary>
    public bool LastRunFailed { get; internal set; }
}

/// <summary>
/// 타 프로젝트 개발자가 상속받아 스케줄 기반 백그라운드 작업을 구현하는 부모 클래스.
/// </summary>
public abstract class BaseWorkerService : BackgroundService
{
    protected readonly ILogger Logger;
    protected readonly IServiceProvider ServiceProvider;

    private WorkerSchedule _schedule;
    private WorkerOptions _options;
    private int _isRunning;
    private readonly ConcurrentDictionary<Task, byte> _inFlightTasks = new();
    private readonly IConfiguration? _configuration;
    private readonly IDisposable? _configChangeRegistration;

    public WorkerHealthCheckStatus HealthStatus { get; } = new();

    protected BaseWorkerService(ILogger logger, IServiceProvider serviceProvider)
    {
        Logger = logger;
        ServiceProvider = serviceProvider;
        HealthStatus.WorkerName = GetType().Name;

        _configuration = serviceProvider.GetService<IConfiguration>();
        (_schedule, _options) = ApplyConfiguration(isReload: false);

        if (_configuration is not null)
        {
            _configChangeRegistration = ChangeToken.OnChange(_configuration.GetReloadToken, () =>
            {
                (_schedule, _options) = ApplyConfiguration(isReload: true);
            });
        }
    }

    /// <summary>
    /// "Workers:{워커 타입명}:Options" / "Workers:{워커 타입명}:Schedule" 설정 섹션이 있으면
    /// ConfigureOptions()/ConfigureSchedule() 코드 기본값 위에 부분 오버라이드로 적용합니다.
    /// </summary>
    private (WorkerSchedule Schedule, WorkerOptions Options) ApplyConfiguration(bool isReload)
    {
        var options = ConfigureOptions();
        var schedule = ConfigureSchedule();

        var section = _configuration?.GetSection($"Workers:{GetType().Name}");
        if (section is { } s && s.Exists())
        {
            s.GetSection("Options").Bind(options);

            var scheduleOverride = new WorkerScheduleOptions();
            s.GetSection("Schedule").Bind(scheduleOverride);
            schedule = scheduleOverride.ToWorkerSchedule() ?? schedule;
        }

        options = NormalizeOptions(options);

        if (isReload)
        {
            Logger.LogInformation("{Worker}: 설정 변경을 감지하여 옵션/스케줄을 다시 적용했습니다.", GetType().Name);
        }

        return (schedule, options);
    }

    /// <summary>
    /// 설정 파일 오버라이드까지 반영된 최종 WorkerOptions에 대해 파생 클래스가 강제해야 할 불변식을
    /// 적용할 수 있는 훅. (예: StepWorkerService가 RetryCountOnFailure를 항상 0으로 고정하는 용도)
    /// 기본 구현은 아무 것도 하지 않습니다.
    /// </summary>
    protected virtual WorkerOptions NormalizeOptions(WorkerOptions options) => options;

    public override void Dispose()
    {
        _configChangeRegistration?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 개발자가 구현할 실제 비즈니스 로직.
    /// </summary>
    protected abstract Task RunAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken);

    /// <summary>
    /// 실행 스케줄 설정. 기본값: 정각 10분 간격.
    /// </summary>
    protected virtual WorkerSchedule ConfigureSchedule() => WorkerSchedule.FromAlignedMinutes(10);

    /// <summary>
    /// 중복방지/타임아웃/재시도 옵션 설정. 기본값: WorkerOptions 기본값.
    /// </summary>
    protected virtual WorkerOptions ConfigureOptions() => new();

    protected virtual Task OnBeforeRunAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected virtual Task OnAfterRunAsync(TimeSpan elapsedTime, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 실행 실패 시마다 호출됩니다(재시도 예정 여부와 무관). 재시도 전 알림/로깅 등에 사용합니다.
    /// </summary>
    protected virtual Task OnErrorAsync(Exception ex, int retryAttempt, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// RetryCountOnFailure를 모두 소진하여 이번 회차가 최종적으로 실패한 시점에 한 번만 호출됩니다.
    /// OnErrorAsync는 재시도마다 호출되어 "이번이 마지막 실패인지" 구분이 어려우므로,
    /// 실패 알림(Slack 등)처럼 최종 실패 시에만 반응해야 하는 로직은 이 훅에 구현합니다.
    /// </summary>
    protected virtual Task OnFinalFailureAsync(Exception ex, int totalAttempts, CancellationToken cancellationToken) => Task.CompletedTask;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = _schedule.GetNextDelay(DateTime.Now);
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (!_options.AllowConcurrentExecution)
            {
                if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
                {
                    Logger.LogWarning("{Worker}: 이전 작업이 아직 실행 중이므로 이번 회차를 건너뜁니다.", GetType().Name);
                    continue;
                }

                try
                {
                    await RunOnceWithRetryAsync(stoppingToken);
                }
                finally
                {
                    Interlocked.Exchange(ref _isRunning, 0);
                }
            }
            else
            {
                var task = RunOnceWithRetryAsync(stoppingToken);
                _inFlightTasks.TryAdd(task, 0);
                _ = task.ContinueWith(t =>
                {
                    _inFlightTasks.TryRemove(t, out _);
                    if (t.IsFaulted)
                    {
                        Logger.LogCritical(t.Exception, "{Worker}: 동시 실행 작업에서 관찰되지 않은 예외가 발생했습니다.", GetType().Name);
                    }
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? shutdownCts = null;
        var effectiveToken = cancellationToken;

        if (_options.GracefulShutdownTimeout is { } configuredTimeout)
        {
            shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            shutdownCts.CancelAfter(configuredTimeout);
            effectiveToken = shutdownCts.Token;

            Logger.LogInformation("{Worker}: 종료 요청을 받았습니다. 최대 {Timeout} 동안 진행 중인 작업의 완료를 기다립니다.", GetType().Name, configuredTimeout);
        }

        using var _ = shutdownCts;

        await base.StopAsync(effectiveToken);

        var pending = _inFlightTasks.Keys.ToArray();
        if (pending.Length == 0)
        {
            return;
        }

        Logger.LogInformation("{Worker}: 종료 전 진행 중인 동시 실행 작업 {Count}건의 완료를 대기합니다.", GetType().Name, pending.Length);

        try
        {
            await Task.WhenAll(pending).WaitAsync(effectiveToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "{Worker}: 종료 대기 시간 내에 일부 작업이 완료되지 못해 그대로 종료합니다.", GetType().Name);
        }
    }

    private async Task RunOnceWithRetryAsync(CancellationToken stoppingToken)
    {
        HealthStatus.IsRunning = true;
        HealthStatus.LastRunTime = DateTime.Now;

        var sw = Stopwatch.StartNew();

        try
        {
            await OnBeforeRunAsync(stoppingToken);

            var attempt = 0;
            while (true)
            {
                using var timeoutCts = _options.ExecutionTimeout is { } timeout
                    ? CancellationTokenSource.CreateLinkedTokenSource(stoppingToken)
                    : null;
                timeoutCts?.CancelAfter(_options.ExecutionTimeout!.Value);

                var executionToken = timeoutCts?.Token ?? stoppingToken;

                try
                {
                    using var scope = ServiceProvider.CreateScope();
                    await RunAsync(scope.ServiceProvider, executionToken);
                    break;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var isTimeout = timeoutCts is { IsCancellationRequested: true } && ex is OperationCanceledException;
                    var reportedException = isTimeout
                        ? new TimeoutException($"{GetType().Name} 작업 실행이 {_options.ExecutionTimeout} 시간을 초과했습니다.", ex)
                        : ex;

                    attempt++;
                    HealthStatus.FailureCount++;
                    await OnErrorAsync(reportedException, attempt, stoppingToken);

                    if (attempt > _options.RetryCountOnFailure)
                    {
                        HealthStatus.CurrentRetryAttempt = 0;

                        try
                        {
                            await OnFinalFailureAsync(reportedException, attempt, stoppingToken);
                        }
                        catch (Exception hookEx)
                        {
                            Logger.LogError(hookEx, "{Worker}: OnFinalFailureAsync 훅 처리 중 예외가 발생했습니다.", GetType().Name);
                        }

                        throw reportedException;
                    }

                    HealthStatus.CurrentRetryAttempt = attempt;
                    await Task.Delay(_options.RetryInterval, stoppingToken);
                }
            }

            sw.Stop();
            HealthStatus.LastSuccessTime = DateTime.Now;
            HealthStatus.LastElapsed = sw.Elapsed;
            HealthStatus.LastException = null;
            HealthStatus.LastRunFailed = false;
            HealthStatus.CurrentRetryAttempt = 0;
            await OnAfterRunAsync(sw.Elapsed, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 서비스 종료에 의한 정상 취소
        }
        catch (Exception ex)
        {
            sw.Stop();
            HealthStatus.LastElapsed = sw.Elapsed;
            HealthStatus.LastException = ex;
            HealthStatus.LastRunFailed = true;
            HealthStatus.CurrentRetryAttempt = 0;
            Logger.LogError(ex, "{Worker}: 최대 재시도 횟수를 초과하여 최종 실패했습니다.", GetType().Name);
        }
        finally
        {
            HealthStatus.IsRunning = false;
        }
    }
}
