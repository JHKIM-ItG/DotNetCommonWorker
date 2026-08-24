using System;
using System.Threading;
using System.Threading.Tasks;
using CommonServiceProject;
using Microsoft.Extensions.Logging;

namespace MultiServiceApp.Workers;

public class OrderProcessingWorker : BaseWorkerService
{
    public OrderProcessingWorker(ILogger<OrderProcessingWorker> logger, IServiceProvider serviceProvider) 
        : base(logger, serviceProvider) { }

    protected override WorkerSchedule ConfigureSchedule()
    {
        // 10분 정각 간격 (00, 10, 20, 30, 40, 50분)
        return WorkerSchedule.FromAlignedMinutes(10);
    }

    protected override async Task RunAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        _logger.LogInformation(">>> [OrderProcessingWorker] 주문배치 처리 중...");
        await Task.Delay(300, cancellationToken);
    }
}
