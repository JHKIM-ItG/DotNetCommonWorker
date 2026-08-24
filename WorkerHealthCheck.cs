using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommonServiceProject;

/// <summary>
/// 등록된 모든 BaseWorkerService의 상태를 취합하여 .NET IHealthCheck로 노출.
/// </summary>
public sealed class WorkerHealthCheck : IHealthCheck
{
    private readonly IEnumerable<BaseWorkerService> _workers;

    public WorkerHealthCheck(IEnumerable<BaseWorkerService> workers)
    {
        _workers = workers;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var unhealthyWorkers = new List<string>();
        var degradedWorkers = new List<string>();

        foreach (var worker in _workers)
        {
            var status = worker.HealthStatus;
            data[status.WorkerName] = new
            {
                status.IsRunning,
                status.LastRunTime,
                status.LastSuccessTime,
                status.LastElapsed,
                status.FailureCount,
                status.CurrentRetryAttempt,
                status.LastRunFailed,
                LastException = status.LastException?.Message
            };

            if (status.LastRunFailed)
            {
                unhealthyWorkers.Add(status.WorkerName);
            }
            else if (status.CurrentRetryAttempt > 0)
            {
                degradedWorkers.Add(status.WorkerName);
            }
        }

        if (unhealthyWorkers.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"다음 워커에서 최종 실패가 발생했습니다: {string.Join(", ", unhealthyWorkers)}",
                data: data));
        }

        if (degradedWorkers.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                $"다음 워커가 재시도 중입니다: {string.Join(", ", degradedWorkers)}",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("모든 워커가 정상입니다.", data));
    }
}
