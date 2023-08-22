using Autofac;
using GreenFeetWorkflow.AdoPersistence;
using GreenFeetWorkflow.Ioc.Autofac;

namespace GreenFeetWorkflow.Tests;

public class TestHelper
{
    public string RndName = "test" + Guid.NewGuid().ToString();
    public NewtonsoftStateFormatterJson? Formatter;
    public CancellationTokenSource cts = new();
    public AutofacBinding? iocContainer;
    public AdoDbStepPersister Persister => (AdoDbStepPersister)iocContainer!.GetInstance<IStepPersister>();
    public readonly string CorrelationId = Guid.NewGuid().ToString();
    public readonly string FlowId = Guid.NewGuid().ToString();
    private IWorkflowLogger? logger;
    public WorkflowEngine? Engine;

    public void CreateAndRunEngine(Step[] steps, params (string name, Func<Step, ExecutionResult> code)[] stepHandlers)
        => CreateAndRunEngine(
            steps,
            1,
            stepHandlers.Select(x => (x.name, (IStepImplementation)new GenericStepHandler(x.code))).ToArray());

    public void CreateAndRunEngine(Step step, params (string, IStepImplementation)[] stepHandlers)
    => CreateAndRunEngine(new[] { step }, 1, stepHandlers);


    public void CreateAndRunEngine(Step[] steps, params (string, IStepImplementation)[] stepHandlers)
        => CreateAndRunEngine(steps, 1, stepHandlers);

    public void TurnOffLogging()
    {
        logger!.TraceLoggingEnabled = true;
        logger.DebugLoggingEnabled = true;
        logger.InfoLoggingEnabled = true;
        logger.ErrorLoggingEnabled = true;
    }

    public readonly string ConnectionString = "Server=localhost;Database=adotest;Integrated Security=True;TrustServerCertificate=True";


    public WorkflowEngine CreateEngine(params (string, IStepImplementation)[] stepHandlers)
    {
        logger = new DiagnosticsStepLogger();
        logger.TraceLoggingEnabled = true;
        logger.DebugLoggingEnabled = true;
        logger.InfoLoggingEnabled = true;
        logger.ErrorLoggingEnabled = true;
        ((DiagnosticsStepLogger)logger).AddNestedLogger(new ConsoleStepLogger());

        var builder = new ContainerBuilder();
        builder.RegisterInstances(logger, stepHandlers);
        builder.Register<IStepPersister>(c => new AdoDbStepPersister(ConnectionString, logger)).InstancePerDependency();
        iocContainer = new AutofacBinding(builder);

        Formatter = new NewtonsoftStateFormatterJson(logger);

        Engine = new WorkflowEngine(logger, iocContainer, Formatter);

        return Engine;
    }

    public void CreateAndRunEngine(Step[] steps, int workerCount, params (string, IStepImplementation)[] stepHandlers)
    {
        logger = new DiagnosticsStepLogger();

        var builder = new ContainerBuilder();
        builder.RegisterInstances(logger, stepHandlers);
        builder.Register<IStepPersister>(c => new AdoDbStepPersister(ConnectionString, logger));
        iocContainer = new AutofacBinding(builder);

        Formatter = new NewtonsoftStateFormatterJson(logger);

        Engine = new WorkflowEngine(logger, iocContainer, Formatter);

        Engine.Runtime.Data.AddSteps(steps);

        if (workerCount == 1)
            Engine.StartAsync(true).GetAwaiter().GetResult();
        else
            Engine.Start(numberOfWorkers: workerCount, stopWhenNoWorkLeft: false, cts.Token);
    }

    public void CreateAndRunEngineWithAttributes(Step[] steps, int workerCount)
    {
        logger = new DiagnosticsStepLogger();

        var builder = new ContainerBuilder();
        builder.RegisterStepImplementations(logger, typeof(TestHelper).Assembly);
        builder.Register<IStepPersister>(c => new AdoDbStepPersister(ConnectionString, logger));
        iocContainer = new AutofacBinding(builder);


        Formatter = new NewtonsoftStateFormatterJson(logger);

        Engine = new WorkflowEngine(logger, iocContainer, Formatter);

        Engine.Runtime.Data.AddSteps(steps);

        Engine.Start(numberOfWorkers: workerCount, stopWhenNoWorkLeft: workerCount == 1, cts.Token);
    }

    public void CreateAndRunEngineWithAttributes(params Step[] steps)
    {
        CreateAndRunEngineWithAttributes(steps, 1);
    }

    public void AssertTableCounts(string flowId, int ready, int done, int failed)
    {
        iocContainer.GetInstance<IStepPersister>()
            .Go((persister) => ((AdoDbStepPersister)persister).CountTables(flowId))
            .Should().BeEquivalentTo(
            new Dictionary<StepStatus, int>
            {
                { StepStatus.Ready, ready},
                { StepStatus.Done, done},
                { StepStatus.Failed, failed},
            });
    }
}
