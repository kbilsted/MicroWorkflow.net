﻿using static MicroWorkflow.TestHelper;

namespace MicroWorkflow;

/// <summary>
/// test the runtime data api
/// </summary>
public class RuntimeDataTests
{
    TestHelper helper = new();

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
    public void When_SearchSteps_for_nonmatch_Then_return_empty()
    {
        var engine = helper.Build();

        var step = new Step(helper.RndName)
        {
            FlowId = guid(),
            CorrelationId = guid(),
            SearchKey = guid(),
            Description = guid(),
            ExecutedBy = guid(),
            Singleton = false
        };
        var now = DateTime.Now;
        var id = tearDownStep = engine.Data.AddStep(step, null);
        FetchLevels fetchLevels = FetchLevels.ALL;

        var steps = engine.Data.SearchSteps(new SearchModel()
        {
            Name = step.Name,
            Singleton = true,
        }, fetchLevels);
        steps[StepStatus.Ready].Should().BeEmpty();

        steps = engine.Data.SearchSteps(new SearchModel(ExecutedBy: "no-match"), fetchLevels);
        steps[StepStatus.Ready].Should().BeEmpty();

        steps = engine.Data.SearchSteps(new SearchModel(Description: "no-match"), fetchLevels);
        steps[StepStatus.Ready].Should().BeEmpty();

        steps = engine.Data.SearchSteps(new SearchModel(CorrelationId: "no-match"), fetchLevels);
        steps[StepStatus.Ready].Should().BeEmpty();

        steps = engine.Data.SearchSteps(new SearchModel(SearchKey: "no-match"), fetchLevels);
        steps[StepStatus.Ready].Should().BeEmpty();

        steps = engine.Data.SearchSteps(new SearchModel(FlowId: "no-match"), fetchLevels);
        steps[StepStatus.Ready].Should().BeEmpty();

        steps = engine.Data.SearchSteps(new SearchModel(Id: id, CreatedTimeUpto: now), fetchLevels);
        steps[StepStatus.Ready].Should().BeEmpty();

        steps = engine.Data.SearchSteps(new SearchModel(Id: id, CreatedTimeFrom: now.AddSeconds(3)), fetchLevels);
        steps[StepStatus.Ready].Should().BeEmpty();
    }

    [Test]
    public void When_SearchSteps_Then_return_data()
    {
        var engine = helper.Build();
        var step = new Step(helper.RndName)
        {
            FlowId = guid(),
            CorrelationId = guid(),
            SearchKey = guid(),
            Description = guid(),
            ExecutedBy = guid(),
            Singleton = false
        };

        var now = DateTime.Now;
        var id = tearDownStep = engine.Data.AddStep(step, null);

        FetchLevels fetchLevels = FetchLevels.ALL;
        var steps = engine.Data.SearchSteps(new SearchModel(Id: id), fetchLevels);
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Data.SearchSteps(new SearchModel(CorrelationId: step.CorrelationId), fetchLevels);
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Data.SearchSteps(new SearchModel(Name: step.Name), fetchLevels);
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Data.SearchSteps(new SearchModel(FlowId: step.FlowId), fetchLevels);
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Data.SearchSteps(new SearchModel(SearchKey: step.SearchKey), fetchLevels);
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Data.SearchSteps(new SearchModel(Description: step.Description), fetchLevels);
        steps[StepStatus.Ready].Single().Id.Should().Be(id);

        steps = engine.Data.SearchSteps(new SearchModel(CreatedTimeFrom: now.AddSeconds(-3)), fetchLevels);
        steps[StepStatus.Ready].Should().Contain(x => x.Id == id);

        steps = engine.Data.SearchSteps(new SearchModel(CreatedTimeUpto: now.AddSeconds(3)), fetchLevels);
        steps[StepStatus.Ready].Should().Contain(x => x.Id == id);

        steps = engine.Data.SearchSteps(new SearchModel(ExecutedBy: step.ExecutedBy), fetchLevels);
        steps[StepStatus.Ready].Should().BeEmpty(because: "step is not executed yet");

        // combined search
        steps = engine.Data.SearchSteps(new SearchModel()
        {
            Id = id,
            CorrelationId = step.CorrelationId,
            Name = step.Name,
            FlowId = step.FlowId,
            SearchKey = step.SearchKey,
            Singleton = step.Singleton,
            CreatedTimeFrom = now.AddSeconds(-3),
            CreatedTimeUpto = now.AddSeconds(3),
            // can't use ExecutedBy - step is not executed yet
        }, fetchLevels);
        steps[StepStatus.Ready].Single().Id.Should().Be(id);
    }

    [Test]
    public void When_reexecuting_a_step_Then_place_it_in_the_ready_queue()
    {
        const string name = "v1/fail-and-reactivate";
        Dictionary<int, int> results = [];
        var step = new Step(name) { FlowId = helper.FlowId, CorrelationId = helper.CorrelationId };
        var engine = helper
            .With(e =>
            {
                e.StepHandlers = [Handle(name, _ => throw new FailCurrentStepException("fail on purpose"))];
                e.Steps = [step];
            })
            .StopWhenNoWork().BuildAndStart();

        int reExecutingId = engine.Data
           .ReExecuteSteps(new SearchModel(FlowId: helper.FlowId), FetchLevels.FAILED)
           .Single();
        tearDownStep = reExecutingId;

        var newStep = engine.Data.SearchSteps(new SearchModel(Id: reExecutingId), StepStatus.Ready).Single();
        newStep.CorrelationId.Should().Be(step.CorrelationId);
        newStep.FlowId.Should().Be(step.FlowId);
        newStep.CreatedByStepId.Should().Be(step.Id);
        newStep.ScheduleTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(2));
        newStep.ScheduleTime.Millisecond.Should().Be(0);
        newStep.ExecutionCount.Should().Be(0);
    }

    [Test]
    public void When_adding_a_step_Then_place_it_in_the_ready_queue()
    {
        var engine = helper.Build();
        var step = new Step(helper.RndName) { FlowId = helper.FlowId, CorrelationId = helper.CorrelationId };
        var id = engine.Data.AddStep(step);
        tearDownStep = id;

        var newStep = engine.Data.SearchSteps(new SearchModel(Id: id), StepStatus.Ready).Single();
        newStep.CorrelationId.Should().Be(step.CorrelationId);
        newStep.FlowId.Should().Be(step.FlowId);
        newStep.CreatedByStepId.Should().Be(step.Id);
        newStep.ScheduleTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        newStep.ScheduleTime.Millisecond.Should().Be(0);
        newStep.ExecutionCount.Should().Be(0);
    }
}
