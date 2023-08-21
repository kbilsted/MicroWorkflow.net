using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace GreenFeetWorkflow.AdoPersistence;

public class AdoDbStepPersister : IStepPersister
{
    public string TableNameReady { get; set; } = "[dbo].[Steps_Ready]";
    public string TableNameFail { get; set; } = "[dbo].[Steps_Fail]";
    public string TableNameDone { get; set; } = "[dbo].[Steps_Done]";

    private readonly string connectionString;
    private readonly IWorkflowLogger logger;
    SqlConnection? connection = null;

    SqlTransaction? Transaction;

    Guid PersisterId = Guid.NewGuid();

    public void SetTransaction(object transaction)
    {
        Transaction = (SqlTransaction?)transaction;
    }

    readonly AdoHelper helper = new();

    public AdoDbStepPersister(string connectionString, IWorkflowLogger logger)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        this.logger = logger;
    }

    public object CreateTransaction()
    {
        if (Transaction != null)
            throw new InvalidOperationException("Cannot make a new transaction when a transaction is executing");

        connection = new SqlConnection(connectionString);

        connection.Open();

        Transaction = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        return Transaction;
    }

    public T Go<T>(Func<IStepPersister, T> code, object? transaction = null)
    {
        try
        {
            if (transaction == null)
                CreateTransaction();
            else
                SetTransaction(transaction);

            T result = code(this);

            if (transaction == null)
            {
                Console.WriteLine(Thread.CurrentThread+ " AUTOCOMMIT");
                Commit();
            }
            return result;
        }
        catch (Exception)
        {
            if (transaction == null)
                RollBack();
            throw;
        }
    }


    public Dictionary<StepStatus, IEnumerable<Step>> SearchSteps(SearchModel model)
    {
        if (Transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        List<Step> ready = model.FetchLevel.IncludeReady
            ? helper.SearchSteps(TableNameReady, model, Transaction!)
            : new List<Step>();
        List<Step> done = model.FetchLevel.IncludeDone
            ? helper.SearchSteps(TableNameDone, model, Transaction!)
            : new List<Step>();
        List<Step> fail = model.FetchLevel.IncludeFail
            ? helper.SearchSteps(TableNameFail, model, Transaction!)
            : new List<Step>();

        return new Dictionary<StepStatus, IEnumerable<Step>>()
        {
            { StepStatus.Ready, ready },
            { StepStatus.Done, done },
            { StepStatus.Failed, fail },
        };
    }

    public int[] ReExecuteSteps(Dictionary<StepStatus, IEnumerable<Step>> entities)
    {
        if (Transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        if (entities.ContainsKey(StepStatus.Ready) && entities[StepStatus.Ready].Any())
            throw new ArgumentOutOfRangeException("Cannot re-execute ready steps");

        List<int> ids = new List<int>();

        var now = DateTime.Now;

        foreach (KeyValuePair<StepStatus, IEnumerable<Step>> steps in entities)
        {
            foreach (var step in steps.Value)
            {
                int id = helper.InsertStep(
                    StepStatus.Ready, 
                    TableNameReady, 
                    new Step()
                    {
                        FlowId = step.FlowId,
                        CorrelationId = step.CorrelationId,
                        CreatedByStepId = step.CreatedByStepId,
                        CreatedTime = now,
                        Description = $"Re-execution of step {nameof(id)} " + step.Id,
                        PersistedState = step.PersistedState,
                        PersistedStateFormat = step.PersistedStateFormat,
                        ScheduleTime = now,
                        Singleton = step.Singleton,
                        SearchKey = step.SearchKey,
                        Name = step.Name,
                    }, Transaction!);

                ids.Add(id);
            }
        }

        return ids.ToArray();
    }


    public Step? GetAndLockReadyStep()
    {
        if (Transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        return helper.GetAndLockReadyStep(TableNameReady, Transaction!);
    }

    public Step? GetStep(int id)
    {
        if (Transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        return helper.GetStep(TableNameReady, id, Transaction!);
    }

    public void UpdateExecutedStep(StepStatus status, Step executedStep)
    {
        if (Transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        switch (status)
        {
            case StepStatus.Done:
                helper.DeleteReady(executedStep, TableNameReady, Transaction!);
                helper.InsertStep(StepStatus.Done, TableNameDone, executedStep, Transaction!);
                break;

            case StepStatus.Failed:
                helper.DeleteReady(executedStep, TableNameReady, Transaction!);
                helper.InsertStep(StepStatus.Failed, TableNameFail, executedStep, Transaction!);
                break;

            case StepStatus.Ready:
                helper.UpdateReady(executedStep, TableNameReady, Transaction!);
                break;
        }
    }

    public void Commit()
    {
        if (logger.TraceLoggingEnabled)
            logger.LogTrace($"{nameof(AdoDbStepPersister)}: Commit Transaction", null, new Dictionary<string, object?>() { 
                { "PersisterId", PersisterId } ,
                { "Stack", new StackTrace().ToString()} ,
            });

        if (Transaction == null)
            throw new InvalidOperationException("There is no transaction to commit");
        Transaction.Commit();
        Transaction.Dispose();
        Transaction = null;

        if (connection == null)
            throw new InvalidOperationException("There is no open connection to close");

        connection.Dispose();
        connection = null;
    }

    public void RollBack()
    {
        if (logger.TraceLoggingEnabled)
            logger.LogTrace($"{nameof(AdoDbStepPersister)}: Rollback Transaction", null, 
                new Dictionary<string, object?>() { { "PersisterId", PersisterId } });

        if (Transaction == null)
            throw new InvalidOperationException("There is no transaction to rollback");
        Transaction.Rollback();
        Transaction.Dispose();
        Transaction = null;

        if (connection == null)
            throw new InvalidOperationException("There is no open connection");
        connection.Dispose();
        connection = null;
    }

    public int[] AddSteps(Step[] steps)
    {
        if (Transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        if (!steps.Any())
            return new int[0];

        var result = new List<int>();

        foreach (var x in steps)
        {
            result.Add(helper.InsertStep(StepStatus.Ready, TableNameReady, x, Transaction!));
        }

        return result.ToArray();
    }

    public Dictionary<StepStatus, int> CountTables(string? flowId = null)
    {
        if (Transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        var result = helper.CountTables(flowId, TableNameReady, TableNameDone, TableNameFail, Transaction!);
        return result;
    }

    // TODO lav test case på at man aktiverer et eksekverende step - som dermed er skrive-låst - skal nok anvende en 2s timeout
    public int UpdateStep(int id, string? activationData, DateTime scheduleTime)
    {
        if (Transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        int rows = helper.UpdateStep(TableNameReady, id, scheduleTime, activationData, Transaction!);
        return rows;
    }

    public void Dispose()
    {
        if (logger.TraceLoggingEnabled)
            logger.LogTrace($"{nameof(AdoDbStepPersister)}: DISPOSE()", null, new Dictionary<string, object?>() { { "PersisterId", PersisterId } });

        if (Transaction != null)
        {
            Transaction.Dispose();
            Transaction = null;
        }

        if (connection != null)
        {
            connection.Dispose();
            connection = null;
        }

        GC.SuppressFinalize(this);
    }
}
