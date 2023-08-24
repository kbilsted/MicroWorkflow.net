using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using ReassureTest;

namespace GreenFeetWorkflow.Tests;

public class WorkerTests
{
    TestHelper helper = new TestHelper();
    private WfRuntimeConfiguration cfg = new WfRuntimeConfiguration(
        new WorkerConfig()
        {
            StopWhenNoWork = true
        },
        NumberOfWorkers: 1);

    [SetUp]
    public void Setup()
    {
        helper = new TestHelper();
    }

    [Test]
    public void When_executing_OneStep_with_no_state_Then_succeed()
    {
        string? stepResult = null;

        helper.CreateAndRunEngine(
            new[] { new Step("OneStep") { InitialState = 1234, FlowId = helper.FlowId } },
            ("OneStep", new GenericStepHandler(step =>
            {
                int counter = helper.Formatter!.Deserialize<int>(step.PersistedState);
                stepResult = $"hello {counter}";
                return ExecutionResult.Done();
            })));

        stepResult.Should().Be("hello 1234");
        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 1, failed: 0);
    }

    [Test]
    public void When_adding_two_steps_in_the_same_transaction_Then_succeed()
    {
        string[] stepResults = new string[2];
        const string name = "v1/When_adding_two_steps_in_the_same_transaction_Then_succeed";

        var engine = helper.CreateEngine(
            (name, new GenericStepHandler(step =>
            {
                int counter = helper.Formatter!.Deserialize<int>(step.PersistedState);
                stepResults[counter] = $"hello {counter}";
                return ExecutionResult.Done();
            })));

        using var connection = new SqlConnection(helper.ConnectionString);
        connection.Open();
        using var tx = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
        engine.Runtime.Data.AddStep(new Step(name, 0), tx);
        engine.Runtime.Data.AddStep(new Step(name, 1), tx);
        tx.Commit();
        engine.Start(cfg);

        stepResults.Should().BeEquivalentTo(new[] { "hello 0", "hello 1" });
    }

    [Test]
    public void When_executing_step_throwing_special_FailCurrentStepException_Then_fail_current_step()
    {
        helper.CreateAndRunEngine(
             new Step() { Name = "test-throw-failstepexception", FlowId = helper.FlowId },
            (
                "test-throw-failstepexception",
                GenericStepHandler.Create(step => throw new FailCurrentStepException("some description"))
            ));

        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 0, failed: 1);
    }

    [Test]
    public void When_executing_step_throwing_special_FailCurrentStepException_from_step_Then_fail_current_step()
    {
        helper.CreateAndRunEngine(
            new Step("test-throw-failstepexception_from_step_variable") { FlowId = helper.FlowId },
            (
                "test-throw-failstepexception_from_step_variable",
                GenericStepHandler.Create(step => throw step.FailAsException())));

        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 0, failed: 1);
    }

    [Test]
    public async Task When_executing_step_throwing_special_FailCurrentStepException_and_add_step_Then_fail_current_step_and_add_ready_step()
    {
        var name = "test-throw-failstepexception-with-newStep";
        var nameNewStep = "test-throw-failstepexception-with-newStep-newstepname";
        var engine = helper.CreateEngine(
            (
                name,
                GenericStepHandler.Create(step => throw step.FailAsException(newSteps: new Step(nameNewStep))))
            );
        engine.Runtime.Data.AddStep(new Step(name) { FlowId = helper.FlowId });
        await engine.StartAsSingleWorker(cfg);

        var steps = engine.Runtime.Data.SearchSteps(new SearchModel(FlowId: helper.FlowId) { FetchLevel = new(true, true, true) });

        steps.Is(@" [
    {
        Key = Ready
        Value = [
            {
                Id = *
                Name = `test-throw-failstepexception-with-newStep-newstepname`
                Singleton = false
                FlowId = *
                SearchKey = null
                InitialState = null
                PersistedState = null
                PersistedStateFormat = null
                ExecutionCount = *
                ExecutionDurationMillis = null
                ExecutionStartTime = null
                ExecutedBy = null
                CreatedTime = now
                CreatedByStepId = *
                ScheduleTime = *
                CorrelationId = null
                Description = `Worker: missing step-implementation for step 'test-throw-failstepexception-with-newStep-newstepname'`
            }
        ]
    },
    {
        Key = Done
        Value = []
    },
    {
        Key = Failed
        Value = [
            {
                Id = *
                Name = `test-throw-failstepexception-with-newStep`
                Singleton = false
                FlowId = *
                SearchKey = null
                InitialState = null
                PersistedState = null
                PersistedStateFormat = null
                ExecutionCount = 1
                ExecutionDurationMillis = 0
                ExecutionStartTime = now
                ExecutedBy = null
                CreatedTime = now
                CreatedByStepId = 0
                ScheduleTime = now
                CorrelationId = null
                Description = `Exception of type 'GreenFeetWorkflow.FailCurrentStepException' was thrown.`
            }
        ]
    }
]");
    }

    [Test]
    public void When_executing_step_throwing_exception_Then_rerun_current_step_and_ensure_state_is_unchanged()
    {
        int? dbid = null;

        helper.CreateAndRunEngine(
            new Step("test-throw-exception", "hej") { FlowId = helper.FlowId },
            (
                "test-throw-exception",
                GenericStepHandler.Create(step =>
                {
                    dbid = step.Id;
                    throw new Exception("exception message");
                })));

        helper.AssertTableCounts(helper.FlowId, ready: 1, done: 0, failed: 0);

        var row = helper.Persister.Go((p) =>
        p.SearchSteps(new SearchModel(Id: dbid!.Value) { FetchLevel = new(Ready: true) })
        [StepStatus.Ready].Single());
        row!.PersistedState.Should().Be("\"hej\"");
        row.FlowId.Should().Be(helper.FlowId);
        row.Name.Should().Be("test-throw-exception");
    }

    [Test]
    public void OneStep_fail()
    {
        string? stepResult = null;

        var stepHandler = ("onestep_fails", new GenericStepHandler(stepToExecute =>
        {
            if (stepToExecute.ExecutionCount == 0)
                return ExecutionResult.Fail();

            stepResult = "hello";
            return ExecutionResult.Done();
        }));

        new TestHelper().CreateAndRunEngine(new[] { new Step("onestep_fails") }, stepHandler);

        stepResult.Should().BeNull();
    }

    [Test]
    public void OneStep_Repeating_Thrice()
    {
        string? stepResult = null;

        var stepHandler = ("repeating_step", new GenericStepHandler(step =>
        {
            int counter = helper.Formatter!.Deserialize<int>(step.PersistedState);

            stepResult = $"hello {counter}";

            if (counter < 3)
                return ExecutionResult.Rerun(stateForRerun: counter + 1, scheduleTime: step.ScheduleTime);
            return ExecutionResult.Done();
        }));

        helper.CreateAndRunEngine(new Step("repeating_step") { InitialState = 1, FlowId = helper.FlowId }, stepHandler);

        stepResult.Should().Be("hello 3");

        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 1, failed: 0);
    }

    [Test]
    public void TwoSteps_flow_same_flowid()
    {
        string? stepResult = null;

        var implA = ("check-flowid/a", new GenericStepHandler(step => step.Done(new Step("check-flowid/b"))));
        var implB = ("check-flowid/b", new GenericStepHandler(step =>
        {
            stepResult = step.FlowId;
            return ExecutionResult.Done();
        }));

        helper.CreateAndRunEngine(new Step { Name = "check-flowid/a", FlowId = helper.FlowId }, implA, implB);

        stepResult.Should().Be($"{helper.FlowId}");

        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 2, failed: 0);
    }

    [Test]
    public void TwoSteps_flow_same_correlationid()
    {
        string? stepResult = null;

        var implA = ("check-correlationid/a", new GenericStepHandler(step => step.Done(new Step("check-correlationid/b"))));
        var implB = ("check-correlationid/b", new GenericStepHandler(step =>
        {
            stepResult = step.CorrelationId;
            return ExecutionResult.Done();
        }));

        helper.CreateAndRunEngine(new Step
        {
            Name = "check-correlationid/a",
            CorrelationId = helper.CorrelationId,
            FlowId = helper.FlowId,
        }, implA, implB);

        stepResult.Should().Be(helper.CorrelationId);

        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 2, failed: 0);
    }

    [Test]
    public void When_a_step_creates_a_new_step_Then_new_step_may_change_correlationid()
    {
        string? stepResult = null;
        string oldId = Guid.NewGuid().ToString();
        string newId = Guid.NewGuid().ToString();

        var cookHandler = ("check-correlationidchange/cookFood", new GenericStepHandler(step =>
        {
            return step.Done()
                .With(new Step("check-correlationidchange/eat")
                {
                    CorrelationId = newId,
                });
        }));
        var eatHandler = ("check-correlationidchange/eat", new GenericStepHandler(step =>
        {
            stepResult = step.CorrelationId;
            return step.Done();
        }));

        helper.CreateAndRunEngine(new[] { new Step()
            {
                Name = "check-correlationidchange/cookFood",
                CorrelationId = oldId
            } },
            cookHandler, eatHandler);

        stepResult.Should().Be(newId);
    }




    [Test]
    public void TwoSteps_flow_last_step_starting_in_the_future_so_test_terminate_before_its_executions()
    {
        string? stepResult = null;


        helper.CreateAndRunEngine(
            new[] { new Step() { Name = "check-future-step/cookFood", FlowId = helper.FlowId } },
            ("check-future-step/cookFood",
            stepToExecute =>
            {
                string food = "potatoes";
                stepResult = $"cooking {food}";
                return ExecutionResult.Done(
                    new Step("check-future-step/eat", food) { ScheduleTime = DateTime.Now.AddYears(30) });
            }
        ),
            ("check-future-step/eat",
            stepToExecute =>
            {
                var food = helper.Formatter!.Deserialize<string>(stepToExecute.PersistedState);
                stepResult = $"eating {food}";
                return ExecutionResult.Done();
            }
        ));

        stepResult.Should().Be($"cooking potatoes");

        helper.AssertTableCounts(helper.FlowId, ready: 1, done: 1, failed: 0);
    }

    [Test]
    public void When_step_is_in_the_future_Then_it_can_be_activated_to_execute_now()
    {
        string? stepResult = null;

        var name = "When_step_is_in_the_future_Then_it_can_be_activated_to_execute_now";

        var engine = helper.CreateEngine((name, GenericStepHandler.Create(step => { stepResult = step.FlowId; return step.Done(); })));
        Step futureStep = new()
        {
            Name = name,
            FlowId = helper.FlowId,
            ScheduleTime = DateTime.Now.AddYears(35)
        };
        var id = engine.Runtime.Data.AddStep(futureStep, null);
        engine.Start(cfg);
        helper.AssertTableCounts(helper.FlowId, ready: 1, done: 0, failed: 0);

        var count = engine.Runtime.Data.ActivateStep(id, null);
        count.Should().Be(1);

        helper.Engine.Start(cfg);

        stepResult.Should().Be(helper.FlowId.ToString());

        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 1, failed: 0);
    }

    [Test]
    public void TwoSteps_flow_with_last_step_undefined_stephandler__so_test_terminate()
    {
        string? stepResult = null;

        var cookHandler = ("undefined-next-step/cookFood", new GenericStepHandler((step) =>
        {
            stepResult = $"cooking {"potatoes"}";
            return step.Done(new Step("undefined-next-step/eat", "potatoes"));
        }));

        helper.CreateAndRunEngine(
            new[] { new Step() { Name = "undefined-next-step/cookFood", FlowId = helper.FlowId } },
            cookHandler);

        stepResult.Should().Be($"cooking potatoes");

        helper.AssertTableCounts(helper.FlowId, ready: 1, done: 1, failed: 0);
    }


    [Test]
    public void When_a_step_creates_two_steps_Then_those_steps_can_be_synchronized_and_join_into_a_forth_merge_step()
    {
        string? stepResult = null;

        var stepDriveToShop = new Step("v1/forkjoin/drive-to-shop", new[] { "milk", "cookies" });
        var payForStuff = new Step("v1/forkjoin/pay");

        var drive = ("v1/forkjoin/drive-to-shop", GenericStepHandler.Create(step =>
        {
            stepResult = $"driving";
            var id = Guid.NewGuid();
            Step milk = new("v1/forkjoin/pick-milk", new BuyInstructions() { Item = "milk", Count = 1, PurchaseId = id });
            Step cookies = new("v1/forkjoin/pick-cookies", new BuyInstructions() { Item = "cookies", Count = 30, PurchaseId = id });
            Step pay = new("v1/forkjoin/pay-for-all", (count: 2, id, maxWait: DateTime.Now.AddSeconds(8)));
            return step.Done(milk, cookies, pay);
        }));

        var checkout = ("v1/forkjoin/pay-for-all", GenericStepHandler.Create(step =>
        {
            (int count, Guid id, DateTime maxWait) = helper.Formatter!.Deserialize<(int, Guid, DateTime)>(step.PersistedState);
            var sales = GroceryBuyer.SalesDb.Where(x => x.id == id).ToArray();
            if (sales.Length != 2 && DateTime.Now <= maxWait)
                return ExecutionResult.Rerun(scheduleTime: DateTime.Now.AddSeconds(0.1));

            stepResult = $"total: {sales.Sum(x => x.total)}";
            helper.cts.Cancel();
            return ExecutionResult.Done();
        }));

        helper.CreateAndRunEngine(
            new[] { new Step() { Name = "v1/forkjoin/drive-to-shop", FlowId = helper.FlowId } },
            4,
            drive,
            checkout,
            ("v1/forkjoin/pick-milk", new GroceryBuyer()),
            ("v1/forkjoin/pick-cookies", new GroceryBuyer()));

        stepResult.Should().Be($"total: 61");

        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 4, failed: 0);
    }


    class BuyInstructions
    {
        public Guid PurchaseId { get; set; }
        public string? Item { get; set; }
        public int Count { get; set; }
    }

    class GroceryBuyer : IStepImplementation
    {
        internal static readonly List<(Guid id, string name, int total)> SalesDb = new();
        static readonly Dictionary<string, int> prices = new() { { "milk", 1 }, { "cookies", 2 } };

        public async Task<ExecutionResult> ExecuteAsync(Step step)
        {
            Debug.WriteLine("Picking up stuff");
            Thread.Sleep(100);

            var instruction = JsonConvert.DeserializeObject<BuyInstructions>(step.PersistedState!);

            lock (SalesDb)
            {
                SalesDb.Add((instruction!.PurchaseId, instruction.Item!, prices[instruction.Item!] * instruction.Count));
            }

            return await Task.FromResult(ExecutionResult.Done());
        }
    }
}