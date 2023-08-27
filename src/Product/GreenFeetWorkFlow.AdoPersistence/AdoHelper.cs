using Microsoft.Data.SqlClient;

namespace GreenFeetWorkflow.AdoPersistence;

#pragma warning disable CA1822 // Mark members as static

public class AdoHelper
{
    public List<Step> SearchSteps(string table, SearchModel model, SqlTransaction tx)
    {
        var sql = new Sql($"SELECT * FROM {table} WITH (NOLOCK)")
            .Where(model.Id, "[Id] = @Id")
            .Where(model.CorrelationId, "[CorrelationId] = @CorrelationId")
            .Where(model.Name, "[Name] = @Name")
            .Where(model.SearchKey, "[SearchKey] LIKE @SearchKey")
            .Where(model.FlowId, "[FlowId] = @FlowId");

        using var cmd = new SqlCommand(sql, tx.Connection, tx);
        if (cmd.CommandText.Contains("@Id"))
            cmd.Parameters.Add(new SqlParameter("Id", model.Id));
        if (cmd.CommandText.Contains("@CorrelationId"))
            cmd.Parameters.Add(new SqlParameter("CorrelationId", model.CorrelationId));
        if (cmd.CommandText.Contains("@Name"))
            cmd.Parameters.Add(new SqlParameter("Name", model.Name));
        if (cmd.CommandText.Contains("@SearchKey"))
            cmd.Parameters.Add(new SqlParameter("SearchKey", model.SearchKey));
        if (cmd.CommandText.Contains("@FlowId"))
            cmd.Parameters.Add(new SqlParameter("FlowId", model.FlowId));

        using SqlDataReader reader = cmd.ExecuteReader();

        var result = new List<Step>();
        while (true)
        {
            var s = ReadStep(reader);
            if (s == null)
                break;
            result.Add(s);
        }

        return result;
    }

    public Step? GetStep(string tableName, int id, SqlTransaction tx)
    {
        var sql = @$"SELECT TOP 1 * 
         FROM {tableName} WITH(ROWLOCK, UPDLOCK) 
         WHERE [Id] = @id";
        var cmd = new SqlCommand(sql, tx.Connection, tx);
        cmd.Parameters.Add(new SqlParameter("id", id));

        return ReadStepRow(cmd);
    }

    // TODO mangler activationData
    public int UpdateStep(string tableName, int id, DateTime scheduleTime, string? activationData, SqlTransaction tx)
    {
        Sql sql = new Sql(@$"UPDATE {tableName} 
        SET [ScheduleTime] = @scheduleTime")
        .Where("[Id] = @Id");

        using var cmd = new SqlCommand(sql, tx.Connection, tx);
        cmd.Parameters.Add(new SqlParameter("scheduleTime", scheduleTime));
        cmd.Parameters.Add(new SqlParameter("Id", id));

        return cmd.ExecuteNonQuery();
    }

    public Step? GetAndLockReadyStep(string tableName, SqlTransaction tx)
    {
        var sql = @$"SELECT TOP 1 * 
         FROM {tableName} WITH(ROWLOCK, UPDLOCK, READPAST) 
         WHERE [ScheduleTime] <= @earliest";
        using var cmd = new SqlCommand(sql, tx.Connection, tx);
        cmd.Parameters.Add(new SqlParameter("earliest", DateTime.Now));

        return ReadStepRow(cmd);
    }

    private Step? ReadStepRow(SqlCommand cmd)
    {
        using SqlDataReader reader = cmd.ExecuteReader();
        return ReadStep(reader);
    }

    private Step? ReadStep(SqlDataReader reader)
    {
        if (!reader.Read())
            return null;

#pragma warning disable IDE0017 // Simplify object initialization
        var step = new Step();
        step.Id = reader.GetInt32(reader.GetOrdinal("Id"));
        step.Name = GetString(reader, "Name");
        step.Singleton = reader.GetBoolean(reader.GetOrdinal("Singleton"));
        step.FlowId = reader.GetString(reader.GetOrdinal("FlowId"));
        step.SearchKey = GetNullString(reader, "SearchKey");
        step.ExecutionCount = reader.GetInt32(reader.GetOrdinal("ExecutionCount"));
        step.ScheduleTime = reader.GetDateTime(reader.GetOrdinal("ScheduleTime"));
        step.PersistedState = GetNullString(reader, "PersistedState");
        step.PersistedStateFormat = GetNullString(reader, "PersistedStateFormat");
        step.ExecutionDurationMillis = GetNullLong(reader, "ExecutionDurationMillis");
        step.ExecutionStartTime = GetNullDatetime(reader, "ExecutionStartTime");
        step.CreatedTime = reader.GetDateTime(reader.GetOrdinal("CreatedTime"));
        step.CreatedByStepId = GetNullInt(reader, "CreatedByStepId");
        step.CorrelationId = GetNullString(reader, "CorrelationId");
        step.Description = GetNullString(reader, "Description");
#pragma warning restore IDE0017 // Simplify object initialization

        return step;
    }


    DateTime? GetNullDatetime(SqlDataReader reader, string column)
    {
        int idx = reader.GetOrdinal(column);
        if (reader.IsDBNull(idx))
            return null;
        return reader.GetDateTime(idx);
    }

    string GetString(SqlDataReader reader, string column)
    {
        int idx = reader.GetOrdinal(column);
        return reader.GetString(idx);
    }

    string? GetNullString(SqlDataReader reader, string column)
    {
        int idx = reader.GetOrdinal(column);
        if (reader.IsDBNull(idx))
            return null;
        return reader.GetString(idx);
    }

    int? GetNullInt(SqlDataReader reader, string column)
    {
        int idx = reader.GetOrdinal(column);
        if (reader.IsDBNull(idx))
            return null;
        return reader.GetInt32(idx);
    }

    long? GetNullLong(SqlDataReader reader, string column)
    {
        int idx = reader.GetOrdinal(column);
        if (reader.IsDBNull(idx))
            return null;
        return reader.GetInt64(idx);
    }


    public int UpdateReady(Step step, string ReadyTableName, SqlTransaction tx)
    {
        var sql = @$"UPDATE {ReadyTableName}
SET
[SearchKey] = @SearchKey
,[PersistedState] = @PersistedState
,[PersistedStateFormat] = @PersistedStateFormat
,[Description] = @Description
,[ExecutionCount] = @ExecutionCount
,[ScheduleTime] = @ScheduleTime
,[ExecutionStartTime] = @ExecutionStartTime
,[ExecutionDurationMillis] = @ExecutionDurationMillis
,[ExecutedBy] = @ExecutedBy
,[CorrelationId] = @CorrelationId
 WHERE Id = @Id";

        var cmd = new SqlCommand(sql, tx.Connection, tx);
        cmd.Parameters.Add(new SqlParameter("@Id", step.Id));
        cmd.Parameters.Add(new SqlParameter("@SearchKey", step.SearchKey ?? (object)DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@PersistedState", step.PersistedState ?? (object)DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@PersistedStateFormat", step.PersistedStateFormat ?? (object)DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@Description", step.Description ?? (object)DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@ExecutionCount", step.ExecutionCount));
        cmd.Parameters.Add(new SqlParameter("@ScheduleTime", step.ScheduleTime));
        cmd.Parameters.Add(new SqlParameter("@ExecutionStartTime", step.ExecutionStartTime ?? (object)DBNull.Value)); // null e.g. when retry due to missing step executor
        cmd.Parameters.Add(new SqlParameter("@ExecutionDurationMillis", step.ExecutionDurationMillis ?? (object)DBNull.Value)); // null e.g. when retry due to missing step executor
        cmd.Parameters.Add(new SqlParameter("@ExecutedBy", step.ExecutedBy ?? (object)DBNull.Value)); // null e.g. when retry due to missing step executor
        cmd.Parameters.Add(new SqlParameter("@CorrelationId", step.CorrelationId ?? (object)DBNull.Value));

        return cmd.ExecuteNonQuery();
    }


    public int DeleteStep(int id, string tableName, SqlTransaction tx)
    {
        var sql = @$"DELETE FROM {tableName} WHERE Id = @Id";
        using var cmd = new SqlCommand(sql, tx.Connection, tx);
        cmd.Parameters.Add(new SqlParameter("Id", id));

        return cmd.ExecuteNonQuery();
    }


    public int DeleteReady(Step step, string ReadyTableName, SqlTransaction tx)
    {
        var sql = @$"DELETE FROM {ReadyTableName} WHERE Id = @Id";
        using var cmd = new SqlCommand(sql, tx.Connection, tx);
        cmd.Parameters.Add(new SqlParameter("Id", step.Id));

        return cmd.ExecuteNonQuery();
    }

    // TODO NICE optimize to enable multiple inserts in one call
    public int InsertStep(StepStatus target, string tablename, Step step, SqlTransaction tx)
    {
        if (target == StepStatus.Ready)
            return InsertReady();
        else
            return InsertDoneFail();

        static void AddParamaters(Step step, SqlCommand cmd)
        {
            cmd.Parameters.AddWithValue("@Name", step.Name);
            cmd.Parameters.AddWithValue("@Singleton", step.Singleton);
            cmd.Parameters.AddWithValue("@FlowId", step.FlowId);
            cmd.Parameters.AddWithValue("@SearchKey", step.SearchKey ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Description", step.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ExecutionCount", step.ExecutionCount);
            cmd.Parameters.AddWithValue("@ScheduleTime", step.ScheduleTime);
            cmd.Parameters.AddWithValue("@PersistedState", step.PersistedState ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PersistedStateFormat", step.PersistedStateFormat ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ExecutionStartTime", step.ExecutionStartTime ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ExecutionDurationMillis", step.ExecutionDurationMillis ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ExecutedBy", step.ExecutedBy ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CorrelationId", step.CorrelationId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedTime", step.CreatedTime);
            cmd.Parameters.AddWithValue("@CreatedByStepId", step.CreatedByStepId ?? (object)DBNull.Value);
        }

        int InsertReady()
        {
            string sql =
    @$"set nocount on INSERT INTO {tablename}
           ([Name]
           ,[Singleton]
           ,[FlowId]
           ,[SearchKey]
           ,[Description]
           ,[ExecutionCount]
           ,[ScheduleTime]
           ,[PersistedState]
           ,[PersistedStateFormat]
           ,[CorrelationId]
           ,[CreatedTime]
           ,[CreatedByStepId])
     VALUES
           (@Name
           ,@Singleton
           ,@FlowId
           ,@SearchKey
           ,@Description
           ,@ExecutionCount
           ,@ScheduleTime
           ,@PersistedState
           ,@PersistedStateFormat
           ,@CorrelationId
           ,@CreatedTime
           ,@CreatedByStepId) 
 select cast(scope_identity() as int)";

            using SqlCommand cmd = new(sql, tx.Connection, tx);
            AddParamaters(step, cmd);

            using var reader = cmd.ExecuteReader();
            reader.Read();
            return reader.GetInt32(0);
        }

        int InsertDoneFail()
        {
            string sql = @$"set nocount on INSERT INTO {tablename}
           ([Id]
           ,[Name]
           ,[Singleton]
           ,[FlowId]
           ,[SearchKey]
           ,[Description]
           ,[ExecutionCount]
           ,[ScheduleTime]
           ,[PersistedState]
           ,[PersistedStateFormat]
           ,[ExecutionStartTime]
           ,[ExecutionDurationMillis]
           ,[ExecutedBy]
           ,[CorrelationId]
           ,[CreatedTime]
           ,[CreatedByStepId])
     VALUES
           (@Id
           ,@Name
           ,@Singleton
           ,@FlowId
           ,@SearchKey
           ,@Description
           ,@ExecutionCount
           ,@ScheduleTime
           ,@PersistedState
           ,@PersistedStateFormat
           ,@ExecutionStartTime
           ,@ExecutionDurationMillis
           ,@ExecutedBy
           ,@CorrelationId
           ,@CreatedTime
           ,@CreatedByStepId) ";

            using SqlCommand cmd = new(sql, tx.Connection, tx);
            cmd.Parameters.AddWithValue("@Id", step.Id);
            AddParamaters(step, cmd);

            cmd.ExecuteNonQuery();
            return step.Id;
        }
    }

    internal Dictionary<StepStatus, int> CountTables(string? flowId, string tableNameReady, string tableNameDone, string tableNameFail, SqlTransaction tx)
    {
        int Count(string name)
        {
            string sql = new Sql(@$"SELECT COUNT(*)
                FROM {name} WITH (NOLOCK)")
                .Where(flowId, "[FlowId] = @FlowId");

            var cmd = new SqlCommand(sql, tx.Connection, tx);
            if (cmd.CommandText.Contains("FlowId"))
                cmd.Parameters.AddWithValue("@FlowId", flowId);

            using var reader = cmd.ExecuteReader();
            reader.Read();
            return reader.GetInt32(0);
        }

        var result = new Dictionary<StepStatus, int>() {
            { StepStatus.Ready, Count(tableNameReady)},
            { StepStatus.Done, Count(tableNameDone)},
            { StepStatus.Failed, Count(tableNameFail)},
        };

        return result;
    }
}
#pragma warning restore CA1822 // Mark members as static
