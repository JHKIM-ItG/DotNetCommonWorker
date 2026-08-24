using CommonServiceProject;
using Microsoft.Extensions.Logging;

namespace MultiServiceApp;

/// <summary>
/// 매시 30분 정각 간격으로 데이터베이스 정리를 수행하는 워커.
/// </summary>
public class DatabaseCleanupWorker : BaseWorkerService
{
    public DatabaseCleanupWorker(ILogger<DatabaseCleanupWorker> logger, IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
    }

    protected override WorkerSchedule ConfigureSchedule() => WorkerSchedule.FromAlignedMinutes(30);

    protected override WorkerOptions ConfigureOptions() => new()
    {
        AllowConcurrentExecution = false,
        ExecutionTimeout = TimeSpan.FromMinutes(30),
        RetryCountOnFailure = 2,
        RetryInterval = TimeSpan.FromSeconds(5)
    };

    protected override Task RunAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        Logger.LogInformation("DatabaseCleanupWorker 실행 시각: {Time}", DateTime.Now);
        return Task.CompletedTask;
    }
}
