using Microsoft.Data.SqlClient;
using static MicroWorkflow.TestHelper;

namespace MicroWorkflow;

public class AdoSingletonStepTests
{
    TestHelper helper = new TestHelper();

    [SetUp]
    public void Setup()
    {
        helper = new TestHelper();
    }

    [Test]
    public void When_creating_a_singleton_Then_it_is_created()
    {
        string? stepResult = null;
        helper.Steps = [new Step(helper.RndName)
        {
            Singleton = true,
            FlowId = helper.FlowId
        }];
        helper.StepHandlers = [Handle(helper.RndName, step => { stepResult = $"hello"; return ExecutionResult.Done(); })];
        helper.StopWhenNoWork().BuildAndStart();

        stepResult.Should().Be("hello");
        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 1, failed: 0);
    }

    [Test]
    public void When_adding_two_identical_singleton_steps_simultaniously_Then_fail()
    {
        var engine = helper.Build();

        var step = new Step(helper.RndName) { Singleton = true, };
        var step2 = new Step(helper.RndName) { Singleton = true, };

        Func<object> act = () => engine.Data.AddSteps([step, step2]);

        act.Should()
            .Throw<SqlException>()
            .WithMessage("Cannot insert duplicate key row*");
    }

    [Test]
    public void When_adding_two_identical_singleton_steps_Then_fail_on_last_insert()
    {
        var engine = helper.Build();
        var step = new Step(helper.RndName) { Singleton = true, };
        engine.Data.AddStep(step);

        var step2 = new Step(helper.RndName) { Singleton = true, };
        Func<object> act = () => engine.Data.AddStep(step2);

        act.Should()
            .Throw<SqlException>()
            .WithMessage("Cannot insert duplicate key row*");
    }

    [Test]
    public void When_AddStepIfNotExists_two_identical_singleton_steps_Then_insert_first_and_return_null_on_duplicate()
    {
        var engine = helper.Build();
        
        var step = new Step(helper.RndName) { Singleton = true };
        SearchModel searchModel = new SearchModel(Name: step.Name);
        engine.Data.AddStepIfNotExists(step, searchModel)
            .Should()
            .HaveValue();

        var step2 = new Step(helper.RndName) { Singleton = true };
        engine.Data.AddStepIfNotExists(step2, searchModel)
            .Should()
            .BeNull();
    }
}
