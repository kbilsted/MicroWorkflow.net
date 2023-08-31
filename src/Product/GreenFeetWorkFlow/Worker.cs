using System.Diagnostics;

namespace GreenFeetWorkflow;

public class Worker
{
    private static readonly object Lock = new(); // TODO dont use static when more machines running in the same environment

    /// <summary> static field so it is shared among all workers. It ensures if no work and many workers, we don't bombard the persistent storage with request for ready items </summary>
    static DateTime SharedThresholdToReducePollingReadyItems = DateTime.MinValue;

    public CancellationToken StoppingToken { get; private set; }

    /// <summary> The good name for a worker is nice for debugging when multiple workers are executing on the same engine. </summary>
    public string? WorkerName { get; set; }

    readonly IWorkflowLogger logger;
    readonly IWorkflowIocContainer iocContainer;
    private readonly WfRuntimeData engineRuntimeData;
    private readonly WorkerConfig workerConfig;

    readonly Stopwatch stopwatch = new();

    public Worker(IWorkflowLogger logger, IWorkflowIocContainer iocContainer, WfRuntimeData runtime, WorkerConfig config)
    {
        this.logger = logger;
        this.iocContainer = iocContainer;
        this.engineRuntimeData = runtime;
        this.workerConfig = config;
    }

    enum WorkerRunStatus { Stop, Continue, NoWorkDone, Error }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        if (logger.InfoLoggingEnabled)
            logger.LogInfo($"{nameof(Worker)}: starting worker", null, new Dictionary<string, object?>() {
                { "workerId", Thread.CurrentThread.Name! } });

        StoppingToken = stoppingToken;

        await ExecuteLoop();

        if (logger.InfoLoggingEnabled)
            logger.LogInfo($"{nameof(Worker)}: stopping worker", null, new Dictionary<string, object?>()
            {
                { "workerId", Thread.CurrentThread.Name! },
                { "IsCancellationRequested", StoppingToken.IsCancellationRequested }
            });
    }

    async Task ExecuteLoop()
    {
        while (!StoppingToken.IsCancellationRequested)
        {
            try
            {
                Delay();

                var result = await FetchExecuteStoreStep();

                switch (result)
                {
                    case WorkerRunStatus.Stop:
                        return;
                    case WorkerRunStatus.Continue:
                        continue;
                    case WorkerRunStatus.Error:
                        StoppingToken.WaitHandle.WaitOne(workerConfig.DelayTechnicalTransientError);
                        continue;
                    case WorkerRunStatus.NoWorkDone:
                        if (workerConfig.StopWhenNoWork)
                        {
                            if (logger.DebugLoggingEnabled)
                                logger.LogDebug($"{nameof(Worker)}: Stopping worker thread due to no work",
                                    null,
                                    new Dictionary<string, object?>() { { "workerId", Thread.CurrentThread.Name! } });
                            return;
                        }

                        lock (Lock)
                        {
                            if (SharedThresholdToReducePollingReadyItems < DateTime.Now)
                                SharedThresholdToReducePollingReadyItems = DateTime.Now + workerConfig.DelayNoReadyWork;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{nameof(Worker)}:****************************************************");
                Debug.WriteLine($"GreenFeetWorkflow unhandled exception{ex}\n{ex.StackTrace}");
                Debug.WriteLine($"{nameof(Worker)}:****************************************************");

                if (logger.ErrorLoggingEnabled)
                    logger.LogError($"{nameof(Worker)}: GreenFeetWorkflow unhandled exception", ex, null);

                StoppingToken.WaitHandle.WaitOne(workerConfig.DelayTechnicalTransientError);
            }
        }
    }

    void Delay()
    {
        bool mustWaitMore;
        do
        {
            lock (Lock)
            {
                mustWaitMore = DateTime.Now < SharedThresholdToReducePollingReadyItems;
            }

            if (mustWaitMore)
                StoppingToken.WaitHandle.WaitOne(workerConfig.DelayNoReadyWork);
        } while (mustWaitMore);
    }

    (Step?, WorkerRunStatus?) GetNextStep(IStepPersister persister)
    {
        try
        {
            Step? step = persister.GetAndLockReadyStep();

            if (step == null)
            {
                if (logger.TraceLoggingEnabled)
                    logger.LogTrace("No ready step found", null, CreateLogContext());

                return (null, WorkerRunStatus.NoWorkDone);
            }
            return (step, null);
        }
        catch (Exception e)
        {
            if (logger.ErrorLoggingEnabled)
                logger.LogError($"{nameof(Worker)}: exception while fetching next step to execute.", e, null);

            return (null, WorkerRunStatus.Error);
        }
    }

    Dictionary<string, object?> CreateLogContext(Step? step = null)
    {
        var result = new Dictionary<string, object?>
        {
            { "workerId", WorkerName }
        };

        if (step != null)
        {
            result.Add("stepName", step.Name);
            result.Add("stepId", step.Id);
            result.Add("flowId", step.FlowId);
            result.Add("correlationId", step.CorrelationId);
        };

        return result;
    }

    async Task<WorkerRunStatus> FetchExecuteStoreStep()
    {
        using (var persister = iocContainer.GetInstance<IStepPersister>())
        {
            if (CreateTransaction(persister) == null)
                return WorkerRunStatus.Error;

            (Step? step, WorkerRunStatus? status) = GetNextStep(persister);
            if (step == null)
                return status!.Value;

            IStepImplementation? implementation = iocContainer.GetNamedInstance(step.Name);
            if (implementation == null)
            {
                LogAndRescheduleStep(persister, step);
                return WorkerRunStatus.Continue;
            }

            if (logger.DebugLoggingEnabled)
                logger.LogDebug($"{nameof(Worker)}: Executing step-implementation for step", null, CreateLogContext(step));
            ExecutionResult result;
            step.ExecutionStartTime = DateTime.Now;
            step.ExecutedBy = WorkerName;
            step.ExecutionCount++;

            result = await ExecuteImplementation(implementation, step);

            step.ExecutionDurationMillis = stopwatch.ElapsedMilliseconds;

            // we log result before persisting, so user get execution result as well as execption during save logs
            if (logger.InfoLoggingEnabled)
            {
                var args = CreateLogContext(step);
                args.Add("executionDuration", step.ExecutionDurationMillis);
                args.Add("newSteps", result.NewSteps?.Count() ?? 0);
                logger.LogInfo($"{nameof(Worker)}: Execution result: {result.Status}.", null, args);
            }

            FixupAfterExecution(step, result);

            if (!PersistChanges(persister, step, result))
                return WorkerRunStatus.Error;

            return WorkerRunStatus.Continue;
        }
    }

    async Task<ExecutionResult> ExecuteImplementation(IStepImplementation implementation, Step step)
    {
        stopwatch.Restart();

        try
        {
            return await implementation.ExecuteAsync(step);
        }
        catch (FailCurrentStepException e)
        {
            return ExecutionResult.Fail(e.Message, e.NewSteps);
        }
        catch (Exception e)
        {
            if (logger.ErrorLoggingEnabled)
                logger.LogError($"{nameof(Worker)}: Unhandled exception during step execution. Will rerun step.", e, CreateLogContext(step));

            return ExecutionResult.Rerun(description: e.Message);
        }
    }

    private bool PersistChanges(IStepPersister persister, Step step, ExecutionResult result)
    {
        try
        {
            switch (result.Status)
            {
                case StepStatus.Done:
                    persister.Delete(StepStatus.Ready, step.Id);
                    persister.Insert(StepStatus.Done, step);
                    break;

                case StepStatus.Failed:
                    persister.Delete(StepStatus.Ready, step.Id);
                    persister.Insert(StepStatus.Failed, step);
                    break;

                case StepStatus.Ready:
                    persister.Update(StepStatus.Ready, step);
                    break;
            }

            if (result.NewSteps != null)
                persister.Insert(StepStatus.Ready, result.NewSteps.ToArray());

            persister.Commit();
        }
        catch (Exception e)
        {
            if (logger.ErrorLoggingEnabled)
                logger.LogError($"{nameof(Worker)}: exception during saving execution result and state.", e, CreateLogContext(step));

            return false;
        }

        return true;
    }

    private void LogAndRescheduleStep(IStepPersister persister, Step step)
    {
        var msg = $"{nameof(Worker)}: missing step-implementation for step '{step.Name}'";
        if (logger.InfoLoggingEnabled)
            logger.LogInfo(msg, null, CreateLogContext(step));

        step.ScheduleTime = DateTime.Now + workerConfig.DelayMissingStepHandler;
        step.Description = msg;
        persister.Update(StepStatus.Ready, step);
        persister.Commit();
    }

    object? CreateTransaction(IStepPersister persister)
    {
        try
        {
            return persister.CreateTransaction();
        }
        catch (Exception e)
        {
            if (logger.ErrorLoggingEnabled)
                logger.LogError($"{nameof(Worker)}: Cannot create persistence transaction.", e, CreateLogContext());

            return null;
        }
    }

    static DateTime CalculateScheduleTime(Step step)
    {
        var now = DateTime.Now;
        var future = now + Min(
            TimeSpan.FromHours(2),
            TimeSpan.FromSeconds(step.ExecutionCount * step.ExecutionCount * step.ExecutionCount));
        return WfRuntimeData.TrimToSeconds(future);
    }

    static TimeSpan Min(TimeSpan t1, TimeSpan t2) => t1 > t2 ? t2 : t1;

    void FixupAfterExecution(Step step, ExecutionResult result)
    {
        var now = DateTime.Now;

        if (result.Status == StepStatus.Ready)
        {
            step.ScheduleTime = WfRuntimeData.TrimToSeconds(result.ScheduleTime)
                ?? CalculateScheduleTime(step);

            if (result.NewState != null)
            {
                step.InitialState = result.NewState;
                engineRuntimeData.FormatStateForSerialization(step);
            }
        }

        step.Description = result.Description ?? step.Description;
        step.StateFormat = result.PersistedStateFormat ?? step.StateFormat;

        if (result.NewSteps != null)
        {
            foreach (var x in result.NewSteps)
                engineRuntimeData.FixupNewStep(step, x, now);
        }
    }
}