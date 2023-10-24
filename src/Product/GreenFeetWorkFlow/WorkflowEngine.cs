namespace GreenFeetWorkflow;

public class WorkflowEngine
{
    private readonly IWorkflowLogger logger;
    private readonly IWorkflowIocContainer iocContainer;
    public string? EngineName { get; set; }
    public CancellationToken StoppingToken { get; private set; }

    public IReadOnlyList<Worker>? Workers { get; private set; }
    public Thread[] Threads { get; private set; } = new Thread[0];


    public WorkflowEngine(
        IWorkflowLogger logger,
        IWorkflowIocContainer iocContainer,
        IWorkflowStepStateFormatter formatter)
    {
        this.logger = logger;
        this.iocContainer = iocContainer;

        Data = new WorkflowRuntimeData(iocContainer, formatter, logger);
        Metrics = new WorkflowRuntimeMetrics(iocContainer);
    }

    /// <summary> Access the steps </summary>
    public WorkflowRuntimeData Data { get; }

    /// <summary> Performance metrics </summary>
    public WorkflowRuntimeMetrics Metrics { get; set; }

    /// <summary> Engine configuration </summary>
    public WorkflowConfiguration Configuration { get; set; }

    static string MakeWorkerName(int i)
        => $"worker/{Environment.MachineName}/process/{Environment.ProcessId}/{i}";

    static string MakeEngineName()
        => $"engine/{Environment.MachineName}/process/{Environment.ProcessId}/{Random.Shared.Next(99999)}";

    void Init(WorkflowConfiguration configuration, string? engineName)
    {
        if (logger.InfoLoggingEnabled)
            logger.LogInfo($"{nameof(WorkflowEngine)}: starting engine" + engineName, null, null);

        Configuration = configuration;
        configuration.LoggerConfiguration = logger.Configuration;

        EngineName = engineName ?? MakeEngineName();

        var workers = new Worker[configuration.NumberOfWorkers];
        for (int i = 0; i < configuration.NumberOfWorkers; i++)
        {
            workers[i] = new Worker(logger, iocContainer, Data, configuration.WorkerConfig)
            {
                WorkerName = MakeWorkerName(i),
            };
        }
        Workers = workers;
    }

    /// <summary> Starts the engine using 1 or more background threads in a non-async context </summary>
    public void Start(
        WorkflowConfiguration configuration,
        string? engineName = null,
        CancellationToken? stoppingToken = null)
    {
        Init(configuration, engineName);

        Threads = Workers!
           .Select((x, i) =>
           {
               var t = new Thread(async () => await x.StartAsync(stoppingToken ?? CancellationToken.None))
               {
                   Name = x.WorkerName,
                   IsBackground = true // c# automatically shuts down background threads when all foreground threads are terminated
               };

               return t;
           })
           .ToArray();

        foreach (var thread in Threads)
            thread.Start();

        foreach (var thread in Threads)
            thread.Join();
    }

    /// <summary> Starts the engine using 1 or more background threads in an async context</summary>
    public async Task StartAsync(
        WorkflowConfiguration configuration,
        string? engineName = null,
        CancellationToken? stoppingToken = null)
    {
        await Task.Run(() => Start(configuration, engineName, stoppingToken));
    }


    /// <summary> Start the engine with the current thread as the worker. Also use this if you have trouble debugging weird scenarios </summary>
    public async Task StartAsSingleWorker(
        WorkflowConfiguration configuration,
        string? engineName = null,
        CancellationToken? stoppingToken = null)
    {
        Init(configuration, engineName);

        if (configuration.NumberOfWorkers != 1)
            throw new ArgumentException($"{nameof(configuration.NumberOfWorkers)} must have the value '1' since we are running in the current thread.");

        await Workers!.Single().StartAsync(stoppingToken ?? CancellationToken.None);
    }
}


