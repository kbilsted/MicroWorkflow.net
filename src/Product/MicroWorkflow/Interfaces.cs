namespace MicroWorkflow;

/// <summary>
/// Use steps in an untyped fashion where you deserialize the state of the step and handle errors
/// </summary>
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

/// <summary>
/// Implement this to use whatever ioc container youw ant
/// </summary>
public interface IWorkflowIocContainer
{
    /// <summary> implement to return null wnen registration is not found, and throw exception on creation failure. </summary>
    IStepImplementation? GetStep(string stepName);

    /// <summary> implement to return null wnen registration is not found, and throw exception on creation failure. </summary>
    T GetInstance<T>() where T : notnull;

    void RegisterWorkflowStep(string stepName, Type implementationType);

    void RegisterWorkflowSteps((string stepName, Type implementationType)[] stepHandlers)
        => stepHandlers.ToList().ForEach(x => RegisterWorkflowStep(x.stepName, x.implementationType));
    void RegisterWorkflowStep(string stepName, IStepImplementation instance);

    void RegisterWorkflowSteps((string stepName, IStepImplementation instance)[] stepHandlers)
        => stepHandlers.ToList().ForEach(x => RegisterWorkflowStep(x.stepName, x.instance));
}

/// <summary>
/// This interface is disposable such that connections/transactions may be cleaned up by the dispose method
/// </summary>
public interface IWorkflowStepPersister : IDisposable
{
    string GetConnectionInfoForLogging();

    T InTransaction<T>(Func<T> code, object? transaction = null);
    object CreateTransaction();
    /// <summary> You can either set the transaction explicitly or create one using <see cref="CreateTransaction"/> </summary>
    void SetTransaction(object transaction);
    void Commit();
    void RollBack();

    /// <summary> Return a row and lock the row so other workers cannot pick it. </summary>
    Step? GetAndLockReadyStep();

    public List<Step> SearchSteps(SearchModel criteria, StepStatus target);
    Dictionary<StepStatus, List<Step>> SearchSteps(SearchModel criteria, FetchLevels fetchLevels);

    Dictionary<StepStatus, int> CountTables(string? flowId = null);
    int Delete(StepStatus target, int id);
    int Insert(StepStatus target, Step step);
    int[] Insert(StepStatus target, Step[] steps);
    Task InsertBulkAsync(StepStatus target, IEnumerable<Step> steps);

    int Update(StepStatus target, Step step);
}

public interface IWorkflowStepStateFormatter // TODO rename to IWorkflowStateFormatter
{
    public string StateFormatName { get; }

    string Serialize(object? binaryState);
    T? Deserialize<T>(string? state);
}