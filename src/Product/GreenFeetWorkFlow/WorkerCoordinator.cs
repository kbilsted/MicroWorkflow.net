namespace GreenFeetWorkflow;

/// <summary>
/// Shared amon all workers of the same engine. Coordinates when new tasks spawn and may die
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
            logger.LogTrace($"{nameof(WorkerCoordinator)}: starting worker count: {WorkerCount} / {TotalWorkerCreated} total", null, null);

        // dont use Task.Factory.StartNew() with async:  https://blog.stephencleary.com/2013/08/startnew-is-dangerous.html            
        Task t = Task
            .Run(newWorkerCreator)
            .ContinueWith(x =>
            {
                //Console.WriteLine($"{x.Id} stopping worker..isfaulted:{x.IsFaulted}. count: {WorkerCount}  total workers created: " + TotalWorkerCreated);
                if (x.IsFaulted)
                {
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

    public void ForceStopEngine()
    {
        cts.Cancel();
    }

    public bool MayWorkerDie()
    {
        lock (this)
        {
            if (WorkerCount == config.MinWorkerCount)
                return false;

            WorkerCount--;
            return true;
        }
    }
}
