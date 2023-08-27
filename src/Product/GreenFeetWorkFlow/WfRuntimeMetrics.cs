namespace GreenFeetWorkflow;

public class WfRuntimeMetrics
{
    private readonly IWorkflowIocContainer container;

    public WfRuntimeMetrics(IWorkflowIocContainer container)
    {
        this.container = container;
    }

    public Dictionary<StepStatus, int> CountSteps()
    {
        using var persister = container.GetInstance<IStepPersister>();
        return persister.InTransaction((persister) => persister.CountTables());
    }
}
