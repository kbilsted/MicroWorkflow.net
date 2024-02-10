using Microsoft.AspNetCore.Mvc;

namespace MicroWorkflow;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get() => WeaterForecastDB.LazyFetchedWeatherForecasts;
}
