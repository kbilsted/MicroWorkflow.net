using System.Collections.Concurrent;
using System.Diagnostics;

namespace GreenFeetWorkflow.Tests;

[Explicit("slow")]
public class PerformanceTests
{
    [Test]
    public void Inserting_10000_steps_timing()
    {
        var helper = new TestHelper();

        var engine = helper.CreateEngine();
        var watch = Stopwatch.StartNew();
        var name = "inserttest";

        var steps = Enumerable.Range(0, 10000).Select(x => new Step(name)).ToArray();
        engine.Data.AddSteps(steps);

        watch.Stop();
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));
        Console.WriteLine(watch);
    }

    [Test]
    public void When_rerun_10_steps_Then_expect_all_to_have_run()
    {
        void doo(int workerCount)
        {
            var testhelper = new TestHelper();
            ConcurrentBag<int> stepResults = new ConcurrentBag<int>();

            var correlationIds = Enumerable.Range(0, 10).Select(x => Guid.NewGuid().ToString()).ToArray();
            var steps = correlationIds.Select(x => new Step("v1/performanc/many-reruns", 1) { CorrelationId = x }).ToArray();
            const int max = 1000;

            var runner = ("v1/performanc/many-reruns", GenericImplementation.Create(step =>
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
            }));

            var stopwach = Stopwatch.StartNew();
            testhelper.CreateAndRunEngineForPerformance(steps, workerCount, runner);
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

        //testhelper.Persister.CountTables(correlationId).Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
        //    { StepStatus.Ready, 0},
        //    { StepStatus.Done, 4},
        //    { StepStatus.Failed, 0},
        //});
    }

    [Test]
    [Explicit]
    public void When_spawn_newsteps_count_to_N_Then_expect_all_to_have_run()
    {
        void doo(int workerCount)
        {
            var testhelper = new TestHelper();
            ConcurrentBag<(string, int)> stepResults = new ConcurrentBag<(string, int)>();

            var correlationIds = Enumerable.Range(0, 10).Select(x => Guid.NewGuid().ToString()).ToArray();
            var steps = correlationIds.Select(x => new Step("v1/performanc/many-new-steps", 1) { CorrelationId = x }).ToArray();
            const int max = 100;

            var runner = ("v1/performanc/many-new-steps", GenericImplementation.Create(step =>
            {
                int counter = testhelper.Formatter!.Deserialize<int>(step.State);

                if (counter == max)
                {
                    stepResults.Add((step.CorrelationId!, counter));

                    if (stepResults.Count == correlationIds.Length)
                        testhelper.cts.Cancel();

                    return step.Done();
                }
                return step.Done().With(new Step("v1/performanc/many-new-steps", counter + 1));
            }));

            var stopwach = Stopwatch.StartNew();
            testhelper.CreateAndRunEngine(steps, workerCount, runner);
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

        //testhelper.Persister.CountTables(correlationId).Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
        //    { StepStatus.Ready, 0},
        //    { StepStatus.Done, 4},
        //    { StepStatus.Failed, 0},
        //});
    }
}