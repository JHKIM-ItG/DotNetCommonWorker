using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommonServiceProject;

public class WorkerHealthCheck : IHealthCheck
{
    private readonly IEnumerable<BaseWorkerService> _workers;

    public WorkerHealthCheck(IEnumerable<BaseWorkerService> workers)
    {
        _workers = workers;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        bool isDegraded = false;

        foreach (var worker in _workers)
        {
            var status = worker.HealthStatus;
            data[status.WorkerName] = new
            {
                status.IsRunning,
                status.LastRunTime,
                status.LastSuccessTime,
                status.LastExecutionDuration,
                status.FailureCount,
                status.LastError
            };

            if (status.FailureCount > 3)
            {
                isDegraded = true;
            }
        }

        if (isDegraded)
        {
            return Task.FromResult(HealthCheckResult.Degraded("일부 워커 서비스에서 연속 오류가 발생했습니다.", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("모든 워커 서비스가 정상 구동 중입니다.", data));
    }
}
