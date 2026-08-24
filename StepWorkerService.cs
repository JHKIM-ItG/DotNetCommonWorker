using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CommonServiceProject;

/// <summary>
/// 워커의 실행 로직을 여러 스텝으로 나누어 순차 실행하고, 스텝별로 독립된 재시도를 적용하는 BaseWorkerService.
/// 개발자는 RunAsync 대신 ConfigureSteps()만 구현하면 됩니다.
///
/// 스텝 하나가 재시도를 모두 소진하면 나머지 스텝은 실행하지 않고 즉시 최종 실패로 처리됩니다
/// (BaseWorkerService의 OnErrorAsync/OnFinalFailureAsync가 그대로 호출됨).
/// 워커 전체 단위 재시도(WorkerOptions.RetryCountOnFailure)는 스텝별 재시도와 이중으로 겹치므로 0으로 강제됩니다.
/// </summary>
public abstract class StepWorkerService : BaseWorkerService
{
    protected StepWorkerService(ILogger logger, IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
    }

    /// <summary>
    /// 순차 실행할 스텝 목록을 정의합니다.
    /// </summary>
    protected abstract IReadOnlyList<WorkerStep> ConfigureSteps();

    /// <summary>
    /// 중복방지/타임아웃 등 워커 전체 옵션 설정. RetryCountOnFailure는 스텝별 재시도로 대체되어 무시됩니다.
    /// </summary>
    protected virtual WorkerOptions ConfigureWorkerOptions() => new();

    protected virtual Task OnStepStartingAsync(WorkerStep step, CancellationToken cancellationToken) => Task.CompletedTask;

    protected virtual Task OnStepCompletedAsync(WorkerStep step, TimeSpan elapsedTime, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 스텝 실행 실패 시마다 호출됩니다(해당 스텝의 재시도 예정 여부와 무관).
    /// </summary>
    protected virtual Task OnStepErrorAsync(WorkerStep step, Exception ex, int retryAttempt, CancellationToken cancellationToken) => Task.CompletedTask;

    protected sealed override WorkerOptions ConfigureOptions() => ConfigureWorkerOptions();

    /// <summary>
    /// 설정 파일(appsettings.json) 오버라이드까지 모두 반영된 뒤 마지막에 호출되므로,
    /// Workers:{워커명}:Options:RetryCountOnFailure로 값이 들어와도 항상 0으로 고정됩니다.
    /// </summary>
    protected sealed override WorkerOptions NormalizeOptions(WorkerOptions options)
    {
        if (options.RetryCountOnFailure != 0)
        {
            Logger.LogWarning(
                "{Worker}: StepWorkerService는 스텝별 재시도(WorkerStep.RetryCountOnFailure)를 사용하므로 RetryCountOnFailure는 0으로 강제됩니다.",
                GetType().Name);
        }

        options.RetryCountOnFailure = 0;
        return options;
    }

    protected sealed override async Task RunAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        var steps = ConfigureSteps();
        if (steps.Count == 0)
        {
            throw new InvalidOperationException($"{GetType().Name}: ConfigureSteps()가 빈 목록을 반환했습니다.");
        }

        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await OnStepStartingAsync(step, cancellationToken);

            var sw = Stopwatch.StartNew();
            var attempt = 0;

            while (true)
            {
                try
                {
                    await step.ExecuteAsync(scopedProvider, cancellationToken);
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    attempt++;
                    await OnStepErrorAsync(step, ex, attempt, cancellationToken);

                    if (attempt > step.RetryCountOnFailure)
                    {
                        throw new StepExecutionException(step.Name, ex);
                    }

                    await Task.Delay(step.RetryInterval, cancellationToken);
                }
            }

            sw.Stop();
            await OnStepCompletedAsync(step, sw.Elapsed, cancellationToken);
        }
    }
}
