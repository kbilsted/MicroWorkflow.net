﻿namespace GreenFeetWorkflow.WebApiDemo;


public class WorkflowStarter : BackgroundService
{
    readonly WorkflowEngine engine;

    public WorkflowStarter(WorkflowEngine engine)
    {
        this.engine = engine;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Step step = new Step(StepFetchWeatherForecast.Name) 
        { 
            Singleton = true, 
            Description= "continuously fetch the latest weather data and cache it" 
        };
        SearchModel searchModel = new SearchModel(Name: step.Name);
        engine.Data.AddStepIfNotExists(step, searchModel);

        engine.StartAsync(new WorkflowConfiguration(new WorkerConfig()), stoppingToken: stoppingToken);

        await Task.CompletedTask;
    }
}
