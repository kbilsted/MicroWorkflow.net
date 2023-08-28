namespace GreenFeetWorkflow;

/// <summary>
///  Simple in-memory storage FOR DEMO PURPOSES ONLY.
///  The current transaction handling is incorrect!! 
/// </summary>
public class DemoInMemoryPersister : IStepPersister
{
    readonly object GlobalLock = new();
    int GlobalId = 1;

    static readonly Dictionary<int, Step> ReadySteps = new();
    static readonly Dictionary<int, Step> DoneSteps = new();
    static readonly Dictionary<int, Step> FailedSteps = new();

    static readonly HashSet<int> Locked = new();

    HashSet<int> LocalTransaction = new();

    public object? Transaction { get; set; }

    public Step? GetAndLockReadyStep()
    {
        lock (GlobalLock)
        {
            var step = ReadySteps
                .Where(x =>
                x.Value.ScheduleTime <= DateTime.Now
                && !Locked.Contains(x.Value.Id))
                .Select(x => x.Value)
                .FirstOrDefault();

            if (step == null)
                return null;

            Locked.Add(step.Id);
            if (LocalTransaction.Any())
                throw new Exception("inside an existing transaction");
            LocalTransaction.Add(step.Id);

            return step;
        }
    }

    void FreeLocks()
    {
        lock (GlobalLock)
        {
            foreach (var step in LocalTransaction!)
                Locked.Remove(step);
            LocalTransaction.Clear();
        }
    }

    public void Commit() => FreeLocks();

    public void RollBack() => FreeLocks();



    public void Dispose()
    {
    }

    public object CreateTransaction()
    {
        LocalTransaction = new HashSet<int> { };
        return new object();
    }

    public Dictionary<StepStatus, IEnumerable<Step>> SearchSteps(SearchModel model)
    {
        IEnumerable<Step> ready = new List<Step>();
        if (model.FetchLevel.Ready)
        {
            ready = ReadySteps.Where(x =>
                (model.CorrelationId != null && x.Value.CorrelationId == model.CorrelationId)
                && (model.SearchKey != null && x.Value.SearchKey == model.SearchKey)
                && (model.FlowId != null && x.Value.FlowId == model.FlowId)
                && (model.Id != null && x.Value.Id == model.Id)
                && (model.Name != null && x.Value.Name == model.Name)
                )
                .Select(x => x.Value);
        }

        return new Dictionary<StepStatus, IEnumerable<Step>>()
        {
            { StepStatus.Ready, ready }
        };
    }

    public Dictionary<StepStatus, int> CountTables(string? flowId)
    {
        lock (GlobalLock)
        {
            var rdyCount = ReadySteps.Where(x => { return flowId == null ? true : x.Value.FlowId == flowId; }).Count();
            var doneCount = DoneSteps.Where(x => { return flowId == null ? true : x.Value.FlowId == flowId; }).Count();
            var failCount = FailedSteps.Where(x => { return flowId == null ? true : x.Value.FlowId == flowId; }).Count();

            return new Dictionary<StepStatus, int>()
            {
                { StepStatus.Ready, rdyCount },
                { StepStatus.Done, doneCount },
                { StepStatus.Failed, failCount },
            };
        }
    }

    public void SetTransaction(object transaction)
    {
    }

    public T InTransaction<T>(Func<T> code, object? transaction = null)
    {
        return code();
    }

    public int[] Insert(StepStatus target, Step[] steps)
    {
        lock (GlobalLock)
        {
            switch (target)
            {
                case StepStatus.Done:
                    steps.ToList().ForEach(x => DoneSteps.Add(x.Id, x));
                    return steps.Select(x => x.Id).ToArray();
                case StepStatus.Failed:
                    steps.ToList().ForEach(x => FailedSteps.Add(x.Id, x));
                    return steps.Select(x => x.Id).ToArray();
                case StepStatus.Ready:
                    foreach (var step in steps)
                    {
                        if (step.Singleton && ReadySteps.Any(x => x.Value.Name == step.Name))
                            throw new Exception("Cannot have duplicate 'ready' singleton steps");

                        step.Id = GlobalId++;
                        ReadySteps.Add(step.Id, step);
                    }
                    return steps.Select(step => step.Id).ToArray();
                default:
                    throw new Exception("unknown target");
            }
        }
    }

    public int Insert(StepStatus target, Step step)
    {
        if (target == StepStatus.Ready)
        {
            Insert(StepStatus.Ready, new[] { step });
            return step.Id;
        }

        lock (GlobalLock)
        {
            if (target == StepStatus.Failed)
                FailedSteps.Add(step.Id, step);
            if (target == StepStatus.Done)
                DoneSteps.Add(step.Id, step);

            return 1;
        }
    }

    public int Update(StepStatus target, Step step)
    {
        return 1;
    }

    public int Delete(StepStatus target, int id)
    {
        lock (GlobalLock)
        {
            if (target == StepStatus.Ready)
                return ReadySteps.Remove(id) ? 1 : 0;
            if (target == StepStatus.Done)
                return DoneSteps.Remove(id) ? 1 : 0;
            if (target == StepStatus.Failed)
                return FailedSteps.Remove(id) ? 1 : 0;
        }
        return 0;
    }
}
