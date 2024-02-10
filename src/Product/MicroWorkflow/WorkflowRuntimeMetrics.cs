namespace MicroWorkflow;

public class WorkflowRuntimeMetrics
{
    private readonly IWorkflowIocContainer container;

    public WorkflowRuntimeMetrics(IWorkflowIocContainer container)
    {
        this.container = container;
    }

    public Dictionary<StepStatus, int> CountSteps()
    {
        using var persister = container.GetInstance<IStepPersister>();
        return persister.InTransaction(() => persister.CountTables());
    }
}
