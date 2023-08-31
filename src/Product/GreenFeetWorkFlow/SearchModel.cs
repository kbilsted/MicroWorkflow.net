namespace GreenFeetWorkflow;

public record SearchModel
(
     FetchLevels FetchLevel,
     int? Id = null,
     string? CorrelationId = null,
     string? SearchKey = null,
     string? Name = null,
     string? FlowId = null,
     string? Description = null
)
{ };

public record FetchLevels(bool Ready = false, bool Done = false, bool Fail = false, int MaxRows = 100)
{
    public static FetchLevels ALL = new FetchLevels(true, true, true);
    public static FetchLevels READY = new FetchLevels(true, false, false);
    public static FetchLevels NONREADY = new FetchLevels(false, true, true);
};
