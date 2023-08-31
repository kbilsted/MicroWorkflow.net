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
        var id = helper.Engine!.Runtime.Data.AddStep(step);

        SearchModel model = new(FetchLevel: new(Ready: true), Id: id);

        var result = helper.Engine.Runtime.Data.SearchSteps(model);

        result[StepStatus.Ready].Single().Name.Should().Be(helper.RndName);
    }


    [Test]
    public void RetryTimeout_what_is_suitable()
    {
        Console.WriteLine("i^2");
        for (int i = 1; i <= 20; i++)
        {
            var t = TimeSpan.FromSeconds(i * i);
            Console.WriteLine($"{i}:: {t}   ");
        }

        Console.WriteLine("2 * i^2");
        for (int i = 1; i <= 20; i++)
        {
            var t = TimeSpan.FromSeconds(2 * i * i);
            Console.WriteLine($"{i}:: {t}   ");
        }

        Console.WriteLine("i^3");
        for (int i = 1; i <= 20; i++)
        {
            var t = TimeSpan.FromSeconds(i * i * i);
            Console.WriteLine($"{i}:: {t}   ");
        }
    }
}