namespace MicroWorkflow;

public class WorkflowRuntimeData
{
    private readonly IWorkflowIocContainer iocContainer;
    private readonly IWorkflowStepStateFormatter formatter;
    private readonly IWorkflowLogger logger;
    public WorkerCoordinator? WorkerCoordinator;

    public WorkflowRuntimeData(IWorkflowIocContainer iocContainer, IWorkflowStepStateFormatter formatter, IWorkflowLogger logger, WorkerCoordinator? workerCoordinator)
    {
        this.iocContainer = iocContainer;
        this.formatter = formatter;
        this.logger = logger;
        this.WorkerCoordinator = workerCoordinator;
    }

    // TODO unittest
    /// <summary> Reschedule a ready step to 'now' and send it activation data </summary>
    public int ActivateStep(int id, object? activationArguments, object? transaction = null)
    {
        var persister = iocContainer.GetInstance<IStepPersister>();

        int rows = persister.InTransaction(() =>
            {
                var step = persister.SearchSteps(new SearchModel(Id: id), StepStatus.Ready).FirstOrDefault();
                if (step == null)
                    return 0;

                step.ScheduleTime = TrimToSeconds(DateTime.Now);
                step.ActivationArgs = formatter.Serialize(activationArguments);
                return persister.Update(StepStatus.Ready, step);
            },
            transaction);
        return rows;
    }

    /// <summary>
    /// Try adding a step if it is not found in the ready queue
    /// </summary>
    /// <returns>the identity of the step or null if a search found one or more ready steps</returns>
    public int? AddStepIfNotExists(Step step, SearchModel searchModel, object? transaction = null)
    {
        IStepPersister persister = iocContainer.GetInstance<IStepPersister>();

        transaction ??= persister.CreateTransaction();

        int? result = persister.InTransaction(() =>
        {
            if (persister.SearchSteps(searchModel, StepStatus.Ready).Any())
                return (int?)null;

            return AddStep(step, transaction);
        }, transaction);

        return result;
    }

    /// <summary> Add step to be executed. May throw exception if persistence layer fails. For example when inserting multiple singleton elements </summary>
    /// <returns>the identity of the step</returns>
    public int AddStep(Step step, object? transaction = null) => AddSteps(new[] { step }, transaction).Single();

    /// <summary> Add steps to be executed. May throw exception if persistence layer fails. For example when inserting multiple singleton elements </summary>
    /// <returns>the identity of the steps</returns>
    public int[] AddSteps(Step[] steps, object? transaction = null)
    {
        var now = DateTime.Now;

        foreach (var step in steps)
        {
            FixupNewStep(null, step, now);
        }

        IStepPersister persister = iocContainer.GetInstance<IStepPersister>();
        var result = persister.InTransaction(() => persister.Insert(StepStatus.Ready, steps), transaction);

        Worker.ResetWaitForWorkers();
        WorkerCoordinator?.TryAddWorker();

        return result;
    }

    /// <summary> Add steps to be executed. May throw exception if persistence layer fails. For example when inserting multiple singleton elements.
    /// Bulk operation. Very fast, but cannot be used with other transactions and does not return identities inserted
    /// </summary>
    public async Task AddStepsBulkAsync(IEnumerable<Step> steps)
    {
        var now = DateTime.Now;

        var persister = iocContainer.GetInstance<IStepPersister>();

        var fix = steps.Select(x =>
        {
            FixupNewStep(null, x, now);
            return x;
        });

        await persister.InsertBulkAsync(StepStatus.Ready, fix);
        Worker.ResetWaitForWorkers();
        WorkerCoordinator?.TryAddWorker();
    }

    internal void FixupNewStep(Step? originStep, Step step, DateTime now)
    {
        if (string.IsNullOrEmpty(step.Name))
            throw new NullReferenceException("step name cannot be null or empty");

        step.CreatedTime = now;
        step.CreatedByStepId = originStep?.Id ?? 0;

        step.FlowId ??= originStep?.FlowId ?? Guid.NewGuid().ToString();

        step.CorrelationId ??= originStep?.CorrelationId;

        if (step.ScheduleTime == default)
            step.ScheduleTime = TrimToSeconds(now);

        FormatStateForSerialization(step);
    }

    public List<Step> SearchSteps(SearchModel criteria, StepStatus target, object? transaction = null)
    {
        IStepPersister persister = iocContainer.GetInstance<IStepPersister>();
        var result = persister.InTransaction(() => persister.SearchSteps(criteria, target), transaction);
        return result;
    }

    public Dictionary<StepStatus, List<Step>> SearchSteps(SearchModel criteria, FetchLevels fetchLevels, object? transaction = null)
    {
        IStepPersister persister = iocContainer.GetInstance<IStepPersister>();
        var result = persister.InTransaction(() => persister.SearchSteps(criteria, fetchLevels), transaction);
        return result;
    }

    /// <summary> Re-execute steps that are 'done' or 'failed' by inserting a clone into the 'ready' queue </summary>
    /// <returns>Ids of inserted steps into the ready queue</returns>
    public int[] ReExecuteSteps(SearchModel criteria, FetchLevels stepKinds, object? transaction = null)
    {
        IStepPersister persister = iocContainer.GetInstance<IStepPersister>();

        if (stepKinds.Ready)
            throw new ArgumentException("Cannot re-execute 'ready' steps");

        int[] ids = persister.InTransaction(() =>
            {
                var now = DateTime.Now;
                var trimmedNow = TrimToSeconds(now);
                var entities = persister.SearchSteps(criteria, stepKinds);

                var steps = entities
                .SelectMany(x => x.Value)
                .Select(step => new Step()
                {
                    FlowId = step.FlowId,
                    CorrelationId = step.CorrelationId,
                    CreatedByStepId = step.CreatedByStepId,
                    CreatedTime = now,
                    Description = $"Re-execution of step id: {step.Id}",
                    State = step.State,
                    StateFormat = step.StateFormat,
                    ActivationArgs = step.ActivationArgs,
                    ScheduleTime = trimmedNow,
                    Singleton = step.Singleton,
                    SearchKey = step.SearchKey,
                    Name = step.Name,
                })
                .ToArray();

                if (logger.InfoLoggingEnabled)
                    logger.LogInfo($"{nameof(WorkflowRuntimeData)}: Reexecuting ids", null, new Dictionary<string, object?>() { { "ids", steps.Select(x => x.Id).ToArray() } });

                return persister.Insert(StepStatus.Ready, steps);
            },
            transaction);

        Worker.ResetWaitForWorkers();
        WorkerCoordinator?.TryAddWorker();

        return ids;
    }

    /// <summary>
    /// Fail one or more 'ready' steps. If a step is executing while being failed a db deadlock may occur and instead the failing must be issued from a step with retries instead.
    /// </summary>
    /// <param name="criteria"></param>
    /// <param name="transaction"></param>
    /// <returns>The ids of the failed steps</returns>
    public int[] FailSteps(SearchModel criteria, object? transaction = null)
    {
        IStepPersister persister = iocContainer.GetInstance<IStepPersister>();

        int[] ids = persister.InTransaction(() =>
        {
            List<int> result = new List<int>();
            List<Step> steps;
            do
            {
                steps = persister.SearchSteps(criteria, StepStatus.Ready);

                foreach (var step in steps)
                {
                    persister.Delete(StepStatus.Ready, step.Id);
                    persister.Insert(StepStatus.Failed, step);
                    result.Add(step.Id);
                }
            } while (steps.Any());
            return result.ToArray();
        }, transaction);

        return ids;
    }

    /// <summary> we round down to ensure a worker can pick up the step/rerun-step. if in unittest mode it may exit if not rounded. </summary>
    internal static DateTime? TrimToSeconds(DateTime? now) => now == null ? null : TrimToSeconds(now.Value);

    /// <summary> we round down to ensure a worker can pick up the step/rerun-step. if in unittest mode it may exit if not rounded. </summary>
    internal static DateTime TrimToSeconds(DateTime now)
    {
        if (now == DateTime.MaxValue || now == DateTime.MinValue)
            return now;
        return new DateTime(now.Ticks - (now.Ticks % TimeSpan.TicksPerSecond), now.Kind);
    }

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
