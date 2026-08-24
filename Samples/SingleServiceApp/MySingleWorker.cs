using CommonServiceProject;
using Microsoft.Extensions.Logging;

namespace SingleServiceApp;

/// <summary>
/// StepWorkerService 예제: 실행 로직을 Fetch -> Process -> Save 스텝으로 나누고,
/// 스텝별로 독립된 재시도를 적용합니다.
/// </summary>
public class MySingleWorker : StepWorkerService
{
    public MySingleWorker(ILogger<MySingleWorker> logger, IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
    }

    protected override WorkerSchedule ConfigureSchedule() => WorkerSchedule.FromAlignedMinutes(10);

    protected override WorkerOptions ConfigureWorkerOptions() => new()
    {
        AllowConcurrentExecution = false,
        ExecutionTimeout = TimeSpan.FromMinutes(30)
    };

    protected override IReadOnlyList<WorkerStep> ConfigureSteps() =>
    [
        new WorkerStep("Fetch", async (scopedProvider, ct) =>
        {
            Logger.LogInformation("MySingleWorker: 데이터 조회 중...");
            await Task.Delay(30, ct);
        })
        {
            RetryCountOnFailure = 2,
            RetryInterval = TimeSpan.FromSeconds(3)
        },

        new WorkerStep("Process", async (scopedProvider, ct) =>
        {
            Logger.LogInformation("MySingleWorker: 데이터 처리 중...");
            await Task.Delay(20, ct);
        }),

        new WorkerStep("Save", async (scopedProvider, ct) =>
        {
            Logger.LogInformation("MySingleWorker: 결과 저장 중...");
            await Task.Delay(20, ct);
        })
        {
            RetryCountOnFailure = 1,
            RetryInterval = TimeSpan.FromSeconds(3)
        }
    ];

    protected override Task OnStepStartingAsync(WorkerStep step, CancellationToken cancellationToken)
    {
        Logger.LogInformation("MySingleWorker: [{Step}] 스텝 시작", step.Name);
        return Task.CompletedTask;
    }

    protected override Task OnStepCompletedAsync(WorkerStep step, TimeSpan elapsedTime, CancellationToken cancellationToken)
    {
        Logger.LogInformation("MySingleWorker: [{Step}] 스텝 완료 ({Elapsed}ms)", step.Name, elapsedTime.TotalMilliseconds);
        return Task.CompletedTask;
    }

    protected override Task OnStepErrorAsync(WorkerStep step, Exception ex, int retryAttempt, CancellationToken cancellationToken)
    {
        Logger.LogWarning(ex, "MySingleWorker: [{Step}] {Attempt}번째 시도 실패", step.Name, retryAttempt);
        return Task.CompletedTask;
    }

    protected override Task OnFinalFailureAsync(Exception ex, int totalAttempts, CancellationToken cancellationToken)
    {
        if (ex is StepExecutionException stepEx)
        {
            Logger.LogError(ex, "MySingleWorker: 스텝 '{Step}'에서 최종 실패했습니다.", stepEx.StepName);
        }

        return Task.CompletedTask;
    }
}
