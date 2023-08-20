using FluentAssertions;
using Microsoft.Data.SqlClient;

namespace GreenFeetWorkflow.Tests;

public class AdoSingletonStepTests
{
    [Test]
    public void When_creating_a_singleton_Then_it_is_created()
    {
        var helper = new TestHelper();
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

        helper.Persister.CountTables(helper.FlowId).Should().BeEquivalentTo(new Dictionary<StepStatus, int>{
            { StepStatus.Ready, 0},
            { StepStatus.Done, 1},
            { StepStatus.Failed, 0},
        });
    }

    [Test]
    public void When_creating_two_identical_singleton_Then_fail()
    {
        var helper = new TestHelper();
        var engine = helper.CreateEngine();

        var step = new Step(helper.RndName) { Singleton = true, };
        var step2 = new Step(helper.RndName) { Singleton = true, };

        Action act = () => engine.Runtime.Data.AddSteps(step, step2);

        act.Should()
            .Throw<SqlException>()
            .WithMessage("Cannot insert duplicate key row*");
    }
}
