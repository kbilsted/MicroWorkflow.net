namespace GreenFeetWorkflow;


public record SearchModel
(
 int? Id = null,
 string? CorrelationId = null,
 string? SearchKey = null,
 string? Name = null,
 string? FlowId = null
)
{
    public FetchLevels FetchLevel { get; set; } = new();
};

public record FetchLevels(bool Ready = false, bool Done = false, bool Fail = false, int MaxRows = 100);
