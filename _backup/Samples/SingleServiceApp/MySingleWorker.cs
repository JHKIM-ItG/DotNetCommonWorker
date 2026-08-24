using System;
using System.Threading;
using System.Threading.Tasks;
using CommonServiceProject;
using Microsoft.Extensions.Logging;

namespace SingleServiceApp;

public class MySingleWorker : BaseWorkerService
{
    public MySingleWorker(ILogger<MySingleWorker> logger, IServiceProvider serviceProvider) 
        : base(logger, serviceProvider) { }

    protected override WorkerSchedule ConfigureSchedule()
    {
        // 10분 정각 간격 (00, 10, 20, 30, 40, 50분)
        return WorkerSchedule.FromAlignedMinutes(10);
    }

    protected override async Task RunAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        _logger.LogInformation(">>> [SingleWorker] 단일 서비스 비즈니스 로직 수행 중...");
        await Task.Delay(500, cancellationToken);
    }
}
