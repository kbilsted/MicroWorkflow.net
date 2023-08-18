using FluentAssertions;

namespace GreenFeetWorkflow.Tests;

public class EngineTests
{
    TestHelper helper = new TestHelper();

    [SetUp]
    public void Setup()
    {
        helper = new TestHelper();
    }

    [Test]
    public void When_adding_an_event_Then_an_id_PK_is_returned()
    {
        helper.CreateEngine();
        Step step = new Step(helper.RndName) { ScheduleTime = DateTime.Now.AddMonths(1) };
        var id = helper.Engine!.Runtime.Data.AddSteps(step).Single();

        SearchModel model = new SearchModel() 
        { 
            Id = id, 
            FetchLevel = new SearchModel.FetchLevels() { IncludeReady = true } 
        };
        var result = helper.Engine.Runtime.Data.SearchSteps(model);

        result[StepStatus.Ready].Single().Name.Should().Be(helper.RndName);
    }
}