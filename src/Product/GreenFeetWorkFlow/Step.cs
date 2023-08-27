namespace GreenFeetWorkflow;

public class Step
{
    public Step()
    { }

    public Step(string name) : this(name, null)
    { }

    public Step(string name, object? state)
    {
        Name = name;
        InitialState = state;
    }

    public int Id { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// When true, only one step can exist in the ready queue. 
    /// Useful for e.g. a recurring job that must only run with a set interval and thus may not exist as multiple ready steps.
    /// </summary>
    public bool Singleton { get; set; }

    /// <summary>
    /// All steps belonging to the same process or flow should be grouped by the same FlowId.
    /// Steps created from a step will inherit the Flowid (unless you overwrite it). 
    /// </summary>
    public string? FlowId { get; set; }

    /// <summary> A key that can be set to later retrieve the step and invoke it using a 'Command' </summary>
    public string? SearchKey { get; set; }

    /// <summary> Field is only used for when new steps needs be persisted for the first time. </summary>
    public object? InitialState { get; set; }

    /// <summary> the state as it is formatted and persisted in the persistencelayer </summary>
    public string? State { get; set; }

    /// <summary> the name of the formatter used to serialize/deserialize state </summary>
    public string? StateFormat { get; set; }

    /// <summary> the arguments for an activation as it is formatted and persisted in the persistencelayer </summary>
    public string? ActivationArgs { get; set; }

    public int ExecutionCount { get; set; }
    public long? ExecutionDurationMillis { get; set; }
    public DateTime? ExecutionStartTime { get; set; }
    public string? ExecutedBy { get; set; }
    public DateTime CreatedTime { get; set; }
    public int? CreatedByStepId { get; set; }

    /// <summary> The earliest point in time the step will be executed </summary>
    public DateTime ScheduleTime { get; set; }

    /// <summary>
    /// The CorrelationId usually denote in what 'context' a given step is created to be able to connect log entries across system boundaries.
    /// Thus steps may share the same FlowId, but not share the same CorrelationId.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary> typically set in various error situations by the framework, unless you overwrite it </summary>
    public string? Description { get; set; }

    /// <summary> Mark the step as finished successfully. </summary>
    public ExecutionResult Done() => ExecutionResult.Done();

    /// <summary> Mark the step as finished successfully. </summary>
    public ExecutionResult Done(params Step[]? newSteps) => ExecutionResult.Done(newSteps);

    /// <summary> Mark the step as finished successfully. </summary>
    public ExecutionResult Done(string description, params Step[]? newSteps) => ExecutionResult.Done(description, newSteps);

    /// <summary> Mark the step as finished successfully. </summary>
    public async Task<ExecutionResult> DoneAsync() => await Task.FromResult(ExecutionResult.Done());

    /// <summary> Mark the step as finished successfully. </summary>
    public async Task<ExecutionResult> DoneAsync(params Step[]? newSteps) => await Task.FromResult(ExecutionResult.Done(newSteps));

    /// <summary> Mark the step as finished successfully. </summary>
    public async Task<ExecutionResult> DoneAsync(string description, params Step[]? newSteps) => await Task.FromResult(ExecutionResult.Done(description, newSteps));

    /// <summary> Mark the step as finished with failure. </summary>
    public ExecutionResult Fail() => ExecutionResult.Fail();

    /// <summary> Mark the step as finished with failure. </summary>
    public ExecutionResult Fail(string description) => ExecutionResult.Fail(description);

    /// <summary> Mark the step as finished with failure. </summary>
    public ExecutionResult Fail(string description, params Step[]? newSteps) => ExecutionResult.Fail(description, newSteps);

    /// <summary> Mark the step as finished with failure. </summary>
    public async Task<ExecutionResult> FailAsync() => await Task.FromResult(ExecutionResult.Fail());

    /// <summary> Mark the step as finished with failure. </summary>
    public async Task<ExecutionResult> FailAsync(string description) => await Task.FromResult(ExecutionResult.Fail(description));

    /// <summary> Mark the step as finished with failure. </summary>
    public async Task<ExecutionResult> FailAsync(string description, params Step[]? newSteps) => await Task.FromResult(ExecutionResult.Fail(description, newSteps));

    /// <summary> Throw this exception to tell the step engine that the job has finished with failure </summary>
    /// <returns>an exception to throw</returns>
    public FailCurrentStepException FailAsException(string? description = null, Exception? exception = null) => ExecutionResult.FailAsException(description, exception);

    /// <summary> Throw this exception to tell the step engine that the job has finished with failure </summary>
    /// <returns>an exception to throw</returns>
    public FailCurrentStepException FailAsException(string? description = null, Exception? exception = null, params Step[]? newSteps)
        => ExecutionResult.FailAsException(description, exception, newSteps);

    /// <summary> Mark the step for a re-execution </summary>
    public ExecutionResult Rerun(
       object? newStateForRerun = null,
       List<Step>? newSteps = null,
       DateTime? scheduleTime = null,
       string? persistedStateFormat = null,
       string? description = null) => ExecutionResult.Rerun(newStateForRerun, newSteps, scheduleTime, persistedStateFormat, description);

    /// <summary> Mark the step for a re-execution </summary>
    public async Task<ExecutionResult> RerunAsync(
       object? newStateForRerun = null,
       List<Step>? newSteps = null,
       DateTime? scheduleTime = null,
       string? persistedStateFormat = null,
       string? description = null) => await Task.FromResult(Rerun(newStateForRerun, newSteps, scheduleTime, persistedStateFormat, description));
}
