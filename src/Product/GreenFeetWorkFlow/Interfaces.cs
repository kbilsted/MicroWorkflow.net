namespace GreenFeetWorkflow;

public interface IStepImplementation
{
    Task<ExecutionResult> ExecuteAsync(Step step);
}

public interface IWorkflowLogger
{
    LoggerConfiguration Configuration { get; init; }
    public bool TraceLoggingEnabled => Configuration.TraceLoggingEnabled;
    public bool DebugLoggingEnabled => Configuration.DebugLoggingEnabled;
    public bool InfoLoggingEnabled => Configuration.InfoLoggingEnabled;
    public bool ErrorLoggingEnabled => Configuration.ErrorLoggingEnabled;

    void LogTrace(string? msg, Exception? exception, Dictionary<string, object?>? arguments);
    void LogDebug(string? msg, Exception? exception, Dictionary<string, object?>? arguments);
    void LogInfo(string? msg, Exception? exception, Dictionary<string, object?>? arguments);
    void LogError(string? msg, Exception? exception, Dictionary<string, object?>? arguments);
}

public interface IWorkflowIocContainer
{
    /// <summary> implement to return null wnen registration is not found, and throw exception on creation failure. </summary>
    IStepImplementation? GetNamedInstance(string stepName);

    T GetInstance<T>() where T : notnull;
}

/// <summary>
/// This interface is disposable such that connections/transactions may be cleaned up by the dispose method
/// </summary>
public interface IStepPersister : IDisposable
{
    T InTransaction<T>(Func<T> code, object? transaction = null);
    object CreateTransaction();
    /// <summary> You can either set the transaction explicitly or create one using <see cref="CreateTransaction"/> </summary>
    void SetTransaction(object transaction);
    void Commit();
    void RollBack();

    /// <summary> Return a row and lock the row so other workers cannot pick it. </summary>
    Step? GetAndLockReadyStep();

    Dictionary<StepStatus, IEnumerable<Step>> SearchSteps(SearchModel criteria);
    Dictionary<StepStatus, int> CountTables(string? flowId = null);
    int Delete(StepStatus target, int id);
    int Insert(StepStatus target, Step step);
    int[] Insert(StepStatus target, Step[] steps);
    int Update(StepStatus target, Step step);
}

public interface IWorkflowStepStateFormatter
{
    public string StateFormatName { get; }

    string Serialize(object? binaryState);
    T? Deserialize<T>(string? state);
}