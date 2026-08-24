using System;
using System.Threading;
using System.Threading.Tasks;
using CommonServiceProject;
using Microsoft.Extensions.Logging;

namespace MultiServiceApp.Workers;

public class DatabaseCleanupWorker : BaseWorkerService
{
    public DatabaseCleanupWorker(ILogger<DatabaseCleanupWorker> logger, IServiceProvider serviceProvider) 
        : base(logger, serviceProvider) { }

    protected override WorkerSchedule ConfigureSchedule()
    {
        // 30분 정각 간격 (00분, 30분)
        return WorkerSchedule.FromAlignedMinutes(30);
    }

    protected override async Task RunAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        _logger.LogInformation(">>> [DatabaseCleanupWorker] 임시 데이터베이스 정리 작업 실행 중...");
        await Task.Delay(200, cancellationToken);
    }
}
