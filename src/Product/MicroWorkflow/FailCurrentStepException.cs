namespace MicroWorkflow;

/// <summary>
/// throwing this exception makes a step FAIL rather than RETRY it later.
/// </summary>
public class FailCurrentStepException : Exception
{
    public Step[]? NewSteps { get; set; }

    public FailCurrentStepException(string? description = null, Exception? innerException = null, params Step[]? newSteps)
        : base(description, innerException)
    {
        NewSteps = newSteps;
    }
}
