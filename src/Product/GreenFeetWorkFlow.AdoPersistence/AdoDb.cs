using Microsoft.Data.SqlClient;

namespace GreenFeetWorkflow.AdoPersistence;

public class AdoDbStepPersister : IStepPersister
{
    public string TableNameReady { get; set; } = "[dbo].[Steps_Ready]";
    public string TableNameFail { get; set; } = "[dbo].[Steps_Fail]";
    public string TableNameDone { get; set; } = "[dbo].[Steps_Done]";
    object? IStepPersister.Transaction { get; set; } // TODO lav til implicit usage

    private readonly string connectionString;
    SqlConnection? connection = null;
    public SqlTransaction? Transaction = null;

    readonly AdoHelper helper = new();

    public AdoDbStepPersister(string connectionString)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public object CreateTransaction()
    {
        if (Transaction != null)
            throw new InvalidOperationException("Cannot make a new transaction when a transaction is executing");

        connection = new SqlConnection(connectionString);

        connection.Open();

        ((IStepPersister)this).Transaction
            = Transaction
                = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        return Transaction;
    }

    public Dictionary<StepStatus, IEnumerable<Step>> SearchSteps(SearchModel model)
    {
        CreateTransaction();

        List<Step> ready = model.FetchLevel.IncludeReady
            ? helper.SearchSteps(TableNameReady, model, Transaction!)
            : new List<Step>();
        List<Step> done = model.FetchLevel.IncludeDone
            ? helper.SearchSteps(TableNameDone, model, Transaction!)
            : new List<Step>();
        List<Step> fail = model.FetchLevel.IncludeFail
            ? helper.SearchSteps(TableNameFail, model, Transaction!)
            : new List<Step>();

        Commit();

        return new Dictionary<StepStatus, IEnumerable<Step>>()
        {
            { StepStatus.Ready, ready },
            { StepStatus.Done, done },
            { StepStatus.Failed, fail },
        };
    }

    public int[] ReExecuteSteps(Dictionary<StepStatus, IEnumerable<Step>> entities)
    {
        if (entities.ContainsKey(StepStatus.Ready) && entities[StepStatus.Ready].Any())
            throw new ArgumentOutOfRangeException("Cannot re-execute ready steps");

        CreateTransaction();

        List<int> ids = new List<int>();

        var now = DateTime.Now;

        foreach (KeyValuePair<StepStatus, IEnumerable<Step>> steps in entities)
        {
            foreach (var step in steps.Value)
            {
                int id = helper.InsertStep(StepStatus.Ready, TableNameReady, new Step()
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

        Commit();

        return ids.ToArray();
    }


    public Step? GetStep()
    {
        if (Transaction == null)
            CreateTransaction();

        return helper.GetStep(TableNameReady, Transaction!);
    }

    public Step? GetStep(int id)
    {
        if (Transaction == null)
            CreateTransaction();

        return helper.GetStep(TableNameReady, id, Transaction!);
    }

    public void Commit(StepStatus status, Step executedStep, List<Step>? newSteps)
    {
        if (Transaction == null)
            throw new Exception("Cannot commit non-existing transaction. Remember to create a transaction before commit");

        if (newSteps != null)
        {
            foreach (var step in newSteps)
            {
                helper.InsertStep(StepStatus.Ready, TableNameReady, step, Transaction!);
            }
        }

        switch (status)
        {
            case StepStatus.Done:
                helper.DeleteReady(executedStep, TableNameReady, Transaction!);
                helper.InsertStep(StepStatus.Done, TableNameDone, executedStep, Transaction);
                break;

            case StepStatus.Failed:
                helper.DeleteReady(executedStep, TableNameReady, Transaction!);
                helper.InsertStep(StepStatus.Failed, TableNameFail, executedStep, Transaction);
                break;

            case StepStatus.Ready:
                helper.UpdateReady(executedStep, TableNameReady, Transaction!);
                break;
        }

        Commit();
    }


    public void Commit()
    {
        if (Transaction == null)
            throw new InvalidOperationException("There is no transaction");
        Transaction.Commit();
        Transaction.Dispose();
        Transaction = null;

        if (connection == null)
            throw new InvalidOperationException("There is no open connection");

        connection.Dispose();
        connection = null;
    }

    public void RollBack()
    {
        if (Transaction == null)
            throw new InvalidOperationException("There is no transaction");
        Transaction.Rollback();
        Transaction.Dispose();
        Transaction = null;

        if (connection == null)
            throw new InvalidOperationException("There is no open connection");
        connection.Dispose();
        connection = null;
    }

    public int[] AddSteps(params Step[] steps) => AddSteps(null, steps);

    // TODO test med/uden transaction
    public int[] AddSteps(object? transaction = null, params Step[] steps)
    {
        if (!steps.Any())
            return new int[0];

        var result = new List<int>();
        if (transaction == null)
        {
            CreateTransaction();
            foreach (var x in steps)
            {
                result.Add(helper.InsertStep(StepStatus.Ready, TableNameReady, x, Transaction!));
            }
            Commit();
        }
        else
        {
            var typedTrans = (SqlTransaction)transaction;
            foreach (var x in steps)
            {
                result.Add(helper.InsertStep(StepStatus.Ready, TableNameReady, x, typedTrans!));
            }
        }

        return result.ToArray();
    }

    public Dictionary<StepStatus, int> CountTables(string flowId)
    {
        if (Transaction == null)
            CreateTransaction();

        var result = helper.CountTables(flowId, TableNameReady, TableNameDone, TableNameFail, Transaction!);
        Commit();
        return result;
    }

    public void Dispose()
    {
        Transaction?.Dispose();
        Transaction = null;
        connection?.Dispose();
        connection = null;

        GC.SuppressFinalize(this);
    }

    // TODO fejler
    public int ActivateStep(string searchKey, string? stepName, string? arguments)
    {
        if (Transaction == null)
            CreateTransaction();

        int rows = helper.UpdateReadyBySearchKeyAndName(TableNameReady, stepName, searchKey, Transaction!);
        Commit();
        return rows;
    }
}
