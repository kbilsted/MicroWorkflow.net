namespace GreenFeetWorkflow;

public class WorkflowEngine
{
    private readonly IWorkflowLogger logger;
    private readonly IWorkflowIocContainer iocContainer;
    public string? EngineName { get; set; }
    public CancellationToken StoppingToken { get; set; }

    public IReadOnlyList<Worker>? WorkerList { get; private set; }
    public Thread[] Threads { get; private set; } = new Thread[0];

    public EngineRuntime Runtime { get; private set; }

    public bool StopWhenNoWorkLeft { get; set; }

    private readonly WfRuntimeData data;

    public WorkflowEngine(
        IWorkflowLogger logger,
        IWorkflowIocContainer iocContainer,
        IStateFormatter formatter)
    {
        this.logger = logger;
        this.iocContainer = iocContainer;

        data = new WfRuntimeData(iocContainer, formatter);
        Runtime = new EngineRuntime(data, new WfRuntimeMetrics(iocContainer));
    }

    static string MakeWorkerName(int i)
        => $"worker/{Environment.MachineName}/processid/{Environment.ProcessId}/instance/{i}";

    static string MakeEngineName()
        => $"engine/{Environment.MachineName}/processid/{Environment.ProcessId}/instance/{Random.Shared.Next(99999)}";

    void Init(int numberOfWorkers,
        bool? stopWhenNoWorkLeft = false,
        CancellationToken? stoppingToken = null,
        string? engineName = null)
    {
        if(logger.InfoLoggingEnabled)
            logger.LogInfo($"{nameof(WorkflowEngine)}: starting engine" + engineName, null, null);

        StoppingToken = stoppingToken ?? CancellationToken.None;

        EngineName = engineName ?? MakeEngineName();

        StopWhenNoWorkLeft = stopWhenNoWorkLeft ?? false;

        var workers = new Worker[numberOfWorkers];
        for (int i = 0; i < numberOfWorkers; i++)
        {
            workers[i] = new Worker(logger, iocContainer, data)
            {
                WorkerName = MakeWorkerName(i),
                StoppingToken = StoppingToken,
                StopWhenNoWorkLeft = StopWhenNoWorkLeft
            };
        }
        WorkerList = workers;
    }


    /// <summary> Starts the engine using 1 or more background threads </summary>
    public void Start(int numberOfWorkers,
        bool? stopWhenNoWorkLeft = false,
        CancellationToken? stoppingToken = null,
        string? engineName = null)
    {
        Init(numberOfWorkers, stopWhenNoWorkLeft, stoppingToken, engineName);

        Threads = WorkerList!
           .Select((x, i) =>
           {
               var t = new Thread(async () => await x.StartAsync())
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

    public async Task StartAsync(int numberOfWorkers,
        bool? stopWhenNoWorkLeft = false,
        CancellationToken? stoppingToken = null,
        string? engineName = null)
    {
        await Task.Run(() => Start(numberOfWorkers, stopWhenNoWorkLeft, stoppingToken, engineName));
    }


    /// <summary> Start the engine with the current thread as the worker. Also use this if you have trouble debugging weird scenarios </summary>
    public async Task StartAsync(
        bool? stopWhenNoWorkLeft = false,
        CancellationToken? stoppingToken = null,
        string? engineName = null)
    {
        Init(1, stopWhenNoWorkLeft, stoppingToken, engineName);

        await WorkerList!.Single().StartAsync();
    }
}

public class EngineRuntime
{
    public WfRuntimeData Data { get; }
    public WfRuntimeMetrics Metrics { get; set; }

    public EngineRuntime(WfRuntimeData data, WfRuntimeMetrics metrics)
    {
        Data = data;
        Metrics = metrics;  
    }
}


