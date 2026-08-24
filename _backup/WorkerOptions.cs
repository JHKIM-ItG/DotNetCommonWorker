using System;

namespace CommonServiceProject;

public class WorkerOptions
{
    public bool AllowConcurrentExecution { get; set; } = false;
    public TimeSpan? ExecutionTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public int RetryCountOnFailure { get; set; } = 2;
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(5);
}
