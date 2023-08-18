using Microsoft.AspNetCore.Mvc;

namespace GreenFeetWorkflow.WebApiDemo.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get() => WeaterForecastDB.LazyFetchedWeatherForecasts;
}
