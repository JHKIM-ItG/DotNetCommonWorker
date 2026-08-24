namespace CommonServiceProject;

/// <summary>
/// StepWorkerService에서 특정 스텝이 재시도를 모두 소진하고 최종 실패했을 때 던져지는 예외.
/// 어떤 스텝에서 실패했는지 StepName으로 알 수 있습니다.
/// </summary>
public sealed class StepExecutionException : Exception
{
    public string StepName { get; }

    public StepExecutionException(string stepName, Exception innerException)
        : base($"스텝 '{stepName}' 실행이 재시도를 모두 소진하고 실패했습니다: {innerException.Message}", innerException)
    {
        StepName = stepName;
    }
}
