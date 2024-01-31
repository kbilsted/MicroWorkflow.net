using GreenFeetWorkflow;
using System.Text.Json;


// An example of a 3-step workflow
//
// +----------+      +------------+      +------------+
// |fetch data|  ->  |Process data|  ->  |Email result|
// +----------+      +------------+      +------------+
//
//
// The initial setup may look verbose. This is quite on purpose as we want you to decide how you want to deal with state, logging etc.

// setup 1.
// register the steps to be used by the engine. For the demo we don't use a real IOC container, but you can use any IOC container supporting named dependencies
var iocContainer = new DemoIocContainer().RegisterNamedSteps(typeof(FetchData).Assembly);

// setup 2.
// register the persistence mechanism. For the demo we use a crude in-memory storage
var persister = new DemoInMemoryPersister();
iocContainer.Entries.Add(typeof(IStepPersister).FullName!, persister);

// setup 3.
// register the logger and the loglevel. For the demo we simply log to the console. 
// Notice loglevels can be re-defined at run-time so you can turn on fine-grained logs for a limited time
IWorkflowLogger logger = new ConsoleStepLogger();
logger.Configuration.TraceLoggingEnabledUntil = DateTime.Now.AddMinutes(15);
logger.Configuration.DebugLoggingEnabledUntil = DateTime.MaxValue;
logger.Configuration.InfoLoggingEnabledUntil = DateTime.MaxValue;
logger.Configuration.ErrorLoggingEnabledUntil = DateTime.MaxValue;

// setup 4.
// Define the format of workflow steps' state. Here we use .Net's JSON serializer
var formatter = new DotNetStepStateFormatterJson(logger);
var engine = new WorkflowEngine(logger, iocContainer, formatter);

// setup 5.
// Add a step to be executed - when executing succesfully, it will spawn new steps during processing
// you can add new steps at any time during run-time
engine.Data.AddStep(new Step(FetchData.Name, 0));

// setup 6.
// Configure the engine. 
// For the demo we tell the engine to stop when there is no immediate pending work, so the program terminates quickly. For production you want the engine to run forever
// The number of workers is dynamically adjusted during execution to fit the pending work. So when there is something to do there is much paralellism, and when there is nothing to do the workers gets killed.
var cfg = new WorkflowConfiguration(
    new WorkerConfig
    {
        StopWhenNoImmediateWork = true,
        MinWorkerCount = 1,
        MaxWorkerCount = 8,
    });


// setup 7.
// Start the engine and wait for it to terminate
engine.Start(cfg);

// don't close the window immediately
Console.WriteLine("Press enter to exit");
Console.ReadLine();


// Below is the code for the workflow

[StepName(Name)]
class FetchData : IStepImplementation
{
    public const string Name = "v1/demos/fetch-word-analyze-email/fetch";

    public async Task<ExecutionResult> ExecuteAsync(Step step)
    {
        var count = JsonSerializer.Deserialize<int>(step.State!);

        if (count >= 3)
            return ExecutionResult.Fail(description: "Too many retries");

        var result = await new HttpClient().GetAsync("https://dr.dk");
        if (result.IsSuccessStatusCode)
            return step.Done()
                .With(new Step(AnalyzeWords.Name, await result.Content.ReadAsStringAsync()));

        return step.Rerun(newStateForRerun: count + 1);
    }
}

[StepName(Name)]
class AnalyzeWords : IStepImplementation
{
    public const string Name = "v1/demos/fetch-wordanalyze-email/process";

    public async Task<ExecutionResult> ExecuteAsync(Step step)
    {
        var content = JsonSerializer.Deserialize<string>(step.State!);
        var topWords = content!
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length > 3)
            .GroupBy(x => x)
            .OrderByDescending(x => x.Count())
            .Take(3)
            .Select(x => x.Key);

        return await Task.FromResult(
            step.Done().With(new Step(SendEmail.Name, topWords)));
    }
}

[StepName(Name)]
[StepName("v2/alternative-name")] // step implementation may have multiple names
class SendEmail : IStepImplementation
{
    public const string Name = "v1/demos/fetch-wordanalyzeemail/ship-results";

    public async Task<ExecutionResult> ExecuteAsync(Step step)
    {
        var topWords = JsonSerializer.Deserialize<string[]>(step.State!);
        var words = string.Join(", ", topWords!);

        await new EmailSender()
            .SendEmail(to: "demos@demoland.com", from: "some@one.cool", $"Top 3 words: {words}");

        return step.Done();
    }
}

class EmailSender
{
    public async Task SendEmail(string to, string from, string content)
    {
        await Console.Out.WriteLineAsync($"");
        await Console.Out.WriteLineAsync($"");
        await Console.Out.WriteLineAsync($"");
        await Console.Out.WriteLineAsync($"To: {to}\nFrom: {from}\nContent: {content}");
        await Console.Out.WriteLineAsync($"");
        await Console.Out.WriteLineAsync($"");
        await Console.Out.WriteLineAsync($"");
    }
}