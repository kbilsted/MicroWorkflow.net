namespace GreenFeetWorkflow;

public class WorkflowEngine
{
    public readonly IWorkflowLogger logger;
    public readonly IWorkflowIocContainer iocContainer;
    public string? EngineName { get; set; }
    public CancellationToken StoppingToken { get; private set; }

    public WorkerCoordinator WorkerCoordinator;

    public WorkflowEngine(
        IWorkflowLogger logger,
        IWorkflowIocContainer iocContainer,
        IWorkflowStepStateFormatter formatter)
    {
        this.logger = logger;
        this.iocContainer = iocContainer;

        Data = new WorkflowRuntimeData(iocContainer, formatter, logger, null);
        Metrics = new WorkflowRuntimeMetrics(iocContainer);
    }

    /// <summary>
    /// used for force stopping an engine in unittest
    /// </summary>
    private CancellationTokenSource? cts;

    /// <summary> Access the steps </summary>
    public WorkflowRuntimeData Data { get; }

    /// <summary> Performance metrics </summary>
    public WorkflowRuntimeMetrics Metrics { get; set; }

    /// <summary> Engine configuration </summary>
    public WorkflowConfiguration Configuration { get; set; }

    static string MakeWorkerName()
        => $"{Environment.MachineName}/pid/{Environment.ProcessId}/{Random.Shared.Next(99999)}";
    static string MakeEngineName()
         => $"{Environment.MachineName}/pid/{Environment.ProcessId}";

    void Init(WorkflowConfiguration configuration, string? engineName, CancellationToken? token)
    {
        if (logger.InfoLoggingEnabled)
            logger.LogInfo($"{nameof(WorkflowEngine)}: starting engine {engineName}", null, null);

        Configuration = configuration;
        configuration.LoggerConfiguration = logger.Configuration;

        EngineName = engineName ?? MakeEngineName();

        if (token == null)
        {
            cts = configuration.WorkerConfig.StopWhenNoImmediateWork ? new CancellationTokenSource() : null;
            if (cts == null)
                throw new Exception("Either use a CancellationToken as a parameter, or use 'WorkerConfig.StopWhenNoImmediateWork=true'");
            StoppingToken= cts.Token;
        }
        else
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(token.Value);
            StoppingToken = cts.Token;
        }

        WorkerCoordinator = new WorkerCoordinator(
            configuration.WorkerConfig, 
            cts, 
            logger, 
            async () => new Worker(MakeWorkerName(), logger, iocContainer, Data, configuration.WorkerConfig, WorkerCoordinator).StartAsync(StoppingToken));

        if (configuration.WorkerConfig.MinWorkerCount < 1)
            throw new Exception("'MinWorkerCount' cannot be less than 1");
        if(configuration.WorkerConfig.MaxWorkerCount< configuration.WorkerConfig.MinWorkerCount)
            throw new Exception("'MaxWorkerCount' cannot be less than 'MinWorkerCount'");
        for (int i = 0; i < configuration.WorkerConfig.MinWorkerCount; i++)
        {
            WorkerCoordinator.TryAddWorker();
        }

        Data.WorkerCoordinator = WorkerCoordinator;
    }

    /// <summary>
    /// start the engine, which starts workers in the back ground
    /// </summary>
    public void StartAsync(
        WorkflowConfiguration configuration,
        string? engineName = null,
        CancellationToken? stoppingToken = null)
    {
        Init(configuration, engineName, stoppingToken);
    }

    /// <summary>
    /// Start the engine and await the stopping token gets cancelled
    /// </summary>
    public void Start(
    WorkflowConfiguration configuration,
    string? engineName = null,
    CancellationToken? stoppingToken = null)
    {
        StartAsync(configuration, engineName, stoppingToken);    

        StoppingToken.WaitHandle.WaitOne();
    }
}
