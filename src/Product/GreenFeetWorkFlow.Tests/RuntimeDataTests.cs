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
            FetchLevel = new SearchModel.FetchLevels()
            {
                IncludeDone = true,
                IncludeFail = true,
                IncludeReady = true,
            }
        });

        steps.Keys.Count.Should().Be(3);
    }

    [Test]
    public void When_searching_for_correlationdi_Then_success()
    {
        var engine = helper.CreateEngine();
        var step = new Step(AttributeRegistrationTests.StepA.Name)
        {
            CorrelationId = helper.RndName,
            ScheduleTime = DateTime.Now.AddSeconds(5)
        };
        engine.Runtime.Data.AddSteps(step);
        var steps = engine.Runtime.Data.SearchSteps(new SearchModel()
        {
            CorrelationId = helper.RndName,
            FetchLevel = new SearchModel.FetchLevels()
            {
                IncludeDone = true,
                IncludeFail = true,
                IncludeReady = true,
            }
        });

        steps[StepStatus.Ready].Single().CorrelationId.Should().Be(helper.RndName);
    }

    [Test]
    public async Task When_reexecuting_a_step_Then_execute_it()
    {
        Dictionary<int,int> results = new();
        
        var engine=helper.CreateEngine(("v1/fail-and-reactivate", GenericStepHandler.Create(_ => throw new FailCurrentStepException())));

        var stepState = 12345;
        var step = new Step("v1/fail-and-reactivate", stepState){ FlowId = helper.FlowId, CorrelationId = helper.CorrelationId};
        var id = engine.Runtime.Data.AddSteps(step).Single();
        await engine.StartAsync(true);

        var newId= engine.Runtime.Data
            .ReExecuteSteps(new SearchModel {Id = id, FetchLevel = new SearchModel.FetchLevels {IncludeFail = true}})
            .Single();

        var newStep = helper.Persister.GetStep(newId)!;
        newStep.Id.Should().BeGreaterThan(id);
        newStep.PersistedState.Should().Be(stepState.ToString());
        newStep.CorrelationId.Should().Be(step.CorrelationId);
        newStep.FlowId.Should().Be(step.FlowId);
        newStep.CreatedByStepId.Should().Be(step.Id);
        newStep.ScheduleTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
    }
    
}