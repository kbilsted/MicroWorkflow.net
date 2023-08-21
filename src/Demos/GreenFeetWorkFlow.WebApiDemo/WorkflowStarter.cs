namespace GreenFeetWorkflow.WebApiDemo;


public class WorkflowStarter : BackgroundService
{
    readonly WorkflowEngine engine;

    public WorkflowStarter(WorkflowEngine engine)
    {
        this.engine = engine;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        engine.Runtime.Data.AddStep(new Step(StepFetchWeatherForecast.Name) { Singleton = true });

        await engine.StartAsync(1, stoppingToken: stoppingToken);
    }
}
