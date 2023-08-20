namespace GreenFeetWorkflow;

// TODO fingure out how and what metrics to expose
public interface IEngineMetrics
{
    // TODO int CountReadySteps();
}

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
}

public interface IWorkflowIocContainer
{
    /// <summary>
    /// implement to return null wnen registration is not found, and throw exception on creation failure.
    /// </summary>
    IStepImplementation? GetNamedInstance(string stepName);

    T GetInstance<T>() where T : notnull;
}

// TODO NICE for testing tillad at der kun hentes et bestemt flowid  og rename to GetReadyStep()
public interface IStepPersister : IDisposable
{
    /// <summary>
    /// You can either set the transaction explicitly or create one using <see cref="CreateTransaction"/>
    /// </summary>
    object? Transaction { get; set; }

    void Commit(StepStatus status, Step executedStep, List<Step>? newSteps);

    // TODO rename to CreateTxAndLockAvialableStep()
    // TODO add CreateTxAndLockAvialableStep(id) - this is needed to activate a step
    Step? GetStep();
    void RollBack();
    
    /// <summary>
    /// Persist one or more steps. If uses a separate transaction, or the supplied transaction when not null.
    /// </summary>
    int[] AddSteps(object? transaction = null, params Step[] steps);
    
    /// <summary>
    /// Reschedule a ready step to 'now' and send it activation data
    /// </summary>
    int ActivateStep(int id, string? activationData);

    object CreateTransaction();
    Dictionary<StepStatus, IEnumerable<Step>> SearchSteps(SearchModel model);

    /// <summary>
    /// Move rows from done and fail columns
    /// </summary>
    /// <param name="entities"></param>
    /// <param name="ids"></param>
    /// <returns>number of rows moved</returns>
    int[] ReExecuteSteps(Dictionary<StepStatus, IEnumerable<Step>> entities);


}

public interface IStateFormatter
{
    public string StateFormatName { get; }

    string Serialize(object? binaryState);
    T? Deserialize<T>(string? state);
}