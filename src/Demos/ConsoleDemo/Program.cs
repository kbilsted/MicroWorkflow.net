using Autofac;
using MicroWorkflow;
using MicroWorkflow.DemoImplementation;
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
            .Split(' ')
            .Where(x => x.Length > 3)
            .GroupBy(x => x)
            .OrderByDescending(x => x.Count())
            .Take(3)
            .Select(x => x.Key);

        ExecutionResult done = step.Done().With(new Step(SendEmail.Name, topWords));
        return await Task.FromResult(done);
    }
}

[StepName(Name)]
class SendEmail(EmailSender sender) : IStepImplementation
{
    public const string Name = "v1/demos/fetch-wordanalyzeemail/ship-results";

    public async Task<ExecutionResult> ExecuteAsync(Step step)
    {
        var topWords = JsonSerializer.Deserialize<string[]>(step.State!);
        var words = string.Join(", ", topWords!);

        await sender.SendEmail(to: "demos@demoland.com", from: "some@one.cool", $"Top 3 words: {words}");

        return step.Done();
    }
}

class Program
{
    public static void Main()
    {
        var builder = new ContainerBuilder();
        var cfg = new WorkflowConfiguration(new WorkerConfig { StopWhenNoImmediateWork = true });
        builder.UseMicroWorkflow(cfg);
        builder.RegisterType<EmailSender>().AsSelf();
        builder.RegisterType<ConsoleStepLogger>().As<IWorkflowLogger>();

        var container = builder.Build();

        // Add a step to be executed - you can add new steps at any time during run-time
        var engine = container.Resolve<WorkflowEngine>();
        engine.Data.AddStep(new Step(FetchData.Name, 0));

        // Start the engine and wait for it to terminate
        engine.Start();

        PrintResult();
    }

    private static void PrintResult()
    {
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