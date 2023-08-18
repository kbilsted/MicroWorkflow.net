namespace GreenFeetWorkflow;

/// <summary>
/// throwing this exception makes a step FAIL rather than RETRY it later.
/// </summary>
public class FailCurrentStepException : Exception
{
    public FailCurrentStepException() : base(){}

    public FailCurrentStepException(string? description) : base(description)
    {
    }

    public FailCurrentStepException(string? description, Exception? innerException)
        : base(description, innerException)
    {
    }
}