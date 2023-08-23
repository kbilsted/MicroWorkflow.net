using System.Diagnostics;

namespace GreenFeetWorkflow;

// why we dont store the raw type from step state objects. will be serialized eg into
// System.Collections.Generic.Dictionary`2[[System.String, System.Private.CoreLib, Version=6.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e],[System.Object, System.Private.CoreLib, Version=6.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]]
//or
// GreenFeetWorkflow.Tests+BuyInstructions
// we dont want to rely on corelib in v6 or that we use very specific private classes
//
public class Worker
{
    private static readonly object Lock = new();

    /// <summary> static field so it is shared among all workers. It ensures if no work and many workers, we don't bombard the persistent storage with request for ready items </summary>
    static DateTime SharedThresholdToReducePollingReadyItems = DateTime.MinValue;

    public CancellationToken StoppingToken { get; set; }

    /// <summary> The good name for a worker is nice for debugging when multiple workers are executing on the same engine. </summary>
    public string? WorkerName { get; set; }

    readonly IWorkflowLogger logger;
    readonly IWorkflowIocContainer iocContainer;
    private readonly WfRuntimeData engineRuntimeData;

    /// <summary> nice for unit testing </summary>
    public bool StopWhenNoWorkLeft { get; set; }

    // TODO make a configuration class - and later make it a DB configuration
    public TimeSpan DelayNoReadyWork = TimeSpan.FromSeconds(3);
    public TimeSpan DelayTechnicalTransientError = TimeSpan.FromSeconds(5);
    public TimeSpan DelayMissingStepHandler = TimeSpan.FromHours(1);
    // TODO add timestamp for startup delay - to make webapi solutions easier to debug
    readonly Stopwatch stopwatch = new();

    public Worker(IWorkflowLogger logger, IWorkflowIocContainer iocContainer, WfRuntimeData runtime)
    {
        this.logger = logger;
        this.iocContainer = iocContainer;
        this.engineRuntimeData = runtime;
    }

    enum WorkerRunStatus { Stop, Continue, NoWorkDone }

    // TODO take as args StopWhenNoWorkLeft,StoppingToken
    public async Task StartAsync()
    {
        if (logger.InfoLoggingEnabled)
            logger.LogInfo($"{nameof(Worker)}: starting worker", null, new Dictionary<string, object?>() {
                { "workerId", Thread.CurrentThread.Name! } });

        await ExecuteLoop();

        if (logger.InfoLoggingEnabled)
            logger.LogInfo($"{nameof(Worker)}: stopping worker", null, new Dictionary<string, object?>()
            {
                { "workerId", Thread.CurrentThread.Name! },
                { "IsCancellationRequested", StoppingToken.IsCancellationRequested }
            });
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
                StoppingToken.WaitHandle.WaitOne(DelayNoReadyWork);
        } while (mustWaitMore);
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
                    case WorkerRunStatus.NoWorkDone:
                        if (StopWhenNoWorkLeft)
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
                                SharedThresholdToReducePollingReadyItems = DateTime.Now + DelayNoReadyWork;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                var message = $"GreenFeetWorkflow error - unhandled exception{ex}\n{ex.StackTrace}";

                Console.WriteLine($"{nameof(Worker)}:****************************************************");
                Console.WriteLine(message);
                Console.WriteLine($"{nameof(Worker)}:****************************************************");

                Debug.WriteLine($"{nameof(Worker)}:****************************************************");
                Debug.WriteLine(message);
                Debug.WriteLine($"{nameof(Worker)}:****************************************************");

                if (logger.ErrorLoggingEnabled)
                    logger.LogError($"{nameof(Worker)}:GreenFeetWorkflow error - unhandled exception", ex, null);
            }
        }
    }

    Step? GetNextStep(IStepPersister persister)
    {
        Step? step = null;

        try
        {
            step = persister.GetAndLockReadyStep();

            if (step == null)
            {
                if (logger.TraceLoggingEnabled)
                    logger.LogTrace("No ready step found", null, new Dictionary<string, object?> { { "workerId", Thread.CurrentThread.Name! } });
            }
        }
        catch (Exception e)
        {
            if (logger.ErrorLoggingEnabled)
                logger.LogError($"{nameof(Worker)}: exception while fetching next step to execute. ", e, null);

            if (step != null)
                persister.RollBack();

            StoppingToken.WaitHandle.WaitOne(DelayTechnicalTransientError);

            return null;
        }

        return step;
    }

    Dictionary<string, object?> CreateLogContext(Step? step = null)
    {
        var result = new Dictionary<string, object?>
        {
            { "workerId", Thread.CurrentThread.Name! }
        };

        if (step != null)
        {
            result.Add("stepName", step.Name);
            result.Add("stepId", step.Id);
            result.Add("correlationId", step.CorrelationId);
        };

        return result;
    }

    async Task<WorkerRunStatus> FetchExecuteStoreStep()
    {
        using (var persister = iocContainer.GetInstance<IStepPersister>())
        {
            persister.CreateTransaction();

            Step? step = GetNextStep(persister);
            if (step == null)
                return WorkerRunStatus.NoWorkDone;

            IStepImplementation? implementation = iocContainer.GetNamedInstance(step.Name);
            if (implementation == null)
            {
                var msg = $"{nameof(Worker)}: missing step-implementation for step '{step.Name}'";
                if (logger.InfoLoggingEnabled)
                    logger.LogInfo(msg, null, CreateLogContext(step));

                step.ScheduleTime = DateTime.Now + DelayMissingStepHandler;
                step.Description = msg;
                step.ExecutionCount++;
                persister.UpdateExecutedStep(StepStatus.Ready, step);
                persister.Commit();
                return WorkerRunStatus.Continue;
            }

            if (logger.DebugLoggingEnabled)
                logger.LogDebug($"{nameof(Worker)}: Executing step-implementation for step", null, CreateLogContext(step));
            ExecutionResult result;
            step.ExecutionStartTime = DateTime.Now;
            step.ExecutedBy = this.WorkerName;

            stopwatch.Restart();
            try
            {
                result = await implementation.ExecuteAsync(step);
            }
            catch (FailCurrentStepException e)
            {
                result = ExecutionResult.Fail(e.Message, e.NewSteps);
            }
            catch (Exception e)
            {
                if (logger.ErrorLoggingEnabled)
                    logger.LogError($"{nameof(Worker)}: exception during step execution. Will rerun step.", e, CreateLogContext(step));

                result = ExecutionResult.Rerun(description: e.Message);
            }
            // set time both for executions and executions with exception
            step.ExecutionDurationMillis = stopwatch.ElapsedMilliseconds;


            if (logger.DebugLoggingEnabled)
                logger.LogDebug($"{nameof(Worker)}: Executed step. Status: {result.Status}. new steps: {result.NewSteps?.Count() ?? 0}",
                    null,
                    CreateLogContext(step));

            FixupAfterExecution(step, result);

            try
            {
                persister.UpdateExecutedStep(result.Status, step);

                if (result.NewSteps != null)
                    persister.AddSteps(result.NewSteps.ToArray());

                persister.Commit();
            }
            catch (Exception e)
            {
                if (logger.ErrorLoggingEnabled)
                    logger.LogError($"{nameof(Worker)}: exception during saving step execution result and state.", e, CreateLogContext(step));

                StoppingToken.WaitHandle.WaitOne(DelayTechnicalTransientError);
            }

            return WorkerRunStatus.Continue;
        }
    }

    static DateTime CalculateScheduleTime(Step step)
    {
        var now = DateTime.Now;
        var future = now + Min(TimeSpan.FromHours(2), TimeSpan.FromSeconds(step.ExecutionCount * step.ExecutionCount * step.ExecutionCount));
        return WfRuntimeData.TrimToSeconds(future);
    }

    static TimeSpan Min(TimeSpan t1, TimeSpan t2) => t1 > t2 ? t2 : t1;

    void FixupAfterExecution(Step step, ExecutionResult result)
    {
        var now = DateTime.Now;

        step.ExecutionCount++;

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
        step.PersistedStateFormat = result.PersistedStateFormat ?? step.PersistedStateFormat;

        if (result.NewSteps != null)
        {
            foreach (var x in result.NewSteps)
                engineRuntimeData.FixupNewStep(step, x, now);
        }
    }
}
