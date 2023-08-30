using System.Text;

namespace GreenFeetWorkflow.AdoPersistence;

/// <summary>
/// Dynamic SQL builder
/// </summary>
public class Sql
{
    Sql AddWhere(string sql)
    {
        if (AnyWhere)
        {
            sb.Append(" AND ");
        }
        else
        {
            sb.Append("\nWHERE ");
            AnyWhere = true;
        }

        return Add(sql);
    }


    Sql AddSet(string sql)
    {
        if (AnySet)
        {
            sb.Append(" ,");
        }
        else
        {
            sb.Append("\nSET ");
            AnySet = true;
        }

        return Add(sql);
    }



    public readonly StringBuilder sb = new();
    public bool AnyWhere = false;
    public bool AnySet = false;

    public Sql(string sql) => sb.Append(sql);

    public Sql Set(string sql) => AddSet(sql);
    public Sql Set(object? value, string sql) => value == null ? this : AddSet(sql);

    public Sql Where(string sql) => AddWhere(sql);
    public Sql Where(bool value, string sql) => value ? AddWhere(sql) : this;
    public Sql Where(bool value, string sql, string sqlElse) => value ? AddWhere(sql) : AddWhere(sqlElse);
    public Sql Where(bool? value, string sql, string sqlElse) => value == null ? this : Where(value.Value, sql, sqlElse);

    public Sql WhereIfAny<T>(IEnumerable<T>? value, string sql) => value != null && value.Any() ? AddWhere(sql) : this;

    public Sql Where(object? value, string sql) => value == null ? this : AddWhere(sql);
    public Sql Where<T>(T? value, string sql) where T : struct => value.HasValue ? AddWhere(sql) : this;

    public Sql Where(Action<Sql> code)
    {
        var orSqlBlock = (new Sql("AND ("));
        code(orSqlBlock);
        orSqlBlock.AddWhere(")");

        return AddWhere(orSqlBlock.Build());
    }

    public Sql Add(string sql)
    {
        sb.AppendLine(sql);
        return this;
    }

    public string Build() => sb.ToString();

    public static implicit operator string(Sql sql) => sql.Build();
}