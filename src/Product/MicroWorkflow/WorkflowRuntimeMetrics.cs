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
        using var persister = container.GetInstance<IWorkflowStepPersister>();
        return persister.InTransaction(() => persister.CountTables());
    }
}
