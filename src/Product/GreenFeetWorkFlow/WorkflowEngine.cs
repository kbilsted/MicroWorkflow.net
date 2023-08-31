namespace GreenFeetWorkflow;

public class WorkflowEngine
{
    private readonly IWorkflowLogger logger;
    private readonly IWorkflowIocContainer iocContainer;
    public string? EngineName { get; set; }
    public CancellationToken StoppingToken { get; private set; }

    public IReadOnlyList<Worker>? WorkerList { get; private set; }
    public Thread[] Threads { get; private set; } = new Thread[0];

    public WfRuntime Runtime { get; private set; }


    private readonly WfRuntimeData data;

    public WorkflowEngine(
        IWorkflowLogger logger,
        IWorkflowIocContainer iocContainer,
        IWorkflowStepStateFormatter formatter)
    {
        this.logger = logger;
        this.iocContainer = iocContainer;

        data = new WfRuntimeData(iocContainer, formatter, logger);
        Runtime = new WfRuntime(data, new WfRuntimeMetrics(iocContainer), new WfRuntimeConfiguration(new WorkerConfig(), 0));
    }

    static string MakeWorkerName(int i)
        => $"worker/{Environment.MachineName}/process/{Environment.ProcessId}/{i}";

    static string MakeEngineName()
        => $"engine/{Environment.MachineName}/process/{Environment.ProcessId}/{Random.Shared.Next(99999)}";

    void Init(WfRuntimeConfiguration configuration, string? engineName = null)
    {
        if (logger.InfoLoggingEnabled)
            logger.LogInfo($"{nameof(WorkflowEngine)}: starting engine" + engineName, null, null);

        Runtime.Configuration = configuration;

        EngineName = engineName ?? MakeEngineName();

        var workers = new Worker[configuration.NumberOfWorkers];
        for (int i = 0; i < configuration.NumberOfWorkers; i++)
        {
            workers[i] = new Worker(logger, iocContainer, data, configuration.WorkerConfig)
            {
                WorkerName = MakeWorkerName(i),
            };
        }
        WorkerList = workers;
    }


    /// <summary> Starts the engine using 1 or more background threads </summary>
    public void Start(
        WfRuntimeConfiguration configuration,
        string? engineName = null,
        CancellationToken? stoppingToken = null)
    {
        Init(configuration, engineName);

        Threads = WorkerList!
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

    public async Task StartAsync(
        WfRuntimeConfiguration configuration,
        string? engineName = null,
        CancellationToken? stoppingToken = null)
    {
        await Task.Run(() => Start(configuration, engineName, stoppingToken));
    }


    /// <summary> Start the engine with the current thread as the worker. Also use this if you have trouble debugging weird scenarios </summary>
    public async Task StartAsSingleWorker(
        WfRuntimeConfiguration configuration,
        string? engineName = null,
        CancellationToken? stoppingToken = null)
    {
        Init(configuration, engineName);

        await WorkerList!.Single().StartAsync(stoppingToken ?? CancellationToken.None);
    }
}


