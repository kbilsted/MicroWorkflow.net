using GreenFeetWorkflow;
using System.Text.Json;


// An example of a 3-step workflow
//
// +----------+      +------------+      +------------+
// |fetch data|  ->  |Process data|  ->  |Email result|
// +----------+      +------------+      +------------+
//
//
// step 1. register the steps to be used by the engine.
// For the demo we don't use a real IOC container
var iocContainer = new DemoIocContainer().RegisterNamedSteps(typeof(FetchData).Assembly);
iocContainer.Entries.Add(typeof(IStepPersister).FullName!, new DemoInMemoryPersister());

// step 2. build the engine and specify as many workers as you want in parallelism
// The engine even supports being started on multiple machines 
IWorkflowLogger logger = new ConsoleStepLogger();
var formatter = new DotNetStepStateFormatterJson(logger);
var engine = new WorkflowEngine(logger, iocContainer, formatter);

// step 3. add a step to be executed - this step will spawn new steps during processing
await engine.Data.AddStepAsync(new Step(FetchData.Name, 0));

// step 4. GO!
engine.Start(new WorkflowConfiguration(new WorkerConfig { StopWhenNoWork = true }, NumberOfWorkers: 1));

// don't close the window yet
Console.ReadLine();



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

        return await Task.FromResult(step.Done().
                With(new Step(SendEmail.Name, topWords)));
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