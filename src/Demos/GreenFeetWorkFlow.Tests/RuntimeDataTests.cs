using NUnit.Framework.Internal;

namespace GreenFeetWorkflow.Tests;

/// <summary>
/// test the runtime api
/// </summary>
public class RuntimeDataTests
{
    TestHelper helper = new TestHelper();

    private readonly WorkflowConfiguration cfg = new WorkflowConfiguration(
        new WorkerConfig() { StopWhenNoWork = true },
        NumberOfWorkers: 1);

    [SetUp]
    public void Setup()
    {
        helper = new TestHelper();
    }

    [Test]
    public void When_searching_with_no_parameters_Then_success()
    {
        var engine = helper.CreateEngine();
        var steps = engine.Data.SearchSteps(new SearchModel(), FetchLevels.ALL);

        steps.Keys.Count.Should().Be(3);
    }

    [Test]
    public void When_SearchSteps_Then_success()
    {
        var engine = helper.CreateEngine();
        var step = new Step(helper.RndName)
        {
            FlowId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            SearchKey = Guid.NewGuid().ToString(),
            Description = Guid.NewGuid().ToString(),
        };
        var id = engine.Data.AddStep(step, null);

        FetchLevels fetchLevels = FetchLevels.ALL;
        var steps = engine.Data.SearchSteps(new SearchModel()
        {
            Id = id,
        }, fetchLevels);
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Data.SearchSteps(new SearchModel()
        {
            CorrelationId = step.CorrelationId,
        }, fetchLevels);
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Data.SearchSteps(new SearchModel()
        {
            Name = step.Name,
        }, fetchLevels);
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Data.SearchSteps(new SearchModel()
        {
            FlowId = step.FlowId,
        }, fetchLevels);
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Data.SearchSteps(new SearchModel()
        {
            SearchKey = step.SearchKey,
        }, fetchLevels);
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Data.SearchSteps(new SearchModel()
        {
            Description = step.Description,
        }, fetchLevels);
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        // combined search
        steps = engine.Data.SearchSteps(new SearchModel()
        {
            Id = id,
            CorrelationId = step.CorrelationId,
            Name = step.Name,
            FlowId = step.FlowId,
            SearchKey = step.SearchKey,
        }, fetchLevels);
        steps[StepStatus.Ready].Single().Id.Should().Be(id);
    }

    [Test]
    public async Task When_reexecuting_a_step_Then_execute_it()
    {
        Dictionary<int, int> results = new();

        var engine = helper.CreateEngine(("v1/fail-and-reactivate", GenericImplementation.Create(_ => throw new FailCurrentStepException("fail on purpose"))));

        var stepState = 12345;
        var step = new Step("v1/fail-and-reactivate", stepState) { FlowId = helper.FlowId, CorrelationId = helper.CorrelationId };
        var id = engine.Data.AddStep(step);
        await engine.StartAsSingleWorker(cfg);

        var newId = engine.Data
            .ReExecuteSteps(new SearchModel(Id: id))
            .Single();

        var persister = helper.Persister;

        var newStep = persister.InTransaction(() => persister.SearchSteps(new SearchModel(Id: newId), StepStatus.Ready).Single());
        newStep.Id.Should().BeGreaterThan(id);
        newStep.State.Should().Be(stepState.ToString());
        newStep.CorrelationId.Should().Be(step.CorrelationId);
        newStep.FlowId.Should().Be(step.FlowId);
        newStep.CreatedByStepId.Should().Be(step.Id);
        newStep.ScheduleTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
    }
}