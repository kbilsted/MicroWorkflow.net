namespace GreenFeetWorkflow;

public class SearchModel
{
    public int? Id { get; set; }
    public string? CorrelationId { get; set; }
    public string? SearchKey { get; set; }
    public string? FlowId { get; set; }
    public FetchLevels FetchLevel { get; set; }

    public struct FetchLevels
    {
        public bool IncludeReady { get; set; }
        public bool IncludeDone { get; set; }
        public bool IncludeFail { get; set; }
    }
}