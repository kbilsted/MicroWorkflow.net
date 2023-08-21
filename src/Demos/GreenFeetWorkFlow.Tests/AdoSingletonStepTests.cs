using FluentAssertions;
using Microsoft.Data.SqlClient;

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

        Step step = new Step(helper.RndName)
        {
            Singleton = true,
            FlowId = helper.FlowId
        };
        helper.CreateAndRunEngine(step,
            ((string, IStepImplementation))(helper.RndName, new GenericStepHandler(step =>
            {
                stepResult = $"hello";
                return ExecutionResult.Done();
            })));

        stepResult.Should().Be("hello");

        helper.AssertTableCounts(helper.FlowId, ready: 0, done: 1, failed: 0);
    }

    [Test]
    public void When_creating_two_identical_singleton_Then_fail()
    {
        var engine = helper.CreateEngine();

        var step = new Step(helper.RndName) { Singleton = true, };
        var step2 = new Step(helper.RndName) { Singleton = true, };

        Action act = () => engine.Runtime.Data.AddSteps(new[] { step, step2 });

        act.Should()
            .Throw<SqlException>()
            .WithMessage("Cannot insert duplicate key row*");
    }
}
