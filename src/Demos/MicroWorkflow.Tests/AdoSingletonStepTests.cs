using Microsoft.Data.SqlClient;
using static MicroWorkflow.TestHelper;

namespace MicroWorkflow;

public class AdoSingletonStepTests
{
    TestHelper helper = new();

    [SetUp]
    public void Setup()
    {
        helper = new TestHelper();
    }

    [Test]
    public void When_creating_a_singleton_Then_it_is_created()
    {
        string? stepResult = null;
        bool? stepResultIsSingleton = null;
        var name = helper.RndName;
        helper.Steps = [new Step(name)
        {
            Singleton = true,
            FlowId = helper.FlowId
        }];
        helper.StepHandlers = [Handle(name, step => { 
            stepResult = $"hello";
            stepResultIsSingleton = step.Singleton;
            return step.Done(); 
        })];
        helper.StopWhenNoWork().BuildAndStart();

        stepResult.Should().Be("hello");
        stepResultIsSingleton.Should().BeTrue();
        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 1, failed: 0);
    }

    [Test]
    public void When_adding_two_identical_singleton_steps_simultaniously_Then_fail()
    {
        var engine = helper.Build();
        var name = helper.RndName;
        var step = new Step(name) { Singleton = true, };
        var step2 = new Step(name) { Singleton = true, };

        Func<object> act = () => engine.Data.AddSteps([step, step2]);

        act.Should()
            .Throw<SqlException>()
            .WithMessage("Cannot insert duplicate key row*");
    }

    [Test]
    public void When_adding_two_identical_singleton_steps_Then_fail_on_last_insert()
    {
        var engine = helper.Build();
        var name = helper.RndName;
        var step = new Step(name) { Singleton = true, };
        engine.Data.AddStep(step);

        var step2 = new Step(name) { Singleton = true, };
        Func<object> act = () => engine.Data.AddStep(step2);

        act.Should()
            .Throw<SqlException>()
            .WithMessage("Cannot insert duplicate key row*");
    }

    [Test]
    public void When_AddStepIfNotExists_two_identical_singleton_steps_Then_insert_first_and_return_null_on_duplicate()
    {
        var engine = helper.Build();
        var name = helper.RndName;
        var step = new Step(name) { Singleton = true };
        SearchModel searchModel = new(Name: step.Name);
        engine.Data.AddStepIfNotExists(step, searchModel)
            .Should()
            .HaveValue();

        var step2 = new Step(name) { Singleton = true };
        engine.Data.AddStepIfNotExists(step2, searchModel)
            .Should()
            .BeNull();
    }
}
