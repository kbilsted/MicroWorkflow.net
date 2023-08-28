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

    readonly Guid PersisterId = Guid.NewGuid();

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

    public T InTransaction<T>(Func<T> code, object? transaction = null)
    {
        try
        {
            if (transaction == null)
                CreateTransaction();
            else
                SetTransaction(transaction);

            T result = code();

            if (transaction == null)
            {
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

        List<Step> ready = model.FetchLevel.Ready
            ? helper.SearchSteps(TableNameReady, model, Transaction!)
            : new List<Step>();
        List<Step> done = model.FetchLevel.Done
            ? helper.SearchSteps(TableNameDone, model, Transaction!)
            : new List<Step>();
        List<Step> fail = model.FetchLevel.Fail
            ? helper.SearchSteps(TableNameFail, model, Transaction!)
            : new List<Step>();

        return new Dictionary<StepStatus, IEnumerable<Step>>()
        {
            { StepStatus.Ready, ready },
            { StepStatus.Done, done },
            { StepStatus.Failed, fail },
        };
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

    public Dictionary<StepStatus, int> CountTables(string? flowId = null)
    {
        if (Transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        var result = helper.CountTables(flowId, TableNameReady, TableNameDone, TableNameFail, Transaction!);
        return result;
    }


    // TODO lav test case på at man aktiverer et eksekverende step - som dermed er skrive-låst - skal nok anvende en 2s timeout
    public int Update(StepStatus target, Step step)
    {
        if (Transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        var name = GetTableName(target);
        return helper.Update(step, name, Transaction!);
    }

    public int Insert(StepStatus target, Step step)
    {
        if (Transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        return helper.InsertStep(target, GetTableName(target), step, Transaction!);
    }

    public int[] Insert(StepStatus target, Step[] steps)
    {
        if (Transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        var result = new List<int>(steps.Length);

        foreach (var x in steps)
        {
            result.Add(helper.InsertStep(target, TableNameReady, x, Transaction!));
        }

        return result.ToArray();
    }

    public int Delete(StepStatus target, int id)
    {
        if (Transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        var name = GetTableName(target);
        return helper.Delete(id, name, Transaction!);
    }

    private string GetTableName(StepStatus target)
    {
        switch (target)
        {
            case StepStatus.Done:
                return TableNameDone;
            case StepStatus.Failed:
                return TableNameFail;
            case StepStatus.Ready:
                return TableNameReady;
            default: throw new NotImplementedException($"{target}");
        }
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
