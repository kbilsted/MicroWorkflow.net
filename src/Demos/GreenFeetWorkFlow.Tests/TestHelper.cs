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
    public IWorkflowLogger? Logger;
    public WorkflowEngine? Engine;
    public (string, IStepImplementation)[] StepHandlers { get; set; } = new (string, IStepImplementation)[0];

    readonly ContainerBuilder builder = new ContainerBuilder();
    public string ConnectionString = "Server=localhost;Database=adotest;Integrated Security=True;TrustServerCertificate=True";

    public Step[] Steps = Array.Empty<Step>();

    public LoggerConfiguration LoggerConfiguration = new LoggerConfiguration()
    {
        ErrorLoggingEnabledUntil = DateTime.MaxValue,
        InfoLoggingEnabledUntil = DateTime.MaxValue,
        DebugLoggingEnabledUntil = DateTime.MaxValue,
        TraceLoggingEnabledUntil = DateTime.MinValue,
    };

    public WorkflowConfiguration WorkflowConfiguration;

    public TestHelper()
    {
        WorkflowConfiguration = new WorkflowConfiguration(
            new WorkerConfig())
        {
            LoggerConfiguration = LoggerConfiguration
        };
    }
    public static (string, IStepImplementation) Handle(string name, Func<Step, ExecutionResult> code) => (name, new GenericImplementation(code));

    public WorkflowEngine Build()
    {
        if (Logger == null)
        {
            Logger = new DiagnosticsStepLogger(WorkflowConfiguration.LoggerConfiguration);
            ((DiagnosticsStepLogger)Logger).AddNestedLogger(new ConsoleStepLogger(WorkflowConfiguration.LoggerConfiguration));
        }

        builder.RegisterInstances(Logger, StepHandlers);
        builder.Register<IStepPersister>(c => new SqlServerPersister(ConnectionString, Logger)).InstancePerDependency();

        // register all classes having a [step] attribute
        builder.RegisterStepImplementations(Logger, typeof(TestHelper).Assembly);

        Formatter ??= new NewtonsoftStateFormatterJson(Logger);

        iocContainer = new AutofacAdaptor(builder.Build());
        Engine = new WorkflowEngine(Logger, iocContainer, Formatter);

        Engine.Data.AddSteps(Steps);

        return Engine;
    }

    public TestHelper With(Action<TestHelper> action)
    {
        action(this);
        return this;
    }

    public TestHelper UseMax1Worker()
    {
        WorkflowConfiguration.WorkerConfig.MaxWorkerCount = 1;
        return this;
    }

    public TestHelper StopWhenNoWork()
    {
        WorkflowConfiguration.WorkerConfig.StopWhenNoImmediateWork = true;
        return this;
    }

    public WorkflowEngine BuildAndStart()
    {
        Build();
        return Start();
    }

    public WorkflowEngine StartAsync()
    {
        Engine!.StartAsync(WorkflowConfiguration, stoppingToken: cts.Token);
        return Engine;
    }

    public WorkflowEngine Start()
    {
        if (Engine == null) throw new Exception("Remember to 'build' before 'start'");
        Engine!.Start(WorkflowConfiguration, stoppingToken: cts.Token);
        return Engine;
    }

    public void AssertTableCounts(string flowId, int ready, int done, int failed)
    {
        var p = Persister;
        p.InTransaction(() => p.CountTables(flowId))
            .Should().BeEquivalentTo(
            new Dictionary<StepStatus, int>
            {
                { StepStatus.Ready, ready},
                { StepStatus.Done, done},
                { StepStatus.Failed, failed},
            });
    }
}
