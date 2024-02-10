namespace MicroWorkflow;

/// <summary>
/// Class that converts an Action to a <see cref="IStepImplementation"/> IStepImplementation
/// </summary>
public class GenericImplementation : IStepImplementation
{
    private readonly Func<Step, Task<ExecutionResult>> code;

    public GenericImplementation(Func<Step, ExecutionResult> code)
    {
        if (code == null)
            throw new ArgumentNullException(nameof(code));
        this.code = async (s) => await Task.FromResult(code(s));
    }

    public GenericImplementation(Func<Step, Task<ExecutionResult>> code)
    {
        this.code = code ?? throw new ArgumentNullException("Code argument can not be null");
    }

    public static GenericImplementation Create(Func<Step, ExecutionResult> code) => new GenericImplementation(code);

    public static GenericImplementation Create(Action<Step> code) => new GenericImplementation((step) => { code(step); return ExecutionResult.Done(); });

    public async Task<ExecutionResult> ExecuteAsync(Step step)
    {
        return await code(step);
    }
}
