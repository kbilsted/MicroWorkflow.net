namespace GreenFeetWorkflow;

public record SearchModel
(
     int? Id = null,
     string? Name = null,
     bool? Singleton = null,
     string? FlowId = null,
     string? SearchKey = null,
     string? ExecutedBy = null,
     string? CorrelationId = null,
     DateTime? CreatedTimeFrom = null,
     DateTime? CreatedTimeUpto = null,
     string? Description = null
);

public record FetchLevels(bool Ready = false, bool Done = false, bool Fail = false, int MaxRows = 100)
{
    public static FetchLevels ALL = new FetchLevels(true, true, true);
    public static FetchLevels READY = new FetchLevels(true, false, false);
    public static FetchLevels NONREADY = new FetchLevels(false, true, true);
    public static FetchLevels FAILED = new FetchLevels(false, false, true);
};
