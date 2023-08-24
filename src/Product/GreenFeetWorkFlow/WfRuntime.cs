namespace GreenFeetWorkflow;

public class WfRuntime
{
    public WfRuntimeData Data { get; }
    public WfRuntimeMetrics Metrics { get; set; }
    public WfRuntimeConfiguration Configuration { get; set; }

    public WfRuntime(WfRuntimeData data, WfRuntimeMetrics metrics, WfRuntimeConfiguration configuration)
    {
        Data = data;
        Metrics = metrics;
        Configuration = configuration;
    }
}


