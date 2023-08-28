namespace GreenFeetWorkflow;

public interface IStepImplementation
{
    Task<ExecutionResult> ExecuteAsync(Step step);
}

public interface IWorkflowLogger
{
    public bool TraceLoggingEnabled { get; set; }
    public bool DebugLoggingEnabled { get; set; }
    public bool InfoLoggingEnabled { get; set; }
    public bool ErrorLoggingEnabled { get; set; }

    void LogTrace(string? msg, Exception? exception, Dictionary<string, object?>? arguments);
    void LogDebug(string? msg, Exception? exception, Dictionary<string, object?>? arguments);
    void LogInfo(string? msg, Exception? exception, Dictionary<string, object?>? arguments);
    void LogError(string? msg, Exception? exception, Dictionary<string, object?>? arguments);

    /// <summary>
    /// when logging, nested loggers should be called as well
    /// </summary>
    IWorkflowLogger AddNestedLogger(IWorkflowLogger logger);
}

public interface IWorkflowIocContainer
{
    /// <summary> implement to return null wnen registration is not found, and throw exception on creation failure. </summary>
    IStepImplementation? GetNamedInstance(string stepName);

    T GetInstance<T>() where T : notnull;
}

// TODO NICE for testing tillad at der kun hentes et bestemt flowid 

/// <summary>
/// This interface is disposable such that connections/transactions may be cleaned up by the dispose method
/// </summary>
public interface IStepPersister : IDisposable
{

    T InTransaction<T>(Func<T> code, object? transaction = null);
    T InTransaction<T>(Func<IStepPersister, T> code, object? transaction = null);
    object CreateTransaction();
    /// <summary> You can either set the transaction explicitly or create one using <see cref="CreateTransaction"/> </summary>
    void SetTransaction(object transaction);
    void Commit();
    void RollBack();

    void UpdateExecutedStep(StepStatus status, Step executedStep);

    /// <summary>
    /// Return a row and lock the row so other workers cannot pick it.
    /// </summary>
    Step? GetAndLockReadyStep();

    /// <summary> Persist one or more steps. </summary>
    int[] AddSteps(Step[] steps);

    /// <summary> Reschedule a ready step to 'now' and send it activation data </summary>
    int UpdateStep(int id, string? activationArgs, DateTime scheduleTime);

    Dictionary<StepStatus, IEnumerable<Step>> SearchSteps(SearchModel model);

    Dictionary<StepStatus, int> CountTables(string? flowId = null);
}

public interface IWorkflowStepStateFormatter
{
    public string StateFormatName { get; }

    string Serialize(object? binaryState);
    T? Deserialize<T>(string? state);
}