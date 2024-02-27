namespace MicroWorkflow;

/// <summary>
/// Shared among all workers of the same engine. Coordinates when new tasks spawn and may die
/// </summary>
public class WorkerCoordinator
{
    private readonly WorkerConfig config;
    private readonly CancellationTokenSource cts;
    private readonly IWorkflowLogger logger;
    private readonly Action newWorkerCreator;

    public int WorkerCount { get; private set; } = 0;
    public int TotalWorkerCreated { get; private set; } = 0;

    public WorkerCoordinator(WorkerConfig config, CancellationTokenSource cts, IWorkflowLogger logger, Action newWorkerCreator)
    {
        this.config = config;
        this.cts = cts;
        this.logger = logger;
        this.newWorkerCreator = newWorkerCreator;
    }

    public bool RoomForMoreWorkers => WorkerCount < config.MaxWorkerCount;

    public bool TryAddWorker()
    {
        lock (this)
        {
            if (!RoomForMoreWorkers)
                return false;

            WorkerCount++;
            TotalWorkerCreated++;
        }

        if (logger.TraceLoggingEnabled)
            logger.LogTrace($"{nameof(WorkerCoordinator)}: Worker count: {WorkerCount}/{config.MaxWorkerCount}. Total workers created: {TotalWorkerCreated}", null, null);

        // we dont use Task.Factory.StartNew() with async due to reasons stated in  https://blog.stephencleary.com/2013/08/startnew-is-dangerous.html            
        Task t = Task
            .Run(newWorkerCreator)
            .ContinueWith(x =>
            {
                //Console.WriteLine($"{x.Id} stopping worker..isfaulted:{x.IsFaulted}. count: {WorkerCount}  total workers created: " + TotalWorkerCreated);
                if (x.IsFaulted)
                {
                    if (logger.ErrorLoggingEnabled)
                        logger.LogError("Unhandled exception during worker execution",
                            x.Exception,
                            new Dictionary<string, object?>
                            {
                                {"workercount", WorkerCount},
                                {"totalworkerscreated", TotalWorkerCreated}
                            });
                }
            }, cts.Token);

        return true;
    }

    public bool MayWorkerDie()
    {
        lock (this)
        {
            // determine if we are any of the last remainders before any code 
            bool AreWeAnyOfTheLastRemainingMinWorkers = WorkerCount == config.MinWorkerCount;

            if (config.StopWhenNoImmediateWork)
            {
                if (logger.TraceLoggingEnabled)
                    logger.LogTrace($"{nameof(WorkerCoordinator)}: MayWorkerDie  WorkerCount:{WorkerCount} MinWorkers:{config.MinWorkerCount}", null, null);

                if (AreWeAnyOfTheLastRemainingMinWorkers)
                {
                    if (logger.TraceLoggingEnabled)
                        logger.LogTrace($"{nameof(WorkerCoordinator)}: cts.cancel", null, null);

                    cts.Cancel();
                }
            }

            if (AreWeAnyOfTheLastRemainingMinWorkers)
                return false;

            WorkerCount--;
            return true;
        }
    }
}
