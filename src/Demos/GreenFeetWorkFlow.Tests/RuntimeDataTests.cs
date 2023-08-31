using NUnit.Framework.Internal;

namespace GreenFeetWorkflow.Tests;

/// <summary>
/// test the runtime api
/// </summary>
public class RuntimeDataTests
{
    TestHelper helper = new TestHelper();

    private readonly WfRuntimeConfiguration cfg = new WfRuntimeConfiguration(
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
        var steps = engine.Runtime.Data.SearchSteps(new SearchModel(FetchLevel: new(true, true, true))
        {
        });

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
        var id = engine.Runtime.Data.AddStep(step, null);

        FetchLevels fetchLevels = FetchLevels.ALL;
        var steps = engine.Runtime.Data.SearchSteps(new SearchModel(fetchLevels)
        {
            Id = id,
        });
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Runtime.Data.SearchSteps(new SearchModel(fetchLevels)
        {
            CorrelationId = step.CorrelationId,
        });
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Runtime.Data.SearchSteps(new SearchModel(fetchLevels)
        {
            Name = step.Name,
        });
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Runtime.Data.SearchSteps(new SearchModel(fetchLevels)
        {
            FlowId = step.FlowId,
        });
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Runtime.Data.SearchSteps(new SearchModel(fetchLevels)
        {
            SearchKey = step.SearchKey,
        });
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Runtime.Data.SearchSteps(new SearchModel(fetchLevels)
        {
            Description = step.Description,
        });
        steps[StepStatus.Ready].Single().Id.Should().Be(id);


        // combined search
        steps = engine.Runtime.Data.SearchSteps(new SearchModel(fetchLevels)
        {
            Id = id,
            CorrelationId = step.CorrelationId,
            Name = step.Name,
            FlowId = step.FlowId,
            SearchKey = step.SearchKey,
        });
        steps[StepStatus.Ready].Single().Id.Should().Be(id);
    }

    [Test]
    public async Task When_reexecuting_a_step_Then_execute_it()
    {
        Dictionary<int, int> results = new();

        var engine = helper.CreateEngine(("v1/fail-and-reactivate", GenericImplementation.Create(_ => throw new FailCurrentStepException("fail on purpose"))));

        var stepState = 12345;
        var step = new Step("v1/fail-and-reactivate", stepState) { FlowId = helper.FlowId, CorrelationId = helper.CorrelationId };
        var id = engine.Runtime.Data.AddStep(step);
        await engine.StartAsSingleWorker(cfg);

        var newId = engine.Runtime.Data
            .ReExecuteSteps(new SearchModel(Id: id, FetchLevel: new(Fail: true)))
            .Single();

        var persister = helper.Persister;

        var newStep = persister.InTransaction(() =>
persister.SearchSteps(new SearchModel(FetchLevel: FetchLevels.READY, Id: newId))
[StepStatus.Ready]
.Single());
        newStep.Id.Should().BeGreaterThan(id);
        newStep.State.Should().Be(stepState.ToString());
        newStep.CorrelationId.Should().Be(step.CorrelationId);
        newStep.FlowId.Should().Be(step.FlowId);
        newStep.CreatedByStepId.Should().Be(step.Id);
        newStep.ScheduleTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
    }
}