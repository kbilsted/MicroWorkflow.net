using Microsoft.Data.SqlClient;
using static GreenFeetWorkflow.Tests.TestHelper;

namespace GreenFeetWorkflow.Tests;

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
    public void When_creating_two_identical_singleton_Then_fail()
    {
        var engine = helper.Build();

        var step = new Step(helper.RndName) { Singleton = true, };
        var step2 = new Step(helper.RndName) { Singleton = true, };

        Func<object> act = () => engine.Data.AddSteps([step, step2]);

        act.Should()
            .Throw<SqlException>()
            .WithMessage("Cannot insert duplicate key row*");
    }
}
