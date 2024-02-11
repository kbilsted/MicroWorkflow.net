namespace MicroWorkflow;

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

public record FetchLevels(bool Ready = false, bool Done = false, bool Fail = false, int MaxRows = 1000)
{
    public static readonly FetchLevels ALL = new(true, true, true);
    public static readonly FetchLevels READY = new(true, false, false);
    public static readonly FetchLevels NONREADY = new(false, true, true);
    public static readonly FetchLevels FAILED = new(false, false, true);
};
