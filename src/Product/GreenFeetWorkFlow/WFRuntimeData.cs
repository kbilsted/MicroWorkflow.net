using System.Reflection;

namespace GreenFeetWorkflow;

public class WfRuntimeData
{
    private readonly IWorkflowIocContainer iocContainer;
    private readonly IStateFormatter formatter;

    public WfRuntimeData(IWorkflowIocContainer iocContainer, IStateFormatter formatter)
    {
        this.iocContainer = iocContainer;
        this.formatter = formatter;
    }

    public int ActivateStep(string searchKey, string? stepName, object? activationArguments)
    {
        string serializedArguments = activationArguments == null
         ? serializedArguments = string.Empty
         : serializedArguments = formatter.Serialize(activationArguments);

        // todo mangler at anvende  serializedArguments 
        int rows = iocContainer.GetInstance<IStepPersister>().ActivateStep(searchKey, stepName, null);

        return rows;
    }

    /// <summary>
    /// Add steps to be executed
    /// </summary>
    /// <returns>the identity of the steps</returns>
    public int[] AddSteps(params Step[] steps) => AddSteps(null, steps);

    /// <summary>
    /// Add steps to be executed
    /// </summary>
    /// <returns>the identity of the steps</returns>
    public int[] AddSteps(object? transaction, params Step[] steps)
    {
        var now = DateTime.Now;

        foreach (var x in steps)
        {
            FixupNewStep(null, x, now);
        }

        var stepPersister = iocContainer.GetInstance<IStepPersister>()
            ?? throw new NullReferenceException($"Cannot find steppersister registered as {typeof(IStepPersister)}");
        return stepPersister.AddSteps(transaction, steps);
    }

    internal void FixupNewStep(Step? originStep, Step step, DateTime now)
    {
        if (string.IsNullOrEmpty(step.Name))
            throw new NullReferenceException("step name cannot be null or empty");

        step.CreatedTime = now;
        step.CreatedByStepId = originStep?.Id ?? 0;

        step.FlowId ??= (originStep == null) ? Guid.NewGuid().ToString() : originStep.FlowId;

        step.CorrelationId ??= originStep?.CorrelationId;

        if (step.ScheduleTime == default)
            step.ScheduleTime = TrimToSeconds(now); // TODO MÅSKE else trim to second since it can be called from a stepresult

        FormatStateForSerialization(step);
    }


    public Dictionary<StepStatus, IEnumerable<Step>> SearchSteps(SearchModel model)
    {
        var result = iocContainer.GetInstance<IStepPersister>().SearchSteps(model);
        return result;
    }

    // TODO implement CRUD step operations
    // done ready task - spawn new task to ensure we perform the operaton in case the tast is rerunning and is long to execute - worst case a direct call would time out waiting 
    // fail ready task - spawn new task to ensure we perform the operaton in case the tast is rerunning and is long to execute - worst case a direct call would time out waiting 
    // activateWaitingReadyTask

    /// <summary>
    /// Re-execute steps that are 'done' or 'failed' by inserting a clone into the 'ready' queue
    /// </summary>
    /// <returns>Ids of inserted steps</returns>
    public int[] ReExecuteSteps(SearchModel criterias)
    {
        if (criterias.FetchLevel.IncludeReady)
            throw new ArgumentOutOfRangeException("Cannot search the ready queue");
        var entries = SearchSteps(criterias);

        var persister = iocContainer.GetInstance<IStepPersister>();
        int[] rows = persister.ReExecuteSteps(entries);
        return rows;
    }


    /// <summary>
    /// we round down to ensure a worker can pick up the step/rerun-step. if in unittest mode it may exit if not rounded.
    /// </summary>
    internal static DateTime? TrimToSeconds(DateTime? now) => now == null ? null : TrimToSeconds(now.Value);

    /// <summary>
    /// we round down to ensure a worker can pick up the step/rerun-step. if in unittest mode it may exit if not rounded.
    /// </summary>
    internal static DateTime TrimToSeconds(DateTime now) => new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);


    internal void FormatStateForSerialization(Step step)
    {
        if (step.InitialState == null)
        {
            step.PersistedState = null;
            return;
        }

        if (step.PersistedStateFormat == null || step.PersistedStateFormat == formatter.StateFormatName)
        {
            step.PersistedState = formatter.Serialize(step.InitialState);
        }
        else
        {
            throw new Exception($"No formatter registered for format name '{step.PersistedStateFormat}'");
        }
    }
}