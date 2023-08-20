namespace GreenFeetWorkflow;

/// <summary>
/// Class that converts an Action to a <see cref="IStepImplementation"/> IStepImplementation
/// </summary>
public class GenericStepHandler : IStepImplementation
{
    private readonly Func<Step, Task<ExecutionResult>> code;

    public GenericStepHandler(Func<Step, ExecutionResult> code)
    {
        if (code == null)
            throw new ArgumentNullException(nameof(code));
        this.code = async (s) => await Task.FromResult(code(s));
    }

    public GenericStepHandler(Func<Step, Task<ExecutionResult>> code)
    {
        this.code = code ?? throw new ArgumentNullException("Code argument can not be null");
    }

    public static GenericStepHandler Create(Func<Step, ExecutionResult> code) => new GenericStepHandler(code);

    public static GenericStepHandler Create(Action<Step> code) => new GenericStepHandler((step) => { code(step); return ExecutionResult.Done(); });

    public async Task<ExecutionResult> ExecuteAsync(Step step)
    {
        return await code(step);
    }
}
