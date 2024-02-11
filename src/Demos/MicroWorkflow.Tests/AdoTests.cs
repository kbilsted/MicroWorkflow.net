using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using ReassureTest;
using System.Diagnostics;
using static MicroWorkflow.TestHelper;

namespace MicroWorkflow;

public class WorkerTests
{
    TestHelper helper = new();

    readonly WorkflowConfiguration cfg = new WorkflowConfiguration(
        new WorkerConfig()
        {
            StopWhenNoImmediateWork = true
        });

    [SetUp]
    public void Setup()
    {
        helper = new TestHelper();
    }

    [Test]
    public void When_executing_OneStep_with_initialstate_Then_that_state_is_accessible_and_step_is_executed()
    {
        string? stepResult = null;
        bool? stepResultIsSingleton = null;
        const string StepName = "OneStep";
        helper.Steps = [new Step(StepName)
        {
            InitialState = 1234,
            FlowId = helper.FlowId
        }];
        helper.StepHandlers = [(StepName, new GenericImplementation(step =>
        {
            int counter = helper.Formatter!.Deserialize<int>(step.State);
            stepResult = $"hello {counter}";
            stepResultIsSingleton = step.Singleton;
            return ExecutionResult.Done();
        }))];

        helper.StopWhenNoWork().BuildAndStart();

        stepResult.Should().Be("hello 1234");
        stepResultIsSingleton.Should().BeFalse();
        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 1, failed: 0);
    }

    [Test]
    public void When_adding_two_steps_in_the_same_transaction_Then_both_execute()
    {
        string[] stepResults = new string[2];
        const string name = "v1/When_adding_two_steps_in_the_same_transaction_Then_succeed";

        helper.StepHandlers = [Handle(name, step =>
        {
            int counter = helper.Formatter!.Deserialize<int>(step.State);
            stepResults[counter] = $"hello {counter}";
            return ExecutionResult.Done();
        })];

        var engine = helper.StopWhenNoWork().Build();

        engine.Data.AddSteps([new Step(name, 0), new Step(name, 1)]);

        helper.Start();

        stepResults.Should().BeEquivalentTo(new[] { "hello 0", "hello 1" });
    }

    IEnumerable<Step> GetByFlowId() => GetAllByFlowId().SelectMany(x => x.Value);

    Dictionary<StepStatus, List<Step>> GetAllByFlowId() => helper.Engine!.Data.SearchSteps(new SearchModel(FlowId: helper.FlowId), FetchLevels.ALL);

    [Test]
    public void When_executing_step_throwing_special_FailCurrentStepException_Then_fail_current_step()
    {
        const string name = "test-throw-failstepexception";
        helper.StepHandlers = [(
                "test-throw-failstepexception",
                GenericImplementation.Create(step => throw new FailCurrentStepException("some description"))
            )];
        helper.Steps = [new Step() { Name = name, FlowId = helper.FlowId }];
        helper.StopWhenNoWork().BuildAndStart();

        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 0, failed: 1);
        GetByFlowId().Should().Satisfy(x => x.Description == "some description");
    }

    [Test]
    public void When_executing_step_throwing_special_FailCurrentStepException_using_step_Then_fail_current_step()
    {
        const string name = "test-throw-failstepexception_from_step_variable";
        helper.Steps = [new Step(name) { FlowId = helper.FlowId }];
        helper.StepHandlers = [Handle(name, step => throw step.FailAsException("some description"))];
        helper.StopWhenNoWork().BuildAndStart();

        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 0, failed: 1);
        GetByFlowId().Single().Description.Should().Be("some description");
    }

    [Test]
    public void When_executing_step_throwing_special_FailCurrentStepException_and_add_step_Then_fail_current_step_and_add_ready_step()
    {
        var name = "test-throw-failstepexception-with-newStep";
        var nameNewStep = "test-throw-failstepexception-with-newStep-newstepname";

        helper.StepHandlers = [Handle(name, step => throw step.FailAsException(newSteps: new Step(nameNewStep)))];
        helper.Steps = [new Step(name) { FlowId = helper.FlowId }];
        helper.StopWhenNoWork().BuildAndStart();

        var steps = GetAllByFlowId();

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
                State = null
                StateFormat = null
                ActivationArgs = null
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
                State = null
                StateFormat = null
                ActivationArgs = null
                ExecutionCount = 1
                ExecutionDurationMillis = *
                ExecutionStartTime = now
                ExecutedBy = null
                CreatedTime = now
                CreatedByStepId = 0
                ScheduleTime = now
                CorrelationId = null
                Description = `Exception of type 'MicroWorkflow.FailCurrentStepException' was thrown.`
            }
        ]
    }
]");
        helper.Engine!.Data.FailSteps(new SearchModel(Name: nameNewStep));
    }

    [Test]
    public void When_executing_step_throwing_exception_Then_rerun_current_step_and_ensure_state_is_unchanged()
    {
        int? dbid = null;
        string name = helper.RndName;
        helper.StepHandlers = [Handle(name,
                step =>
                {
                    dbid = step.Id;
                    throw new Exception("exception message");
                })];
        helper.Steps = [new Step(name, "hey") { FlowId = helper.FlowId }];
        helper.StopWhenNoWork().BuildAndStart();

        helper.AssertTableCounts(helper.FlowId, ready: 1, done: 0, failed: 0);

        var persister = helper.Persister;
        var row = helper.Engine!.Data.SearchSteps(new SearchModel(Id: dbid!.Value), StepStatus.Ready).Single();
        row!.State.Should().Be("\"hey\"");
        row.FlowId.Should().Be(helper.FlowId);
        row.Name.Should().Be(name);
        helper.Engine.Data.FailSteps(new SearchModel(Name: name));
    }

    [Test]
    public void When_connecting_unsecurely_to_DB_Then_see_the_exception()
    {
        const string IllegalConnectionString = "Server=localhost;Database=adotest;Integrated Security=False;TrustServerCertificate=False";

        helper.StepHandlers = [Handle("any", step => step.Fail())];
        helper.ConnectionString = IllegalConnectionString;

        Action act = () => helper.StopWhenNoWork().BuildAndStart();

        act.Should()
            .Throw<SqlException>()
            .WithMessage("A connection was successfully established with the server, but then an error occurred during the login process.*");
    }

    [Test]
    public void When_step_is_failing_Then_it_is_marked_as_failed()
    {
        string name = helper.RndName;
        helper.StepHandlers = [(name, new GenericImplementation(step => step.Fail()))];
        helper.Steps = [new Step(name) { FlowId = helper.FlowId }];
        helper.StopWhenNoWork().BuildAndStart();

        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 0, failed: 1);
    }

    [Test]
    public void When_executing_step_for_the_first_time_Then_execution_count_is_1()
    {
        int? stepResult = null;
        var name = helper.RndName;
        helper.StepHandlers = [(name, new GenericImplementation(step =>
        {
            stepResult = step.ExecutionCount;
            return ExecutionResult.Done();
        }))];
        helper.Steps = [new Step(name)];
        helper.StopWhenNoWork().BuildAndStart();

        stepResult.Should().Be(1);
    }

    [Test]
    public void When_step_returns_rerun_Then_it_is_rerun()
    {
        string name = helper.RndName;
        string? stepResult = null;
        helper.StepHandlers = [Handle(name, step =>
        {
            int counter = helper.Formatter!.Deserialize<int>(step.State);
            stepResult = $"counter {counter} executionCount {step.ExecutionCount}";

            if (counter < 3)
                return ExecutionResult.Rerun(stateForRerun: counter + 1, scheduleTime: step.ScheduleTime);
            return ExecutionResult.Done();
        })];
        helper.Steps = [new Step(name) { InitialState = 1, FlowId = helper.FlowId }];
        helper.StopWhenNoWork().BuildAndStart();

        stepResult.Should().Be("counter 3 executionCount 3");

        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 1, failed: 0);
    }

    [Test]
    public void When_one_step_executes_as_done_with_a_new_step_Then_new_step_has_the_same_flowid_and_correlationId()
    {
        Step? executingStep = null;

        helper.StepHandlers = [
         ("check-flowid/a", new GenericImplementation(step => step.Done(new Step("check-flowid/b")))),
            ("check-flowid/b", new GenericImplementation(step =>
           {
               executingStep = step;
               return ExecutionResult.Done();
           }))];
        helper.Steps = [new Step
        {
            Name = "check-flowid/a",
            FlowId = helper.FlowId,
            CorrelationId = helper.CorrelationId,
        }];
        helper.StopWhenNoWork().BuildAndStart();

        executingStep!.FlowId.Should().Be(helper.FlowId);
        executingStep.CorrelationId.Should().Be(helper.CorrelationId);
        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 2, failed: 0);
    }

    [Test]
    public void When_a_step_creates_a_new_step_Then_new_step_may_change_correlationid()
    {
        string? stepResult = null;
        string oldId = guid();
        string newId = guid();

        helper.StepHandlers = [("check-correlationidchange/cookFood", new GenericImplementation(step =>
        {
            return step.Done()
                .With(new Step("check-correlationidchange/eat")
                {
                    CorrelationId = newId,
                });
        })),
            ("check-correlationidchange/eat", new GenericImplementation(step =>
        {
            stepResult = step.CorrelationId;
            return step.Done();
        }))];
        helper.Steps = [new Step()
        {
            Name = "check-correlationidchange/cookFood",
            CorrelationId = oldId
        }];

        helper.StopWhenNoWork().BuildAndStart();

        stepResult.Should().Be(newId);
    }


    [Test]
    public void When_a_step_creates_a_new_step_Then_new_step_may_change_correlationid2()
    {
        string? stepResult = null;
        string oldId = guid();
        string newId = guid();

        helper.StepHandlers = [
            Handle("check-correlationidchange/cookFood", step =>
        {
            return step.Done()
                .With(new Step("check-correlationidchange/eat")
                {
                    CorrelationId = newId,
                });
        }),
            Handle("check-correlationidchange/eat", step =>
            {
                stepResult = step.CorrelationId;
                return step.Done();
            })];
        helper.Steps = [new Step()
        {
            Name = "check-correlationidchange/cookFood",
            CorrelationId = oldId
        }];

        helper.StopWhenNoWork().BuildAndStart();

        stepResult.Should().Be(newId);
    }

    [Test]
    public void TwoSteps_flow_last_step_starting_in_the_future_so_test_terminate_before_its_executions()
    {
        string? stepResult = null;

        helper.StepHandlers = [
            Handle("check-future-step/cookFood", step =>
            {
                string food = "potatoes";
                stepResult = $"cooking {food}";
                return ExecutionResult.Done(
                    new Step("check-future-step/eat", food) { ScheduleTime = DateTime.Now.AddYears(30) });
            }),
            Handle("check-future-step/eat", step =>
            {
                var food = helper.Formatter!.Deserialize<string>(step.State);
                stepResult = $"eating {food}";
                return ExecutionResult.Done();
            })];
        helper.Steps = [new Step() { Name = "check-future-step/cookFood", FlowId = helper.FlowId }];
        helper.StopWhenNoWork().BuildAndStart();

        stepResult.Should().Be($"cooking potatoes");

        helper.AssertTableCounts(helper.FlowId, ready: 1, done: 1, failed: 0);
        helper.Engine!.Data.FailSteps(new SearchModel(Name: "check-future-step/eat"));
    }


    [Test]
    public void When_step_is_in_the_future_Then_it_wont_execute()
    {
        string? stepResult = null;
        const string name = nameof(When_step_is_in_the_future_Then_it_wont_execute);
        helper.StepHandlers = [Handle(name, step => { stepResult = step.FlowId; return step.Done(); })];
        helper.Steps = [new Step(name)
        {
            FlowId = helper.FlowId,
            ScheduleTime = DateTime.Now.AddYears(35)
        }];
        helper.StopWhenNoWork().BuildAndStart();

        helper.AssertTableCounts(helper.FlowId, ready: 1, done: 0, failed: 0);

        stepResult.Should().BeNull();
        helper.Engine!.Data.FailSteps(new SearchModel(Name: name));
    }

    [Test]
    public void When_step_is_in_the_future_Then_it_can_be_activated_to_execute_now()
    {
        string? stepResult = null;
        const string name = "When_step_is_in_the_future_Then_it_can_be_activated_to_execute_now";
        helper.StepHandlers = [Handle(name, step => { stepResult = step.FlowId; return step.Done(); })];
        helper.Steps = [new Step(name)
        {
            FlowId = helper.FlowId,
            ScheduleTime = DateTime.Now.AddYears(35)
        }];
        helper.StopWhenNoWork().BuildAndStart();
        helper.AssertTableCounts(helper.FlowId, ready: 1, done: 0, failed: 0);
        stepResult.Should().BeNull();

        // activate
        var id = GetByFlowId().Single().Id;
        var count = helper.Engine!.Data.ActivateStep(id, null);
        count.Should().Be(1);

        helper.Start();

        stepResult.Should().Be(helper.FlowId.ToString());
        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 1, failed: 0);
    }

    [Test]
    public void When_step_is_in_the_future_Then_it_can_be_activated_to_execute_now_with_args()
    {
        string? stepResult = null;
        string args = "1234";
        const string name = "When_step_is_in_the_future_Then_it_can_be_activated_to_execute_now_with_args";

        helper.StepHandlers = [Handle(name, step => { stepResult = step.ActivationArgs; return step.Done(); })];
        helper.Steps = [new Step(name)
        {
            FlowId = helper.FlowId,
            ScheduleTime = DateTime.Now.AddYears(35)
        }];
        helper.StopWhenNoWork().BuildAndStart();

        // activate
        var id = GetByFlowId().Single().Id;
        var count = helper.Engine!.Data.ActivateStep(id, args);
        count.Should().Be(1);
        helper.Start();

        stepResult.Should().Be(JsonConvert.SerializeObject(args));
        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 1, failed: 0);
    }

    [Test]
    public void TwoSteps_flow_with_last_step_undefined_stephandler__so_test_terminate()
    {
        string? stepResult = null;
        const string name = "undefined-next-step/cookFood";
        helper.StepHandlers = [Handle(name, step =>
        {
            stepResult = $"cooking potatoes";
            return step.Done(new Step("undefined-next-step/eat", "potatoes"));
        })];
        helper.Steps = [new Step(name) { FlowId = helper.FlowId }];
        helper.StopWhenNoWork().BuildAndStart();

        stepResult.Should().Be($"cooking potatoes");

        helper.AssertTableCounts(helper.FlowId, ready: 1, done: 1, failed: 0);
        GetAllByFlowId()[StepStatus.Ready].Single().State.Should().Be("\"potatoes\"");
        helper.Engine!.Data.FailSteps(new SearchModel(Name: "undefined-next-step/eat"));
    }

    [Test]
    public void When_a_step_creates_two_steps_Then_those_steps_can_be_synchronized_and_join_into_a_forth_merge_step()
    {
        string? stepResult = null;

        helper.StepHandlers = [Handle("v1/forkjoin/pay-for-all",
            step =>
            {
                (int count, Guid id, DateTime maxWait) = helper.Formatter!.Deserialize<(int, Guid, DateTime)>(step.State);
                var sales = GroceryBuyer.SalesDb.Where(x => x.id == id).ToArray();
                if (sales.Length != 2 && DateTime.Now <= maxWait)
                    return ExecutionResult.Rerun(scheduleTime: DateTime.Now.AddSeconds(0.2));

                stepResult = $"total: {sales.Sum(x => x.total)}";
                return ExecutionResult.Done();
            }),
            Handle("v1/forkjoin/drive-to-shop", step =>
            {
                stepResult = $"driving";
                var id = Guid.NewGuid();
                Step milk = new("v1/forkjoin/pick-milk", new BuyInstructions() { Item = "milk", Count = 1, PurchaseId = id });
                Step cookies = new("v1/forkjoin/pick-cookies", new BuyInstructions() { Item = "cookies", Count = 30, PurchaseId = id });
                Step pay = new("v1/forkjoin/pay-for-all", (count: 2, id, maxWait: DateTime.Now.AddSeconds(8)));
                return step.Done(milk, cookies, pay);
            }),
            ("v1/forkjoin/pick-milk", new GroceryBuyer()),
            ("v1/forkjoin/pick-cookies", new GroceryBuyer())];

        helper.Steps = [new Step("v1/forkjoin/drive-to-shop") { FlowId = helper.FlowId }];

        helper.StopWhenNoWork().BuildAndStart();

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
        internal static readonly List<(Guid id, string name, int total)> SalesDb = [];
        static readonly Dictionary<string, int> prices = new() { { "milk", 1 }, { "cookies", 2 } };

        public async Task<ExecutionResult> ExecuteAsync(Step step)
        {
            Debug.WriteLine("Picking up stuff");
            Thread.Sleep(100);

            var instruction = JsonConvert.DeserializeObject<BuyInstructions>(step.State!);

            lock (SalesDb)
            {
                SalesDb.Add((instruction!.PurchaseId, instruction.Item!, prices[instruction.Item!] * instruction.Count));
            }

            return await Task.FromResult(step.Done());
        }
    }
}