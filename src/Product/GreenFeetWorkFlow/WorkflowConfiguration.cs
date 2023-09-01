namespace GreenFeetWorkflow;

public record WorkflowConfiguration(
    WorkerConfig WorkerConfig,
    int NumberOfWorkers)
{
    public LoggerConfiguration LoggerConfiguration { get; set; }
}

public class LoggerConfiguration
{
    public DateTime TraceLoggingEnabledUntil { get; set; } = DateTime.MinValue;
    public DateTime DebugLoggingEnabledUntil { get; set; } = DateTime.MinValue;
    public DateTime InfoLoggingEnabledUntil { get; set; } = DateTime.MaxValue;
    public DateTime ErrorLoggingEnabledUntil { get; set; } = DateTime.MaxValue;

    public bool TraceLoggingEnabled => DateTime.Now < TraceLoggingEnabledUntil;
    public bool DebugLoggingEnabled => DateTime.Now < DebugLoggingEnabledUntil;
    public bool InfoLoggingEnabled => DateTime.Now < InfoLoggingEnabledUntil;
    public bool ErrorLoggingEnabled => DateTime.Now < ErrorLoggingEnabledUntil;

    public static LoggerConfiguration OFF = new LoggerConfiguration()
    {
        ErrorLoggingEnabledUntil = DateTime.MinValue,
        InfoLoggingEnabledUntil = DateTime.MinValue,
        DebugLoggingEnabledUntil = DateTime.MinValue,
        TraceLoggingEnabledUntil = DateTime.MinValue,
    };

}

public record WorkerConfig()
{
    /// <summary>
    /// stop the engine when there is no work to carry out now (only none or future step exists)
    /// </summary>
    public bool StopWhenNoWork { get; set; } = false;

    public TimeSpan DelayNoReadyWork { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan DelayTechnicalTransientError { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan DelayMissingStepHandler { get; set; } = TimeSpan.FromHours(1);
}