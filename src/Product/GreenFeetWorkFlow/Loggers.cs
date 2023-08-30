using System.Diagnostics;
using System.Text;

namespace GreenFeetWorkflow;


public abstract class GenericStepLogger : IWorkflowLogger
{
    bool IWorkflowLogger.TraceLoggingEnabled { get; set; } = false;
    bool IWorkflowLogger.DebugLoggingEnabled { get; set; } = true;
    bool IWorkflowLogger.InfoLoggingEnabled { get; set; } = true;
    bool IWorkflowLogger.ErrorLoggingEnabled { get; set; } = true;

    protected Action<string, Exception?, string?, Dictionary<string, object?>?>? Code;
    private IWorkflowLogger? nestedLogger;

    protected GenericStepLogger(Action<string, Exception?, string?, Dictionary<string, object?>?>? code)
    {
        Code = code;
    }

    public IWorkflowLogger AddNestedLogger(IWorkflowLogger logger)
    {
        nestedLogger = logger;
        return this;
    }

    public static string CreateMessage(string severity, Exception? e, string? msg, Dictionary<string, object?>? arguments)
    {
        msg ??= "";

        var sb = new StringBuilder();

        if (e == null)
            sb.AppendLine(msg);
        else
            sb.AppendLine($"EXCEPTION: {msg}. {e.Message}");

        if (arguments != null)
            foreach (var key in arguments.Keys.OrderBy(x => x))
                sb.AppendLine($"- {key}: {arguments[key]}");

        if (e != null)
            sb.AppendLine("- stacktrace: " + e.StackTrace);

        return sb.ToString();
    }

    public void LogTrace(string? msg, Exception? exception, Dictionary<string, object?>? arguments)
    {
        Code!("TRACE ", exception, msg, arguments);
        nestedLogger?.LogTrace(msg, exception, arguments);
    }

    public void LogDebug(string? msg, Exception? exception, Dictionary<string, object?>? arguments)
    {
        Code!("DEBUG ", exception, msg, arguments);
        nestedLogger?.LogDebug(msg, exception, arguments);
    }

    public void LogInfo(string? msg, Exception? exception, Dictionary<string, object?>? arguments)
    {
        Code!("INFO  ", exception, msg, arguments);
        nestedLogger?.LogInfo(msg, exception, arguments);
    }

    public void LogError(string? msg, Exception? exception, Dictionary<string, object?>? arguments)
    {
        Code!("ERROR", exception, msg, arguments);
        nestedLogger?.LogError(msg, exception, arguments);
    }
}

/// <summary>
/// Writes to the console in colors. Less useful in visual studio as the output window
/// does not show console colors. Consider using <see cref="DiagnosticsStepLogger"/>
/// </summary>
public class ConsoleStepLogger : GenericStepLogger
{
    private static readonly object Lock = new object();

    public ConsoleStepLogger() : base(Print)
    {
    }

    static void Print(string severity, Exception? e, string? msg, Dictionary<string, object?>? arguments)
    {
        var message = CreateMessage(severity, e, msg, arguments);

        lock (Lock)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"{DateTime.Now} ");

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write($"[{severity}] ");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"{message}");
        }
    }
}


/// <summary>
/// Writes to the debug, useful in visual studio as is shown in the output window.
/// </summary>
public class DiagnosticsStepLogger : GenericStepLogger
{
    public DiagnosticsStepLogger() : base(Print)
    {
    }

    static void Print(string severity, Exception? e, string? msg, Dictionary<string, object?>? arguments)
    {
        Debug.WriteLine($"{DateTime.Now} [{severity}] {CreateMessage(severity, e, msg, arguments)}");
    }
}


/// <summary>
/// Collect log messages in a collection for testing purposes
/// </summary>
public class CollectingLoggerForUnittest : GenericStepLogger
{
    protected readonly object Lock = new();
    public List<LogLine> Logs = new();

    public CollectingLoggerForUnittest() : base(null)
    {
        Code = Store;
    }

    void Store(string severity, Exception? e, string? msg, Dictionary<string, object?>? arguments)
    {
        string message = CreateMessage(severity, e, msg, arguments);
        var log = new LogLine(DateTime.Now, severity, e, message, arguments);

        lock (Lock)
        {
            Logs.Add(log);
        }
    }

    public class LogLine
    {
        public DateTime DateTime { get; set; }
        public string Severity { get; set; }
        public string? Message { get; set; }
        public Exception? Exception { get; set; }
        public Dictionary<string, object?>? Extra { get; set; }

        public LogLine(DateTime dateTime, string severity, Exception? e, string? message, Dictionary<string, object?>? extra)
        {
            DateTime = dateTime;
            Severity = severity;
            Message = message;
            Extra = extra;
            Exception = e;
        }
    }
}

