namespace GreenFeetWorkflow.Tests;

/// <summary>
/// test the runtime api
/// </summary>
public class RuntimeDataTests
{
    TestHelper helper = new TestHelper();

    [SetUp]
    public void Setup()
    {
        helper = new TestHelper();
    }

    [Test]
    public void When_searching_with_no_parameters_Then_success()
    {
        var engine = helper.CreateEngine();
        var steps = engine.Runtime.Data.SearchSteps(new SearchModel()
        {
            FetchLevel = new (true, true, true)
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
        };
        var id = engine.Runtime.Data.AddStep(step, null);

        FetchLevels fetchLevels = new (true, true, true);
        var steps = engine.Runtime.Data.SearchSteps(new SearchModel()
        {
            Id = id,
            FetchLevel = fetchLevels
        });
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Runtime.Data.SearchSteps(new SearchModel()
        {
            CorrelationId = step.CorrelationId,
            FetchLevel = fetchLevels
        });
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Runtime.Data.SearchSteps(new SearchModel()
        {
            Name = step.Name,
            FetchLevel = fetchLevels
        });
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Runtime.Data.SearchSteps(new SearchModel()
        {
            FlowId = step.FlowId,
            FetchLevel = fetchLevels
        });
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Runtime.Data.SearchSteps(new SearchModel()
        {
            SearchKey = step.SearchKey,
            FetchLevel = fetchLevels
        });
        steps[StepStatus.Ready].Single().Id.Should().Be(id);


        // combined search
        steps = engine.Runtime.Data.SearchSteps(new SearchModel()
        {
            Id = id,
            CorrelationId = step.CorrelationId,
            Name = step.Name,
            FlowId = step.FlowId,
            SearchKey = step.SearchKey,
            FetchLevel = fetchLevels
        });
        steps[StepStatus.Ready].Single().Id.Should().Be(id);
    }

    [Test]
    public async Task When_reexecuting_a_step_Then_execute_it()
    {
        Dictionary<int, int> results = new();

        var engine = helper.CreateEngine(("v1/fail-and-reactivate", GenericStepHandler.Create(_ => throw new FailCurrentStepException())));

        var stepState = 12345;
        var step = new Step("v1/fail-and-reactivate", stepState) { FlowId = helper.FlowId, CorrelationId = helper.CorrelationId };
        var id = engine.Runtime.Data.AddStep(step);
        await engine.StartAsync(true);

        var newId = engine.Runtime.Data
            .ReExecuteSteps(new SearchModel { Id = id, FetchLevel = new(Fail: true) })
            .Single();

        var newStep = helper.Persister.Go(p =>
p.SearchSteps(new SearchModel() { Id = newId, FetchLevel = new(Ready: true) })
[StepStatus.Ready]
.Single());
        newStep.Id.Should().BeGreaterThan(id);
        newStep.PersistedState.Should().Be(stepState.ToString());
        newStep.CorrelationId.Should().Be(step.CorrelationId);
        newStep.FlowId.Should().Be(step.FlowId);
        newStep.CreatedByStepId.Should().Be(step.Id);
        newStep.ScheduleTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
    }
}