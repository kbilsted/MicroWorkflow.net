using System.Diagnostics;

namespace GreenFeetWorkflow;

public class Worker
{
    private static readonly object Lock = new();

    /// <summary> Shared among all workers. It ensures if no work and many workers, we don't bombard the persistent storage with request for ready items </summary>
    static DateTime SharedThresholdToReducePollingReadyItems = DateTime.MinValue;

    public CancellationToken StoppingToken { get; private set; }

    public static void ResetWaitForWorkers()
    {
        lock (Lock)
        {
            SharedThresholdToReducePollingReadyItems = DateTime.MinValue;
        }
    }

    // TODO: flyt til metrics
    private int performanceWaitCounter = 0;
    private int performanceFutileFetchCounter = 0;
    private int performanceWorkDoneCounter = 0;
    private static int TotalperformanceWaitCounter = 0;
    private static int TotalperformanceFutileFetchCounter = 0;
    private static int TotalperformanceWorkDoneCounter = 0;

    int noWorkStreak = 0;

    [Conditional("MOREOUTPUT")]
    void AddToWaitCounter()
    {
        performanceWaitCounter++;
        TotalperformanceWaitCounter++;
    }

    [Conditional("MOREOUTPUT")]
    void AddToFutileFetchCounter() { performanceFutileFetchCounter++; TotalperformanceFutileFetchCounter++; }

    [Conditional("MOREOUTPUT")]
    void AddToWorkDoneCounter() { performanceWorkDoneCounter++; TotalperformanceWorkDoneCounter++; }

    [Conditional("MOREOUTPUT")]
    void PrintPerformanceCounters() => Console.WriteLine(@$"{WorkerName} waits: {performanceWaitCounter} futile-fetches:{performanceFutileFetchCounter} work done:{performanceWorkDoneCounter}
totalwaits: {TotalperformanceWaitCounter} totalfutile-fetches:{TotalperformanceFutileFetchCounter} total work done:{TotalperformanceWorkDoneCounter}");

    /// <summary> The good name for a worker is nice for debugging when multiple workers are executing on the same engine. </summary>
    public string WorkerName { get; set; }

    private readonly IWorkflowLogger logger;
    private readonly IWorkflowIocContainer iocContainer;
    private readonly WorkflowRuntimeData engineRuntimeData;
    private readonly WorkerConfig workerConfig;
    private readonly WorkerCoordinator coordinator;

    private readonly Stopwatch stopwatch = new();

    public Worker(string workerName, IWorkflowLogger logger, IWorkflowIocContainer iocContainer, WorkflowRuntimeData runtime, WorkerConfig config, WorkerCoordinator coordinator)
    {
        WorkerName = workerName;
        this.logger = logger;
        this.iocContainer = iocContainer;
        this.engineRuntimeData = runtime;
        this.workerConfig = config;
        this.coordinator = coordinator;
    }

    enum WorkerRunStatus { Stop, Continue, NoWorkDone, Error }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        if (logger.TraceLoggingEnabled)
            logger.LogTrace($"{nameof(Worker)}: starting worker", null, new Dictionary<string, object?>() {
                { "workerId", WorkerName } });

        StoppingToken = stoppingToken;

        await ExecuteLoop();

        PrintPerformanceCounters();

        if (logger.TraceLoggingEnabled)
            logger.LogTrace($"{nameof(Worker)}: stopping worker", null, new Dictionary<string, object?>()
            {
                { "workerId", WorkerName },
                { "IsCancellationRequested", StoppingToken.IsCancellationRequested }
            });
    }

    async Task ExecuteLoop()
    {
        while (!StoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await FetchExecuteStoreStep();

                switch (result)
                {
                    case WorkerRunStatus.Stop:
                        return;

                    case WorkerRunStatus.Continue:
                        AddToWorkDoneCounter();
                        ResetWaitForWorkers();
                        noWorkStreak = 0;
                        coordinator.TryAddWorker();
                        continue;

                    case WorkerRunStatus.Error:
                        StoppingToken.WaitHandle.WaitOne(workerConfig.DelayTechnicalTransientError);
                        continue;

                    case WorkerRunStatus.NoWorkDone:
                        AddToFutileFetchCounter();

                        if (workerConfig.StopWhenNoImmediateWork)
                        {
                            if (logger.TraceLoggingEnabled)
                                logger.LogTrace($"{nameof(Worker)}: Stopping worker thread due to no work",
                                    null,
                                    new Dictionary<string, object?>() { { "workerId", WorkerName } });
                            coordinator.ForceStopEngine();
                            return;
                        }

                        lock (Lock)
                        {
                            // we don't want to always extend the deadline for when to look for work again
                            // as this can become a very long wait when there are many threads
                            var firstWorkerToHaveNoWork = SharedThresholdToReducePollingReadyItems < DateTime.Now;
                            if (firstWorkerToHaveNoWork)
                                SharedThresholdToReducePollingReadyItems = DateTime.Now + workerConfig.DelayNoReadyWork;
                        }

                        if (noWorkStreak++ >= workerConfig.MaxNoWorkStreakCount)
                        {
                            if (coordinator.MayWorkerDie())
                                return;
                            else
                                noWorkStreak = 0;
                        }

                        Delay();

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
        bool mustWaitMore = true;
        while (mustWaitMore && !StoppingToken.IsCancellationRequested)
        {
            mustWaitMore = DateTime.Now < SharedThresholdToReducePollingReadyItems;
            if (mustWaitMore)
            {
                StoppingToken.WaitHandle.WaitOne(workerConfig.DelayNoReadyWork);
                AddToWaitCounter();
            }
        }
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
            {
                persister.RollBack();
                return status!.Value;
            }

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
        var count = step.ExecutionCount;
        var future = now + Min(
            TimeSpan.FromHours(2),
            TimeSpan.FromSeconds(count * count * count));
        return WorkflowRuntimeData.TrimToSeconds(future);
    }

    static TimeSpan Min(TimeSpan t1, TimeSpan t2) => t1 > t2 ? t2 : t1;

    void FixupAfterExecution(Step step, ExecutionResult result)
    {
        var now = DateTime.Now;

        if (result.Status == StepStatus.Ready)
        {
            step.ScheduleTime = WorkflowRuntimeData.TrimToSeconds(result.ScheduleTime)
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