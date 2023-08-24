namespace GreenFeetWorkflow;

public record WfRuntimeConfiguration(WorkerConfig WorkerConfig, int NumberOfWorkers)
{
}

public record WorkerConfig()
{
    public bool StopWhenNoWork { get; set; } = false;

    public TimeSpan DelayNoReadyWork { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan DelayTechnicalTransientError { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan DelayMissingStepHandler { get; set; } = TimeSpan.FromHours(1);
}