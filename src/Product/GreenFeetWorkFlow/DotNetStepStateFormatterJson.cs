using System.Text.Json;

namespace GreenFeetWorkflow;

public class DotNetStepStateFormatterJson : IWorkflowStepStateFormatter
{
    private readonly IWorkflowLogger logger;

    public string StateFormatName => "json";

    public DotNetStepStateFormatterJson(IWorkflowLogger logger)
    {
        this.logger = logger;
    }

    public string Serialize(object? binaryState)
    {
        try
        {
            return JsonSerializer.Serialize(binaryState);
        }
        catch (Exception ex)
        {
            if (logger.ErrorLoggingEnabled)
                logger.LogError($"Error serializing object.", ex, new Dictionary<string, object?>() { { "state", binaryState } });
            throw;
        }
    }

    public T? Deserialize<T>(string? state)
    {
        try
        {
            if (state == null) 
                return default;
            return JsonSerializer.Deserialize<T>(state);
        }
        catch (Exception ex)
        {
            if (logger.ErrorLoggingEnabled)
                logger.LogError($"Error deserializing object.", ex, new Dictionary<string, object?>() { { "json", state } });
            throw;
        }
    }
}