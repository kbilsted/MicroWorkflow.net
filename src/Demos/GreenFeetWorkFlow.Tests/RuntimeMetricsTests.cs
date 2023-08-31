namespace GreenFeetWorkflow.Tests;

public class RuntimeMetricsTests
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
        var steps = engine.Metrics.CountSteps();

        steps.Keys.Count.Should().Be(3);
    }
}
