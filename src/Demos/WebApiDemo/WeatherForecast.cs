namespace MicroWorkflow;

public class WeatherForecast
{
    public DateTime Date { get; set; }

    public int TemperatureC { get; set; }

    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    public string? Summary { get; set; }
}


/// <summary>
/// simulate DB access
/// </summary>
public class WeaterForecastDB
{
    public static List<WeatherForecast> LazyFetchedWeatherForecasts { get; set; } = new List<WeatherForecast>();

    internal static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };
}