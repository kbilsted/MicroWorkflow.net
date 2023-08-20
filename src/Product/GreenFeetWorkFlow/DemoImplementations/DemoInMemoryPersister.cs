﻿namespace GreenFeetWorkflow;

/// <summary>
///  Simple in-memory storage with support for locking
/// </summary>
public class DemoInMemoryPersister : IStepPersister
{
    readonly object GlobalLock = new();
    int GlobalId = 1;

    static readonly Dictionary<int, Step> ReadySteps = new();
    static readonly HashSet<int> Locked = new();
    HashSet<int>? LockedLocal;

    public object? Transaction { get; set; }

    public Step? GetStep()
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
            if (LockedLocal != null)
                throw new Exception("inside an existing transaction");
            LockedLocal = new HashSet<int>
            {
                step.Id
            };

            return step;
        }
    }

    public void Commit(StepStatus status, Step executedStep, List<Step>? newSteps)
    {
        lock (GlobalLock)
        {
            Locked.Remove(executedStep.Id);

            if (LockedLocal == null)
                throw new Exception("not inside a transaction");

            LockedLocal.Remove(executedStep.Id);

            if (status != StepStatus.Ready)
                ReadySteps.Remove(executedStep.Id);

            if (newSteps == null)
                return;

            foreach (var step in newSteps)
            {
                if (step.CorrelationId == null)
                    throw new NullReferenceException("step correlationId cannot be null");

                step.Id = GlobalId++;
                ReadySteps.Add(step.Id, step);
            }
        }
    }

    public void RollBack()
    {
        lock (GlobalLock)
        {
            foreach (var step in LockedLocal!)
                Locked.Remove(step);
            LockedLocal = null;
        }
    }

    public int[] AddSteps(object? transaction = null, params Step[] steps)
    {
        lock (GlobalLock)
        {
            foreach (var step in steps)
            {
                step.Id = GlobalId++;
                ReadySteps.Add(step.Id, step);
            }
        }

        return steps.Select(step => step.Id).ToArray();
    }

    public void Dispose()
    {
    }

    public int ActivateStep(int id, string? activationData)
    {
        throw new NotImplementedException();
    }

    public object CreateTransaction()
    {
        LockedLocal = new HashSet<int> { };
        return new object();
    }

    public Dictionary<StepStatus, IEnumerable<Step>> SearchSteps(SearchModel model)
    {
        IEnumerable<Step> ready = new List<Step>();
        if (model.FetchLevel.IncludeReady)
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

    public int[] ReExecuteSteps(Dictionary<StepStatus, IEnumerable<Step>> entities)
    {
        throw new NotImplementedException();
    }
}
