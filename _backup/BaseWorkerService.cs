using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CommonServiceProject;

public abstract class BaseWorkerService : BackgroundService
{
    protected readonly ILogger _logger;
    protected readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _executionLock = new(1, 1);

    public WorkerHealthCheckStatus HealthStatus { get; } = new();

    protected BaseWorkerService(ILogger logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        HealthStatus.WorkerName = GetType().Name;
    }

    protected virtual WorkerSchedule ConfigureSchedule()
    {
        return WorkerSchedule.FromAlignedMinutes(10);
    }

    protected virtual WorkerOptions ConfigureOptions()
    {
        return new WorkerOptions();
    }

    protected abstract Task RunAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken);

    protected virtual Task OnBeforeRunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task OnAfterRunAsync(TimeSpan elapsedTime, CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task OnErrorAsync(Exception exception, int retryAttempt, CancellationToken cancellationToken) => Task.CompletedTask;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var schedule = ConfigureSchedule();
        var options = ConfigureOptions();

        _logger.LogInformation("[{Worker}] 워커 서비스가 시작되었습니다. (스케줄 타입: {Type})", HealthStatus.WorkerName, schedule.Type);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = schedule.GetNextDelay(DateTime.Now);
            var nextExecutionTime = DateTime.Now.Add(delay);
            _logger.LogInformation("[{Worker}] 다음 실행 시각: {NextTime:yyyy-MM-dd HH:mm:ss} (남은 대기: {Delay:hh\\:mm\\:ss})", 
                HealthStatus.WorkerName, nextExecutionTime, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested) break;

            if (!options.AllowConcurrentExecution && _executionLock.CurrentCount == 0)
            {
                _logger.LogWarning("[{Worker}] 이전 작업이 아직 실행 중이므로 이번 회차 실행을 스킵합니다.", HealthStatus.WorkerName);
                continue;
            }

            await _executionLock.WaitAsync(stoppingToken);
            try
            {
                await ExecuteWorkWithRetryAndTimeoutAsync(options, stoppingToken);
            }
            finally
            {
                _executionLock.Release();
            }
        }

        _logger.LogInformation("[{Worker}] 워커 서비스가 종료되었습니다.", HealthStatus.WorkerName);
    }

    private async Task ExecuteWorkWithRetryAndTimeoutAsync(WorkerOptions options, CancellationToken stoppingToken)
    {
        int attempt = 0;
        bool success = false;

        HealthStatus.IsRunning = true;
        HealthStatus.LastRunTime = DateTime.Now;

        while (attempt <= options.RetryCountOnFailure && !success && !stoppingToken.IsCancellationRequested)
        {
            attempt++;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            if (options.ExecutionTimeout.HasValue)
            {
                cts.CancelAfter(options.ExecutionTimeout.Value);
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var scope = _serviceProvider.CreateScope();
                
                await OnBeforeRunAsync(cts.Token);
                _logger.LogInformation("[{Worker}] 작업 실행 시작 (시도: {Attempt})", HealthStatus.WorkerName, attempt);

                await RunAsync(scope.ServiceProvider, cts.Token);

                stopwatch.Stop();
                await OnAfterRunAsync(stopwatch.Elapsed, cts.Token);

                HealthStatus.LastSuccessTime = DateTime.Now;
                HealthStatus.LastExecutionDuration = stopwatch.Elapsed;
                success = true;

                _logger.LogInformation("[{Worker}] 작업 성공 완료 (소요시간: {Elapsed}ms)", HealthStatus.WorkerName, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                HealthStatus.FailureCount++;
                HealthStatus.LastError = ex.Message;

                _logger.LogError(ex, "[{Worker}] 작업 실행 중 오류 발생 (시도 {Attempt}/{Max})", 
                    HealthStatus.WorkerName, attempt, options.RetryCountOnFailure + 1);

                await OnErrorAsync(ex, attempt, stoppingToken);

                if (attempt <= options.RetryCountOnFailure)
                {
                    await Task.Delay(options.RetryInterval, stoppingToken);
                }
            }
        }

        HealthStatus.IsRunning = false;
    }
}

public class WorkerHealthCheckStatus
{
    public string WorkerName { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public DateTime? LastRunTime { get; set; }
    public DateTime? LastSuccessTime { get; set; }
    public TimeSpan? LastExecutionDuration { get; set; }
    public int FailureCount { get; set; }
    public string? LastError { get; set; }
}
