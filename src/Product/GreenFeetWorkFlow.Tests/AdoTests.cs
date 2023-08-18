using System.Diagnostics;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

namespace GreenFeetWorkflow.Tests;

public class WorkerTests
{
    TestHelper helper = new TestHelper();

    [SetUp]
    public void Setup()
    {
        helper = new TestHelper();
    }

    [Test]
    public void When_creating_a_transaction_from_interfacetype_Then_it_is_created()
    {

        helper.CreateEngine();
        using IStepPersister p = helper.Persister;
        p.CreateTransaction();

        p.Transaction.Should().NotBeNull();
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
        helper.Persister.CountTables(helper.FlowId).Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
            { StepStatus.Ready, 0},
            { StepStatus.Done, 1},
            { StepStatus.Failed, 0},
        });
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
        engine.Runtime.Data.AddSteps(tx, new Step(name, 0));
        engine.Runtime.Data.AddSteps(tx, new Step(name, 1));
        tx.Commit();
        engine.Start(1, true);

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

        helper.Persister.CountTables(helper.FlowId).Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
            { StepStatus.Ready, 0},
            { StepStatus.Done, 0},
            { StepStatus.Failed, 1},
        });
    }

    [Test]
    public void When_executing_step_throwing_special_FailCurrentStepException_from_step_Then_fail_current_step()
    {


        helper.CreateAndRunEngine(
            new Step("test-throw-failstepexception_from_step_variable") { FlowId = helper.FlowId },
            (
                "test-throw-failstepexception_from_step_variable",
                GenericStepHandler.Create(step => throw step.FailAsException())));

        helper.Persister.CountTables(helper.FlowId).Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
            { StepStatus.Ready, 0},
            { StepStatus.Done, 0},
            { StepStatus.Failed, 1},
        });
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

        helper.Persister.CountTables(helper.FlowId)
            .Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
                { StepStatus.Ready, 1},
                { StepStatus.Done, 0},
                { StepStatus.Failed, 0},
            });

        var row = helper.Persister.GetStep(dbid!.Value);
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
        helper.Persister.CountTables(helper.FlowId).Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
            { StepStatus.Ready, 0},
            { StepStatus.Done, 1},
            { StepStatus.Failed, 0},
        });
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
        helper.Persister.CountTables(helper.FlowId).Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
            { StepStatus.Ready, 0},
            { StepStatus.Done, 2},
            { StepStatus.Failed, 0},
        });
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

        helper.Persister.CountTables(helper.FlowId).Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
            { StepStatus.Ready, 0},
            { StepStatus.Done, 2},
            { StepStatus.Failed, 0},
        });
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

        helper.Persister.CountTables(helper.FlowId).Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
            { StepStatus.Ready, 1},
            { StepStatus.Done, 1},
            { StepStatus.Failed, 0},
        });
    }

    [Test]
    public void When_step_is_in_the_future_Then_it_can_be_activated_to_execute_now()
    {
        string? stepResult = null;

        var name = "When_step_is_in_the_future_Then_it_can_be_activated_to_execute_now";

        Step futureStep = new()
        {
            Name = name,
            FlowId = helper.FlowId,
            SearchKey = helper.FlowId.ToString(),
            ScheduleTime = DateTime.Now.AddYears(35)
        };
        helper.CreateAndRunEngine(
            new[] { futureStep },
            (name,
            step =>
            {
                stepResult = step.SearchKey;
                return step.Done();
            }
        ));

        helper.Persister.CountTables(helper.FlowId).Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
            { StepStatus.Ready, 1},
            { StepStatus.Done, 0},
            { StepStatus.Failed, 0},
        });

        _ = helper.Engine!.Runtime.Data.ActivateStep(helper.FlowId.ToString(), null, null);
        helper.Engine.Start(numberOfWorkers: 1, stopWhenNoWorkLeft: true);

        stepResult.Should().Be(helper.FlowId.ToString());

        helper.Persister.CountTables(helper.FlowId).Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
            { StepStatus.Ready, 0},
            { StepStatus.Done, 1},
            { StepStatus.Failed, 0},
        });
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

        helper.Persister.CountTables(helper.FlowId)
            .Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
            { StepStatus.Ready, 1},
            { StepStatus.Done, 1},
            { StepStatus.Failed, 0},
        });
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

        helper.Persister.CountTables(helper.FlowId).Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
            { StepStatus.Ready, 0},
            { StepStatus.Done, 4},
            { StepStatus.Failed, 0},
        });
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