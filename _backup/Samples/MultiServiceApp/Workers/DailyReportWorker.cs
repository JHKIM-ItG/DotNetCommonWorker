using System;
using System.Threading;
using System.Threading.Tasks;
using CommonServiceProject;
using Microsoft.Extensions.Logging;

namespace MultiServiceApp.Workers;

public class DailyReportWorker : BaseWorkerService
{
    public DailyReportWorker(ILogger<DailyReportWorker> logger, IServiceProvider serviceProvider) 
        : base(logger, serviceProvider) { }

    protected override WorkerSchedule ConfigureSchedule()
    {
        // 특정 시각 리스트 지정 (예: 09:00:00, 18:00:00)
        return WorkerSchedule.FromSpecificTimes("09:00:00", "18:00:00");
    }

    protected override async Task RunAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        _logger.LogInformation(">>> [DailyReportWorker] 일일 보고서 생성 작업 실행 중...");
        await Task.Delay(400, cancellationToken);
    }
}
