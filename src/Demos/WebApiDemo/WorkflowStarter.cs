namespace MicroWorkflow;


public class WorkflowStarter : BackgroundService
{
    readonly WorkflowEngine engine;

    public WorkflowStarter(WorkflowEngine engine)
    {
        this.engine = engine;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Step step = new(StepFetchWeatherForecast.Name)
        { 
            Singleton = true, 
            Description= "continuously fetch the latest weather data and cache it" 
        };
        SearchModel searchModel = new(Name: step.Name, Singleton: step.Singleton);
        engine.Data.AddStepIfNotExists(step, searchModel);

        engine.StartAsync(new WorkflowConfiguration(new WorkerConfig()), stoppingToken: stoppingToken);

        await Task.CompletedTask;
    }
}
