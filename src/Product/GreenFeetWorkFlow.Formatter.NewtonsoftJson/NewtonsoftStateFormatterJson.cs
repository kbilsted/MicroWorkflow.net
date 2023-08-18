using Newtonsoft.Json;

namespace GreenFeetWorkflow;


public class NewtonsoftStateFormatterJson : IStateFormatter
{
    private readonly IWorkflowLogger logger;

    public string StateFormatName => "json";

    public NewtonsoftStateFormatterJson(IWorkflowLogger logger)
    {
        this.logger = logger;
    }

    public string Serialize(object? binaryState)
    {
        try
        {
            return JsonConvert.SerializeObject(binaryState);
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
            return JsonConvert.DeserializeObject<T>(state);
        }
        catch (Exception ex)
        {
            if (logger.ErrorLoggingEnabled)
                logger.LogError($"Error deserializing object.", ex, new Dictionary<string, object?>() { { "json", state } });
            throw;
        }
    }
}
