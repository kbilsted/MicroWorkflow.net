using GreenFeetWorkflow;
using System.Text.Json;


// An example of a 3-step workflow
//
// +----------+      +------------+      +------------+
// |fetch data|  ->  |Process data|  ->  |Email result|
// +----------+      +------------+      +------------+
//
//


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

class Program
{
    public static void Main(string[] args)
    {
        // To start and run the engine

        // register the steps to be used by the engine. For the demo we don't use a real IOC container, but you can use any IOC container supporting named dependencies
        var iocContainer = new DemoIocContainer().RegisterNamedSteps(typeof(FetchData).Assembly);

        // register the persistence mechanism. For the demo we use a crude in-memory storage
        iocContainer.Entries.Add(typeof(IStepPersister).FullName!, new DemoInMemoryPersister());

        // register the logger and the loglevel. For the demo we simply log to the console. 
        // Notice loglevels can be re-defined at run-time so you can turn on fine-grained logs for a limited time
        IWorkflowLogger logger = new ConsoleStepLogger();

        // Define the format of workflow steps' state. Here we use .Net's JSON serializer
        var formatter = new DotNetStepStateFormatterJson(logger);
        var engine = new WorkflowEngine(logger, iocContainer, formatter);

        // Add a step to be executed - when executing succesfully, it will spawn new steps during processing
        // you can add new steps at any time during run-time
        engine.Data.AddStep(new Step(FetchData.Name, 0));

        // Configure the engine. 
        // For the demo we tell the engine to stop when there is no immediate pending work, so the program terminates quickly. For production you want the engine to run forever
        // The number of workers is dynamically adjusted during execution to fit the pending work.
        // This ensures we do not constantly bombard the persistence storage with requests while at the same time quickly respond to new work
        var cfg = new WorkflowConfiguration(
            new WorkerConfig
            {
                StopWhenNoImmediateWork = true,
                MinWorkerCount = 1,
                MaxWorkerCount = 8,
            });

        // Start the engine and wait for it to terminate
        engine.Start(cfg);

        // don't close the window immediately
        Console.WriteLine(PrintTable("Ready", DemoInMemoryPersister.ReadySteps));
        Console.WriteLine(PrintTable("Failed", DemoInMemoryPersister.FailedSteps));
        Console.WriteLine(PrintTable("Done", DemoInMemoryPersister.DoneSteps));
        Console.WriteLine("Press enter to exit");
        Console.ReadLine();
    }

    static string PrintTable(string name, Dictionary<int, Step> table) => $"{name}: total:{table.Count}\n{string.Join("\n", table.Select(Print))}";
    static string Print(KeyValuePair<int, Step> x) => $"id:{x.Key} {x.Value.Name,-46}  time:{x.Value.ExecutionStartTime}  duration:{x.Value.ExecutionDurationMillis} ms";
}


/*
 
Example output log



31-01-2024 20:43:17 [INFO  ] WorkflowEngine: starting engine

31-01-2024 20:43:17 [INFO  ] Worker: Execution result: Done.
- correlationId:
- executionDuration: 474
- flowId: e4c8a29b-2f44-4b05-90f9-32ffc00c9b7d
- newSteps: 1
- stepId: 1
- stepName: v1/demos/fetch-word-analyze-email/fetch
- workerId: PUTTE/pid/9688/9843

31-01-2024 20:43:17 [INFO  ] Worker: Execution result: Done.
- correlationId:
- executionDuration: 5
- flowId: e4c8a29b-2f44-4b05-90f9-32ffc00c9b7d
- newSteps: 1
- stepId: 2
- stepName: v1/demos/fetch-wordanalyze-email/process
- workerId: PUTTE/pid/9688/9843

Sending email...     To: demos@demoland.com\nFrom: some@one.cool\nContent: Top 3 words: class="dre-label-text, dre-label-text--xxs-x-small"><span, class="dre-label-text__text"><span
31-01-2024 20:43:17 [INFO  ] Worker: Execution result: Done.
- correlationId:
- executionDuration: 0
- flowId: e4c8a29b-2f44-4b05-90f9-32ffc00c9b7d
- newSteps: 0
- stepId: 3
- stepName: v1/demos/fetch-wordanalyzeemail/ship-results
- workerId: PUTTE/pid/9688/9843

Ready :0

Failed:0

Done  :3
id:1 v1/demos/fetch-word-analyze-email/fetch         time:31-01-2024 20:43:17  duration:474 ms
id:2 v1/demos/fetch-wordanalyze-email/process        time:31-01-2024 20:43:17  duration:5 ms
id:3 v1/demos/fetch-wordanalyzeemail/ship-results    time:31-01-2024 20:43:17  duration:0 ms
Press enter to exit

*/



class EmailSender
{
    public async Task SendEmail(string to, string from, string content)
    {
        Console.WriteLine(@$"Sending email...     To: {to}\nFrom: {from}\nContent: {content}");
        await Task.CompletedTask;
    }
}