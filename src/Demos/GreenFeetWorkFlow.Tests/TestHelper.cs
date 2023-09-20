using Autofac;
using GreenFeetWorkflow.Ioc.Autofac;
using GreenFeetWorkFlow.AdoMsSql;

namespace GreenFeetWorkflow.Tests;

public class TestHelper
{
    public string RndName = "test" + Guid.NewGuid().ToString();
    public NewtonsoftStateFormatterJson? Formatter;
    public CancellationTokenSource cts = new();
    public AutofacAdaptor? iocContainer;
    public SqlServerPersister Persister => (SqlServerPersister)iocContainer!.GetInstance<IStepPersister>();
    public readonly string CorrelationId = Guid.NewGuid().ToString();
    public readonly string FlowId = Guid.NewGuid().ToString();
    private IWorkflowLogger? logger;
    public WorkflowEngine? Engine;

    readonly ContainerBuilder builder = new ContainerBuilder();

    readonly LoggerConfiguration loggerConfiguration = new LoggerConfiguration()
    {
        ErrorLoggingEnabledUntil = DateTime.MaxValue,
        InfoLoggingEnabledUntil = DateTime.MaxValue,
        DebugLoggingEnabledUntil = DateTime.MaxValue,
        TraceLoggingEnabledUntil = DateTime.MaxValue,
    };

    public void CreateAndRunEngine(Step[] steps, params (string name, Func<Step, ExecutionResult> code)[] stepHandlers)
        => CreateAndRunEngine(
            steps,
            1,
            stepHandlers.Select(x => (x.name, (IStepImplementation)new GenericImplementation(x.code))).ToArray());

    public void CreateAndRunEngine(Step step, params (string, IStepImplementation)[] stepHandlers)
    => CreateAndRunEngine(new[] { step }, 1, stepHandlers);


    public void CreateAndRunEngine(Step[] steps, params (string, IStepImplementation)[] stepHandlers)
        => CreateAndRunEngine(steps, 1, stepHandlers);

    public readonly string ConnectionString = "Server=localhost;Database=adotest;Integrated Security=True;TrustServerCertificate=True";


    public WorkflowEngine CreateEngine(params (string, IStepImplementation)[] stepHandlers)
    {
        logger = new DiagnosticsStepLogger();
        ((DiagnosticsStepLogger)logger).AddNestedLogger(new ConsoleStepLogger());

        builder.RegisterInstances(logger, stepHandlers);
        builder.Register<IStepPersister>(c => new SqlServerPersister(ConnectionString, logger)).InstancePerDependency();

        Formatter = new NewtonsoftStateFormatterJson(logger);

        iocContainer = new AutofacAdaptor(builder.Build());
        Engine = new WorkflowEngine(logger, iocContainer, Formatter);

        return Engine;
    }

    public async Task CreateAndRunEngineForPerformance(Step[] steps, int workerCount, params (string, IStepImplementation)[] stepHandlers)
    {
        logger = new DiagnosticsStepLogger();
        logger.Configuration.TraceLoggingEnabledUntil = DateTime.MinValue;
        logger.Configuration.DebugLoggingEnabledUntil = DateTime.MinValue;
        logger.Configuration.InfoLoggingEnabledUntil = DateTime.MinValue;
        logger.Configuration.ErrorLoggingEnabledUntil = DateTime.MinValue;

        builder.RegisterInstances(logger, stepHandlers);
        builder.Register<IStepPersister>(c => new SqlServerPersister(ConnectionString, logger));

        Formatter = new NewtonsoftStateFormatterJson(logger);

        iocContainer = new AutofacAdaptor(builder.Build());
        Engine = new WorkflowEngine(logger, iocContainer, Formatter);

        await Engine.Data.AddStepsAsync(steps);

        var workflowConfiguration = new WorkflowConfiguration(new WorkerConfig()
        {
            StopWhenNoWork = false
        }, NumberOfWorkers: workerCount);

        Engine.Start(workflowConfiguration, stoppingToken: cts.Token);
    }

    public async Task CreateAndRunEngine(Step[] steps, int workerCount, params (string, IStepImplementation)[] stepHandlers)
    {
        logger = new DiagnosticsStepLogger();

        builder.RegisterInstances(logger, stepHandlers);
        builder.Register<IStepPersister>(c => new SqlServerPersister(ConnectionString, logger));

        Formatter = new NewtonsoftStateFormatterJson(logger);

        iocContainer = new AutofacAdaptor(builder.Build());
        Engine = new WorkflowEngine(logger, iocContainer, Formatter);

        await Engine.Data.AddStepsAsync(steps);

        var workflowConfiguration = new WorkflowConfiguration(new WorkerConfig()
        {
            StopWhenNoWork = workerCount == 1,
        }, NumberOfWorkers: workerCount);

        if (workerCount == 1)
            Engine.StartAsSingleWorker(workflowConfiguration, stoppingToken: cts.Token).GetAwaiter().GetResult();
        else
            Engine.Start(workflowConfiguration, stoppingToken: cts.Token);
    }

    public async Task CreateAndRunEngineWithAttributes(Step[] steps, int workerCount)
    {
        logger = new DiagnosticsStepLogger();

        builder.RegisterStepImplementations(logger, typeof(TestHelper).Assembly);
        builder.Register<IStepPersister>(c => new SqlServerPersister(ConnectionString, logger));

        Formatter = new NewtonsoftStateFormatterJson(logger);

        iocContainer = new AutofacAdaptor(builder.Build());
        Engine = new WorkflowEngine(logger, iocContainer, Formatter);

        await Engine.Data.AddStepsAsync(steps);

        var workflowConfiguration = new WorkflowConfiguration(new WorkerConfig()
        { StopWhenNoWork = workerCount == 1 },
        NumberOfWorkers: workerCount);

        Engine.Start(workflowConfiguration, stoppingToken: cts.Token);
    }

    public void CreateAndRunEngineWithAttributes(params Step[] steps)
    {
        CreateAndRunEngineWithAttributes(steps, 1);
    }

    public void AssertTableCounts(string flowId, int ready, int done, int failed)
    {
        IStepPersister persister = iocContainer!.GetInstance<IStepPersister>();
        persister
            .InTransaction(() => ((SqlServerPersister)persister).CountTables(flowId))
            .Should().BeEquivalentTo(
            new Dictionary<StepStatus, int>
            {
                { StepStatus.Ready, ready},
                { StepStatus.Done, done},
                { StepStatus.Failed, failed},
            });
    }

    public Step GetByFlowId(string flowId)
    {
        IStepPersister persister = iocContainer!.GetInstance<IStepPersister>();
        return persister
        .InTransaction(() =>
            persister.SearchSteps(new SearchModel(FlowId: flowId), FetchLevels.ALL))
        .SelectMany(x => x.Value)
        .First();
    }
}
