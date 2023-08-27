namespace GreenFeetWorkflow;

public class ExecutionResult
{
    /// <summary> set this within a step-execution to denote if the job needs to run, again, has failed etc. </summary>
    public StepStatus Status { get; set; }

    /// <summary> used only when rerunning a step </summary>
    public object? NewState { get; set; }

    public string? Description { get; set; }
    public string? PersistedStateFormat { get; set; }

    public DateTime? ScheduleTime { get; set; }

    public List<Step>? NewSteps { get; set; } // only allocate when needed

    public ExecutionResult()
    {
    }

    public ExecutionResult(
        StepStatus status = StepStatus.Done,
        List<Step>? newSteps = null,
        object? stateForRerun = null,
        DateTime? scheduleTime = null,
        string? persistedStateFormat = null,
        string? description = null)
    {
        NewState = stateForRerun;
        Status = status;
        ScheduleTime = scheduleTime;
        NewSteps = newSteps;
        PersistedStateFormat = persistedStateFormat;
        Description = description;

        Validate();
    }

    /// <summary> Mark the step as finished successfully. </summary>
    public static ExecutionResult Done() => new ExecutionResult(StepStatus.Done);

    /// <summary> Mark the step as finished successfully. </summary>
    public static ExecutionResult Done(params Step[]? newSteps) => new ExecutionResult(StepStatus.Done, newSteps?.ToList());

    /// <summary> Mark the step as finished successfully. </summary>
    public static ExecutionResult Done(string description, params Step[]? newSteps) => new ExecutionResult(StepStatus.Done, newSteps?.ToList(), description: description);

    /// <summary> Mark the step as finished with failure. </summary>
    public static ExecutionResult Fail() => new ExecutionResult(StepStatus.Failed);

    /// <summary> Mark the step as finished with failure. </summary>
    public static ExecutionResult Fail(string description)
        => new ExecutionResult(StepStatus.Failed, description: description);

    /// <summary> Mark the step as finished with failure. </summary>
    public static ExecutionResult Fail(string description, params Step[]? newSteps)
        => new ExecutionResult(StepStatus.Failed, newSteps?.ToList(), description: description);

    /// <summary> Throw this exception to tell the step engine that the job has finished with failure </summary>
    /// <returns>an exception to throw</returns>
    public static FailCurrentStepException FailAsException(string? description = null, Exception? exception = null)
        => new FailCurrentStepException(description ?? exception?.Message, exception);

    /// <summary> Throw this exception to tell the step engine that the job has finished with failure </summary>
    /// <returns>an exception to throw</returns>
    public static FailCurrentStepException FailAsException(string? description = null, Exception? exception = null, params Step[]? newSteps)
        => new FailCurrentStepException(description ?? exception?.Message, exception, newSteps);

    /// <summary> Mark the step for a re-execution </summary>
    public static ExecutionResult Rerun(
        object? stateForRerun = null,
        List<Step>? newSteps = null,
        DateTime? scheduleTime = null,
        string? persistedStateFormat = null,
        string? description = null)
    {
        return new ExecutionResult(
            StepStatus.Ready,
            newSteps,
            stateForRerun,
            scheduleTime,
            persistedStateFormat,
            description);
    }

    public ExecutionResult With(Step newstep)
    {
        if (NewSteps == null)
            NewSteps = new List<Step>();

        NewSteps.Add(newstep);

        return this;
    }

    public void Validate()
    {
        if (ScheduleTime != null && Status != StepStatus.Ready)
            throw new ArgumentException("'ScheduleTime' can only change on 'ready' steps");
    }
}
