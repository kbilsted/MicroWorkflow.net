namespace MicroWorkflow;

public record WorkflowConfiguration(WorkerConfig WorkerConfig)
{
    public LoggerConfiguration LoggerConfiguration { get; set; } = LoggerConfiguration.INFO;
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

    public static readonly LoggerConfiguration OFF = new LoggerConfiguration()
    {
        ErrorLoggingEnabledUntil = DateTime.MinValue,
        InfoLoggingEnabledUntil = DateTime.MinValue,
        DebugLoggingEnabledUntil = DateTime.MinValue,
        TraceLoggingEnabledUntil = DateTime.MinValue,
    };

    public static readonly LoggerConfiguration INFO = new LoggerConfiguration()
    {
        ErrorLoggingEnabledUntil = DateTime.MaxValue,
        InfoLoggingEnabledUntil = DateTime.MaxValue,
        DebugLoggingEnabledUntil = DateTime.MinValue,
        TraceLoggingEnabledUntil = DateTime.MinValue,
    };
}

public record WorkerConfig()
{
    /// <summary>
    /// stop the engine when there is no immediate work to carry out now (it will stop if eg. future step exists).
    /// Useful for testing
    /// </summary>
    public bool StopWhenNoImmediateWork { get; set; } = false;

    public TimeSpan DelayNoReadyWork { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan DelayTechnicalTransientError { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan DelayMissingStepHandler { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// minimum number of workers
    /// </summary>
    public int MinWorkerCount = 1;

    /// <summary>
    /// Maximum number of workers
    /// </summary>
    public int MaxWorkerCount = 8;

    /// <summary>
    /// Kill dynamic workers that haven't found work x times in a streak. 
    /// </summary>
    public int MaxNoWorkStreakCount = 10;
}