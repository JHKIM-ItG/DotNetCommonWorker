using CommonServiceProject;
using Microsoft.Extensions.Logging;

namespace MultiServiceApp;

/// <summary>
/// 매일 09:00, 18:00에 리포트를 생성하는 워커.
/// </summary>
public class DailyReportWorker : BaseWorkerService
{
    public DailyReportWorker(ILogger<DailyReportWorker> logger, IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
    }

    protected override WorkerSchedule ConfigureSchedule() => WorkerSchedule.FromSpecificTimes("09:00:00", "18:00:00");

    protected override WorkerOptions ConfigureOptions() => new()
    {
        AllowConcurrentExecution = false,
        ExecutionTimeout = TimeSpan.FromMinutes(30),
        RetryCountOnFailure = 2,
        RetryInterval = TimeSpan.FromSeconds(5)
    };

    protected override Task RunAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        Logger.LogInformation("DailyReportWorker 실행 시각: {Time}", DateTime.Now);
        return Task.CompletedTask;
    }
}
