using static GreenFeetWorkflow.Tests.TestHelper;

namespace GreenFeetWorkflow.Tests;

/// <summary>
/// test the runtime data api
/// </summary>
public class RuntimeDataTests
{
    TestHelper helper = new TestHelper();

    [SetUp]
    public void Setup()
    {
        helper = new TestHelper();
    }

    int? tearDownStep = null;
    [TearDown]
    public void Teardown()
    {
        if (tearDownStep == null)
            return;
        if (helper.Engine == null)
            helper.Build();
        helper.Engine!.Data.FailSteps(new SearchModel(Id: tearDownStep), null);
    }

    [Test]
    public void When_searching_with_no_parameters_Then_success()
    {
        var engine = helper.Build();
        var steps = engine.Data.SearchSteps(new SearchModel(), FetchLevels.ALL);

        steps.Keys.Count.Should().Be(3);
    }

    [Test]
    public void When_SearchSteps_Then_success()
    {
        var engine = helper.Build();

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
    public void When_reexecuting_a_step_Then_place_it_in_the_ready_queue()
    {
        const string name = "v1/fail-and-reactivate";
        Dictionary<int, int> results = new();
        var step = new Step(name) { FlowId = helper.FlowId, CorrelationId = helper.CorrelationId };
        var engine = helper
            .With(e =>
            {
                e.StepHandlers = [Handle(name, _ => throw new FailCurrentStepException("fail on purpose"))];
                e.Steps = [step];
            })
            .UseMax1Worker().StopWhenNoWork().BuildAndStart();

        int reExecutingId = engine.Data
           .ReExecuteSteps(new SearchModel(FlowId: helper.FlowId), FetchLevels.FAILED)
           .Single();
        tearDownStep = reExecutingId;

        var newStep = engine.Data.SearchSteps(new SearchModel(Id: reExecutingId), StepStatus.Ready).Single();
        newStep.CorrelationId.Should().Be(step.CorrelationId);
        newStep.FlowId.Should().Be(step.FlowId);
        newStep.CreatedByStepId.Should().Be(step.Id);
        newStep.ScheduleTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(2));
        newStep.ExecutionCount.Should().Be(0);
    }


        newStep.CorrelationId.Should().Be(step.CorrelationId);
        newStep.FlowId.Should().Be(step.FlowId);
        newStep.CreatedByStepId.Should().Be(step.Id);
        newStep.ScheduleTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
    }
}