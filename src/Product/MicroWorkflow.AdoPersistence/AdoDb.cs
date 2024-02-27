using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.RegularExpressions;

namespace MicroWorkflow;

public class SqlServerPersister : IWorkflowStepPersister
{
    public string TableNameReady { get; set; } = "[dbo].[Steps_Ready]";
    public string TableNameFail { get; set; } = "[dbo].[Steps_Fail]";
    public string TableNameDone { get; set; } = "[dbo].[Steps_Done]";

    private readonly string connectionString;
    private readonly IWorkflowLogger logger;
    SqlConnection? connection = null;

    SqlTransaction? transaction;

    readonly Guid persisterId = Guid.NewGuid();

    readonly AdoHelper helper;

    public SqlServerPersister(string connectionString, IWorkflowLogger logger)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        this.logger = logger;
        helper = new(logger);
    }

    public void SetTransaction(object transaction)
    {
        this.transaction = (SqlTransaction?)transaction;
        this.connection = this.transaction!.Connection;
    }

    public object CreateTransaction()
    {
        if (transaction != null)
            throw new InvalidOperationException("Cannot make a new transaction when a transaction is executing");

        connection = new SqlConnection(connectionString);

        connection.Open();

        transaction = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);

        return transaction;
    }

    public string GetConnectionInfoForLogging() => "SqlServer persister: " + Regex.Replace(connectionString, "Password=[^;]*", "");

    public T InTransaction<T>(Func<T> code, object? outsideTransaction = null)
    {
        try
        {
            if (outsideTransaction == null)
                CreateTransaction();
            else
                SetTransaction(outsideTransaction);
        }
        catch (Exception ex)
        {
            if (logger.TraceLoggingEnabled)
                logger.LogTrace($"{nameof(SqlServerPersister)}: Error in 'InTransaction()' while creating/setting transaction", ex, new Dictionary<string, object?>() {
                { "PersisterId", persisterId } ,
                        //{ "Stack", new StackTrace().ToString()} ,
                });

            throw;
        }

        try
        {
            T result = code();

            // only commit if we created the tx
            if (outsideTransaction == null)
                Commit();
            
            return result;
        }
        catch (Exception e)
        {
            if (logger.TraceLoggingEnabled)
                logger.LogTrace($"{nameof(SqlServerPersister)}: Error executing code in 'InTransaction()'", e, new Dictionary<string, object?>() {
                { "PersisterId", persisterId } ,
                        //{ "Stack", new StackTrace().ToString()} ,
                });

            // only rollback if we created the tx
            if (outsideTransaction == null)
                RollBack();

            throw;
        }
    }

    public List<Step> SearchSteps(SearchModel criteria, StepStatus target)
    {
        if (transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        var steps = helper.SearchSteps(GetTableName(target), criteria, new FetchLevels(), transaction!);
        return steps;
    }

    public Dictionary<StepStatus, List<Step>> SearchSteps(SearchModel criteria, FetchLevels fetchLevel)
    {
        if (transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        List<Step> ready = fetchLevel.Ready
            ? helper.SearchSteps(TableNameReady, criteria, fetchLevel, transaction!)
            : new List<Step>();
        List<Step> done = fetchLevel.Done
            ? helper.SearchSteps(TableNameDone, criteria, fetchLevel, transaction!)
            : new List<Step>();
        List<Step> fail = fetchLevel.Fail
            ? helper.SearchSteps(TableNameFail, criteria, fetchLevel, transaction!)
            : new List<Step>();

        return new Dictionary<StepStatus, List<Step>>()
        {
            { StepStatus.Ready, ready },
            { StepStatus.Done, done },
            { StepStatus.Failed, fail },
        };
    }

    public Step? GetAndLockReadyStep()
    {
        if (transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        return helper.GetAndLockReadyStep(TableNameReady, transaction!);
    }

    public Step? GetStep(int id)
    {
        if (transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        return helper.GetStep(TableNameReady, id, transaction!);
    }

    public void Commit()
    {
        if (logger.TraceLoggingEnabled)
            logger.LogTrace($"{nameof(SqlServerPersister)}: Commit Transaction",
                null,
                new Dictionary<string, object?>() {
                { "PersisterId", persisterId } ,
                //{ "Stack", new StackTrace().ToString()} ,
            });

        if (transaction == null)
            throw new InvalidOperationException("There is no transaction to commit");
        transaction.Commit();
        transaction.Dispose();
        transaction = null;

        if (connection == null)
            throw new InvalidOperationException("There is no open connection to close");
        connection.Dispose();
        connection = null;
    }

    public void RollBack()
    {
        if (logger.TraceLoggingEnabled)
            logger.LogTrace($"{nameof(SqlServerPersister)}: Rollback Transaction",
                null,
                new Dictionary<string, object?>() { { "PersisterId", persisterId } });
        try
        {
            if (transaction == null)
                throw new InvalidOperationException("There is no transaction to rollback");
            transaction.Rollback();
            transaction.Dispose();
            transaction = null;
        }
        finally
        {
            if (connection == null)
                throw new InvalidOperationException("There is no open connection");
            connection.Dispose();
            connection = null;
        }
    }

    public Dictionary<StepStatus, int> CountTables(string? flowId = null)
    {
        if (transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        var result = helper.CountTables(flowId, TableNameReady, TableNameDone, TableNameFail, transaction!);
        return result;
    }


    public int Update(StepStatus target, Step step)
    {
        if (transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        var name = GetTableName(target);
        return helper.Update(step, name, transaction!);
    }

    public int Insert(StepStatus target, Step step)
    {
        if (transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        return helper.InsertStep(target, GetTableName(target), step, transaction!);
    }

    public int[] Insert(StepStatus target, Step[] steps)
    {
        if (transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        var result = new List<int>(steps.Length);

        foreach (var x in steps)
        {
            result.Add(helper.InsertStep(target, TableNameReady, x, transaction!));
        }

        return result.ToArray();
    }

    public async Task InsertBulkAsync(StepStatus target, IEnumerable<Step> steps)
    {
        if (transaction != null)
            throw new ArgumentException("Inside transaction, does not work in bulk");

        await using SqlConnection conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        var table = new DataTable();

        using SqlDataAdapter adapter = new SqlDataAdapter($"select top 0 * from {TableNameReady}", conn);
        adapter.FillSchema(table, SchemaType.Source);

        using SqlBulkCopy bulk = new SqlBulkCopy(conn);
        bulk.DestinationTableName = table.TableName;

        using ObjectDataReader<Step> objectDataReader = new ObjectDataReader<Step>(steps);
        await bulk.WriteToServerAsync(objectDataReader);
    }

    public int Delete(StepStatus target, int id)
    {
        if (transaction == null)
            throw new ArgumentException("Missing transaction. Remember to create a transaction before calling");

        var name = GetTableName(target);
        return helper.Delete(id, name, transaction!);
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
        if (transaction != null)
        {
            if (logger.TraceLoggingEnabled)
                logger.LogTrace($"{nameof(SqlServerPersister)}: Dispose() unclosed transaction", null, new Dictionary<string, object?>() { { "persisterId", persisterId } });

            transaction.Dispose();
            transaction = null;
        }

        if (connection != null)
        {
            if (logger.TraceLoggingEnabled)
                logger.LogTrace($"{nameof(SqlServerPersister)}: Dispose() unclosed connection", null, new Dictionary<string, object?>() { { "persisterId", persisterId } });

            connection.Dispose();
            connection = null;
        }

        GC.SuppressFinalize(this);
    }
}