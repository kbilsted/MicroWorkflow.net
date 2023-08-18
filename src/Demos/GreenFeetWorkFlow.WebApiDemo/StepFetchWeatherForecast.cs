namespace GreenFeetWorkflow.WebApiDemo;

[StepName(Name)]
class StepFetchWeatherForecast : IStepImplementation
{
    public const string Name = "singleton/v1/fetch-weather";

    public async Task<ExecutionResult> ExecuteAsync(Step step)
    {
        var weather = Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateTime.Now.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = WeaterForecastDB.Summaries[Random.Shared.Next(WeaterForecastDB.Summaries.Length)]
        }).ToList();

        WeaterForecastDB.LazyFetchedWeatherForecasts = weather;

        return await step.RerunAsync(scheduleTime: DateTime.Now.AddSeconds(3));
    }
}