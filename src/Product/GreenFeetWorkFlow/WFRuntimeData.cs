﻿namespace GreenFeetWorkflow;

public class WfRuntimeData
{
    private readonly IWorkflowIocContainer iocContainer;
    private readonly IWorkflowStepStateFormatter formatter;

    public WfRuntimeData(IWorkflowIocContainer iocContainer, IWorkflowStepStateFormatter formatter)
    {
        this.iocContainer = iocContainer;
        this.formatter = formatter;
    }

    /// <summary> Reschedule a ready step to 'now' and send it activation data </summary>
    public int ActivateStep(int id, object? activationArguments, object? transaction = null)
    {
        string? serializedArguments = formatter.Serialize(activationArguments);
        var persister = iocContainer.GetInstance<IStepPersister>();

        int rows = persister.InTransaction(
            () => persister.UpdateStep(id, serializedArguments, TrimToSeconds(DateTime.Now)),
            transaction);
        return rows;
    }

    /// <summary> Add step to be executed </summary>
    /// <returns>the identity of the steps</returns>
    public int AddStep(Step step, object? transaction = null) => AddSteps(new[] { step }, transaction).Single();

    /// <summary> Add steps to be executed </summary>
    /// <returns>the identity of the steps</returns>
    public int[] AddSteps(Step[] steps, object? transaction = null)
    {
        var now = DateTime.Now;

        foreach (var x in steps)
        {
            FixupNewStep(null, x, now);
        }

        IStepPersister persister = iocContainer.GetInstance<IStepPersister>();
        return persister.InTransaction(() => persister.AddSteps(steps), transaction);
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

    public Dictionary<StepStatus, IEnumerable<Step>> SearchSteps(SearchModel model, object transaction = null)
    {
        IStepPersister persister = iocContainer.GetInstance<IStepPersister>();

        var result = persister.InTransaction(() => persister.SearchSteps(model), transaction);
        return result;
    }

    // TODO implement CRUD step operations
    // done ready task - spawn new task to ensure we perform the operaton in case the tast is rerunning and is long to execute - worst case a direct call would time out waiting 
    // fail ready task - spawn new task to ensure we perform the operaton in case the tast is rerunning and is long to execute - worst case a direct call would time out waiting 
    // activateWaitingReadyTask

    /// <summary> Re-execute steps that are 'done' or 'failed' by inserting a clone into the 'ready' queue </summary>
    /// <returns>Ids of inserted steps</returns>
    public int[] ReExecuteSteps(SearchModel criterias, object? transaction = null)
    {
        if (criterias.FetchLevel.Ready)
            throw new ArgumentOutOfRangeException("Cannot search the ready queue for steps to re-execute");

        IStepPersister persister = iocContainer.GetInstance<IStepPersister>();

        int[] ids = persister.InTransaction(
            () =>
            {
                var now = DateTime.Now;

                var entities = persister.SearchSteps(criterias);

                if (entities.ContainsKey(StepStatus.Ready) && entities[StepStatus.Ready].Any())
                    throw new ArgumentOutOfRangeException("Cannot re-execute ready steps.");

                var steps = entities
                .SelectMany(x => x.Value)
                .Select(step => new Step()
                {
                    FlowId = step.FlowId,
                    CorrelationId = step.CorrelationId,
                    CreatedByStepId = step.CreatedByStepId,
                    CreatedTime = now,
                    Description = $"Re-execution of step id: " + step.Id,
                    State = step.State,
                    StateFormat = step.StateFormat,
                    ActivationArgs = step.ActivationArgs,
                    ScheduleTime = now,
                    Singleton = step.Singleton,
                    SearchKey = step.SearchKey,
                    Name = step.Name,
                })
                .ToArray();

                return persister.AddSteps(steps);
            },
            transaction);

        return ids;
    }


    /// <summary> we round down to ensure a worker can pick up the step/rerun-step. if in unittest mode it may exit if not rounded. </summary>
    internal static DateTime? TrimToSeconds(DateTime? now) => now == null ? null : TrimToSeconds(now.Value);

    /// <summary> we round down to ensure a worker can pick up the step/rerun-step. if in unittest mode it may exit if not rounded. </summary>
    internal static DateTime TrimToSeconds(DateTime now) => new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);

    internal void FormatStateForSerialization(Step step)
    {
        if (step.InitialState == null)
        {
            step.State = null;
            return;
        }

        if (step.StateFormat == null || step.StateFormat == formatter.StateFormatName)
        {
            step.State = formatter.Serialize(step.InitialState);
        }
        else
        {
            throw new Exception($"No formatter registered for format name '{step.StateFormat}'");
        }
    }
}