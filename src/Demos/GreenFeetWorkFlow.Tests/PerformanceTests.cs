using System.Collections.Concurrent;
using System.Diagnostics;

using static GreenFeetWorkflow.Tests.TestHelper;

namespace GreenFeetWorkflow.Tests;

public class PerformanceTests
{
    [Test]
    [TestCase(50)] // 3 (local 00:00:00.5890152)
    [TestCase(100)] // 5 (local 00:00:00.0149946)
    [TestCase(200)] // 10 (local 00:00:00.0244962)
    public async Task Inserting_10000_steps_timing(int max)
    {
        var helper = new TestHelper();

        var engine = helper.Build();
        var watch = Stopwatch.StartNew();
        var name = "inserttest";

        var steps = Enumerable.Range(0, max).Select(x => new Step(name)).ToArray();
        engine.Data.AddSteps(steps);

        watch.Stop();
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds((max / 50.0) * 3.01));
        Console.WriteLine(watch);
    }

    [Test]
    [TestCase(50)] // 3
    [TestCase(100)] // 5
    [TestCase(200)] // 10
    public async Task Inserting_10000_steps_timing_bulk(int max)
    {
        var helper = new TestHelper();

        var engine = helper.Build();
        var watch = Stopwatch.StartNew();
        var name = "inserttest3";

        var steps = Enumerable.Range(0, max).Select(x => new Step(name)).ToArray();
        await engine.Data.AddStepsBulkAsync(steps);

        watch.Stop();
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(0.9));
        Console.WriteLine(watch);
    }


    /*
     *
DelayNoReadyWork = 1 sec
worker/a/process/22164/0 waits: 0 futile-fetches:1 work done:0
Worker count: 1 Elapsed: 295ms 
worker/a/process/22164/1 waits: 6 futile-fetches:7 work done:0
worker/a/process/22164/0 waits: 6 futile-fetches:7 work done:0
Worker count: 2 Elapsed: 6003ms 
worker/a/process/22164/0 waits: 6 futile-fetches:7 work done:0
worker/a/process/22164/1 waits: 6 futile-fetches:7 work done:0
worker/a/process/22164/3 waits: 6 futile-fetches:7 work done:0
worker/a/process/22164/2 waits: 6 futile-fetches:7 work done:0
Worker count: 4 Elapsed: 6015ms 
worker/a/process/22164/6 waits: 6 futile-fetches:6 work done:0
worker/a/process/22164/3 waits: 6 futile-fetches:7 work done:0
worker/a/process/22164/1 waits: 6 futile-fetches:7 work done:0
worker/a/process/22164/2 waits: 6 futile-fetches:7 work done:0
worker/a/process/22164/7 waits: 6 futile-fetches:6 work done:0
worker/a/process/22164/0 waits: 6 futile-fetches:7 work done:0
worker/a/process/22164/4 waits: 6 futile-fetches:6 work done:0
worker/a/process/22164/5 waits: 6 futile-fetches:6 work done:0
Worker count: 8 Elapsed: 6003ms 

       
     *
DelayNoReadyWork = 3 sec
worker/a/process/52120/0 waits: 0 futile-fetches:1 work done:0
Worker count: 1 Elapsed: 220ms 
worker/a/process/52120/1 waits: 2 futile-fetches:3 work done:0
worker/a/process/52120/0 waits: 2 futile-fetches:2 work done:0
Worker count: 2 Elapsed: 6018ms 
worker/a/process/52120/2 waits: 2 futile-fetches:2 work done:0
worker/a/process/52120/0 waits: 2 futile-fetches:3 work done:0
worker/a/process/52120/1 waits: 2 futile-fetches:3 work done:0
worker/a/process/52120/3 waits: 2 futile-fetches:3 work done:0
Worker count: 4 Elapsed: 6014ms 
worker/a/process/52120/2 waits: 2 futile-fetches:3 work done:0
worker/a/process/52120/6 waits: 2 futile-fetches:2 work done:0
worker/a/process/52120/1 waits: 2 futile-fetches:3 work done:0
worker/a/process/52120/7 waits: 2 futile-fetches:2 work done:0
worker/a/process/52120/3 waits: 2 futile-fetches:3 work done:0
worker/a/process/52120/0 waits: 2 futile-fetches:3 work done:0
worker/a/process/52120/5 waits: 2 futile-fetches:2 work done:0
worker/a/process/52120/4 waits: 2 futile-fetches:2 work done:0
Worker count: 8 Elapsed: 6015ms 

     */
    [Test]
    [Explicit("slow")]
    public void When_running_idle_for_6_seconds_Then_ensure_we_do_not_overpoll_the_db()
    {
        var testhelper = new TestHelper()
        {
            cts = new CancellationTokenSource(TimeSpan.FromSeconds(6)),
            StepHandlers = [("v1/performance/idle", GenericImplementation.Create(step => { }))],
        };
        var stopwach = Stopwatch.StartNew();
        testhelper.UseMax1Worker().BuildAndStart();
        stopwach.Stop();

        Console.WriteLine($"Elapsed: {stopwach.ElapsedMilliseconds}ms ");
    }


    /*
     when delay is always extended 
    
    SharedThresholdToReducePollingReadyItems = DateTime.Now + workerConfig.DelayNoReadyWork;


worker/a/process/43088/1 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/0 waits: 1 futile-fetches:2 work done:2750
       worker/a/process/43088/2 waits: 1 futile-fetches:1 work done:2250
       Worker count: 3 Elapsed: 1883ms Elements: 5000 ~ 0,3766 ms / per element and 2655 pr. sec.
       
       
       worker/a/process/43088/1 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/0 waits: 2 futile-fetches:3 work done:7
       worker/a/process/43088/2 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/3 waits: 0 futile-fetches:0 work done:4993
       Worker count: 4 Elapsed: 1944ms Elements: 5000 ~ 0,3888 ms / per element and 2572 pr. sec.
       
       
       worker/a/process/43088/2 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/4 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/3 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/1 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/0 waits: 0 futile-fetches:0 work done:5000
       Worker count: 5 Elapsed: 1934ms Elements: 5000 ~ 0,3868 ms / per element and 2585 pr. sec.
       
       
       worker/a/process/43088/3 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/5 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/4 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/2 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/0 waits: 1 futile-fetches:2 work done:2683
       worker/a/process/43088/1 waits: 1 futile-fetches:1 work done:2317
       Worker count: 6 Elapsed: 1992ms Elements: 5000 ~ 0,3984 ms / per element and 2510 pr. sec.
       
       
       worker/a/process/43088/5 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/1 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/2 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/6 waits: 2 futile-fetches:3 work done:0
       worker/a/process/43088/4 waits: 1 futile-fetches:2 work done:2680
       worker/a/process/43088/0 waits: 2 futile-fetches:3 work done:1
       worker/a/process/43088/7 waits: 1 futile-fetches:0 work done:2319
       worker/a/process/43088/3 waits: 2 futile-fetches:3 work done:0
       Worker count: 8 Elapsed: 1937ms Elements: 5000 ~ 0,3874 ms / per element and 2581 pr. sec.



    when delay is only extended when expired
   var firstWorkerToHaveNoWork = SharedThresholdToReducePollingReadyItems < DateTime.Now;
   if (firstWorkerToHaveNoWork)
       SharedThresholdToReducePollingReadyItems = DateTime.Now + workerConfig.DelayNoReadyWork;


worker/a/process/24980/0 waits: 0 futile-fetches:0 work done:5000
Worker count: 1 Elapsed: 2204ms Elements: 5000 ~ 0,4408 ms / per element and 2268 pr. sec.


worker/a/process/24980/0 waits: 0 futile-fetches:0 work done:5000
Worker count: 1 Elapsed: 1492ms Elements: 5000 ~ 0,2984 ms / per element and 3351 pr. sec.


worker/a/process/24980/0 waits: 2 futile-fetches:3 work done:6
worker/a/process/24980/1 waits: 0 futile-fetches:0 work done:4994
Worker count: 2 Elapsed: 1579ms Elements: 5000 ~ 0,3158 ms / per element and 3166 pr. sec.


worker/a/process/24980/1 waits: 2 futile-fetches:3 work done:0
worker/a/process/24980/2 waits: 2 futile-fetches:3 work done:0
worker/a/process/24980/0 waits: 0 futile-fetches:0 work done:5000
Worker count: 3 Elapsed: 1497ms Elements: 5000 ~ 0,2994 ms / per element and 3340 pr. sec.


worker/a/process/24980/0 waits: 1 futile-fetches:2 work done:3367
worker/a/process/24980/2 waits: 2 futile-fetches:3 work done:0
worker/a/process/24980/1 waits: 2 futile-fetches:3 work done:7
worker/a/process/24980/3 waits: 1 futile-fetches:0 work done:1626
Worker count: 4 Elapsed: 1544ms Elements: 5000 ~ 0,3088 ms / per element and 3238 pr. sec.


worker/a/process/24980/0 waits: 2 futile-fetches:3 work done:1
worker/a/process/24980/4 waits: 2 futile-fetches:3 work done:0
worker/a/process/24980/3 waits: 1 futile-fetches:2 work done:3297
worker/a/process/24980/2 waits: 2 futile-fetches:3 work done:0
worker/a/process/24980/1 waits: 1 futile-fetches:1 work done:1702
Worker count: 5 Elapsed: 1576ms Elements: 5000 ~ 0,3152 ms / per element and 3172 pr. sec.


worker/a/process/24980/5 waits: 2 futile-fetches:3 work done:0
worker/a/process/24980/3 waits: 2 futile-fetches:3 work done:0
worker/a/process/24980/2 waits: 2 futile-fetches:3 work done:0
worker/a/process/24980/0 waits: 1 futile-fetches:2 work done:3078
worker/a/process/24980/4 waits: 2 futile-fetches:3 work done:0
worker/a/process/24980/1 waits: 1 futile-fetches:1 work done:1922
Worker count: 6 Elapsed: 1602ms Elements: 5000 ~ 0,3204 ms / per element and 3121 pr. sec.


worker/a/process/24980/1 waits: 1 futile-fetches:2 work done:3253
worker/a/process/24980/4 waits: 2 futile-fetches:3 work done:0
worker/a/process/24980/5 waits: 2 futile-fetches:3 work done:43
worker/a/process/24980/0 waits: 2 futile-fetches:3 work done:1
worker/a/process/24980/2 waits: 2 futile-fetches:3 work done:0
worker/a/process/24980/3 waits: 2 futile-fetches:3 work done:0
worker/a/process/24980/6 waits: 1 futile-fetches:1 work done:1703
worker/a/process/24980/7 waits: 2 futile-fetches:2 work done:0
Worker count: 8 Elapsed: 1593ms Elements: 5000 ~ 0,3186 ms / per element and 3138 pr. sec.

     */

    /// <summary>
    /// ensure when we workers share a wait-threshold they do not block each other
    /// hence one step to be rerun many times means many workers have the opportunity to discover no work
    /// </summary>
    [Test]
    [Explicit("slow")]
    public void When_rerun_1_step_Then_expect_all_to_have_run()
    {
        const int max = 1000;
        const string StepName = "v1/performance/single-many-reruns";

        void doo(int workerCount)
        {
            var testhelper = new TestHelper();
            testhelper.WorkflowConfiguration.WorkerConfig.MaxWorkerCount = workerCount;
            testhelper.WorkflowConfiguration.LoggerConfiguration = LoggerConfiguration.OFF;
            testhelper.StepHandlers = [(StepName, GenericImplementation.Create(step =>
            {
                int counter = testhelper.Formatter!.Deserialize<int>(step.State);

                if (counter >= max)
                {
                    testhelper.cts.Cancel();
                    return step.Done();
                }
                return step.Rerun(counter + 1, scheduleTime: DateTime.Now);
            }))];
            testhelper.Steps = [new Step(StepName, 1) { CorrelationId = testhelper.RndName }];

            var stopwach = Stopwatch.StartNew();
            testhelper.BuildAndStart();
            stopwach.Stop();

            int noElements = max;
            Console.WriteLine($"Max Workers: {workerCount} Created:{testhelper.Engine.WorkerCoordinator.TotalWorkerCreated} Elapsed: {stopwach.ElapsedMilliseconds}ms Elements: {noElements} ~ {stopwach.ElapsedMilliseconds / (double)noElements} ms / per element and {noElements * 1000 / stopwach.ElapsedMilliseconds} pr. sec.\n\n");
        }

        doo(1);
        doo(2);
        doo(3);
        doo(4);
        doo(5);
        doo(6);
        doo(8);
    }

    [Test]
    [Explicit("slow")]
    public void When_rerun_10_steps_each_repeating_1000_times_Then_expect_all_to_have_run()
    {
        const string stepName = "v1/performance/many-reruns";
        const int max = 1000;

        void doo(int workerCount)
        {
            ConcurrentBag<int> stepResults = new ConcurrentBag<int>();
            var correlationIds = Enumerable.Range(0, 10).Select(x => Guid.NewGuid().ToString()).ToArray();

            var testhelper = new TestHelper();
            testhelper.WorkflowConfiguration.WorkerConfig.MaxWorkerCount = workerCount;
            testhelper.WorkflowConfiguration.LoggerConfiguration = LoggerConfiguration.OFF;
            testhelper.StepHandlers =
            [
                Handle(stepName, step =>
                {
                    int counter = testhelper.Formatter!.Deserialize<int>(step.State);

                    if (counter >= max)
                    {
                        stepResults.Add(counter);

                        if (stepResults.Count >= correlationIds.Length)
                            testhelper.cts.Cancel();

                        return step.Done();
                    }

                    return step.Rerun(counter + 1, scheduleTime: DateTime.Now);
                })
            ];
            testhelper.Steps = correlationIds.Select(x => new Step(stepName, 1) { CorrelationId = x }).ToArray();

            var stopwach = Stopwatch.StartNew();
            testhelper.BuildAndStart();
            stopwach.Stop();

            int noElements = correlationIds.Length * max;
            Console.WriteLine($"Worker count: {workerCount} Elapsed: {stopwach.ElapsedMilliseconds}ms Elements: {noElements} ~ {stopwach.ElapsedMilliseconds / (double)noElements} ms / per element and {noElements * 1000 / stopwach.ElapsedMilliseconds} pr. sec.");
        }

        doo(1);
        doo(1);
        doo(2);
        doo(3);
        doo(4);
        doo(5);
        doo(6);
        doo(8);
    }
}