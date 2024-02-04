namespace GreenFeetWorkflow;

/// <summary>
/// throwing this exception makes a step FAIL rather than RETRY it later.
/// </summary>
public class FailCurrentStepException : Exception
{
    public Step[]? NewSteps { get; set; }

    public FailCurrentStepException() : this(null, null, null) { }

    public FailCurrentStepException(string? description)
        : this(description, null, null)
    { }

    public FailCurrentStepException(string? description, Exception? innerException)
        : this(description, innerException, null)
    { }

    public FailCurrentStepException(string? description, Exception? innerException, params Step[]? newSteps)
        : base(description, innerException)
    {
        NewSteps = newSteps;
    }
}
