using CommonServiceProject;
using Microsoft.Extensions.Logging;

namespace MultiServiceApp;

/// <summary>
/// 매시 20분 정각 간격으로 외부 파일을 가져와 검증 후 저장하는 워커.
/// StepWorkerService를 상속받아 실행 로직을 스텝 단위로 나누고, 스텝별로 독립된 재시도를 적용하는 예제.
/// </summary>
public class FileImportWorker : StepWorkerService
{
    public FileImportWorker(ILogger<FileImportWorker> logger, IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
    }

    protected override WorkerSchedule ConfigureSchedule() => WorkerSchedule.FromAlignedMinutes(20);

    protected override WorkerOptions ConfigureWorkerOptions() => new()
    {
        AllowConcurrentExecution = false,
        ExecutionTimeout = TimeSpan.FromMinutes(30)
    };

    protected override IReadOnlyList<WorkerStep> ConfigureSteps() =>
    [
        new WorkerStep("Download", async (scopedProvider, ct) =>
        {
            Logger.LogInformation("FileImportWorker: 파일 다운로드 중...");
            await Task.Delay(50, ct);
        })
        {
            // 네트워크 오류에 대비해 다운로드는 3회까지 재시도
            RetryCountOnFailure = 3,
            RetryInterval = TimeSpan.FromSeconds(5)
        },

        new WorkerStep("Validate", async (scopedProvider, ct) =>
        {
            Logger.LogInformation("FileImportWorker: 파일 검증 중...");
            await Task.Delay(20, ct);
        }),

        new WorkerStep("Save", async (scopedProvider, ct) =>
        {
            Logger.LogInformation("FileImportWorker: 저장소에 반영 중...");
            await Task.Delay(30, ct);
        })
        {
            // 저장 실패는 1회 재시도 후 최종 실패 처리
            RetryCountOnFailure = 1,
            RetryInterval = TimeSpan.FromSeconds(3)
        }
    ];

    protected override Task OnStepStartingAsync(WorkerStep step, CancellationToken cancellationToken)
    {
        Logger.LogInformation("FileImportWorker: [{Step}] 스텝 시작", step.Name);
        return Task.CompletedTask;
    }

    protected override Task OnStepCompletedAsync(WorkerStep step, TimeSpan elapsedTime, CancellationToken cancellationToken)
    {
        Logger.LogInformation("FileImportWorker: [{Step}] 스텝 완료 ({Elapsed}ms)", step.Name, elapsedTime.TotalMilliseconds);
        return Task.CompletedTask;
    }

    protected override Task OnStepErrorAsync(WorkerStep step, Exception ex, int retryAttempt, CancellationToken cancellationToken)
    {
        Logger.LogWarning(ex, "FileImportWorker: [{Step}] {Attempt}번째 시도 실패", step.Name, retryAttempt);
        return Task.CompletedTask;
    }

    protected override Task OnFinalFailureAsync(Exception ex, int totalAttempts, CancellationToken cancellationToken)
    {
        if (ex is StepExecutionException stepEx)
        {
            Logger.LogError(ex, "FileImportWorker: 스텝 '{Step}'에서 최종 실패했습니다.", stepEx.StepName);
        }

        return Task.CompletedTask;
    }
}
