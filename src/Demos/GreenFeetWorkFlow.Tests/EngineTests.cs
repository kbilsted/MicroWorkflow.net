
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
    public async Task When_adding_an_event_Then_an_id_PK_is_returned()
    {
        Step step = new Step(helper.RndName) { ScheduleTime = DateTime.Now.AddMonths(1) };

        helper.Build();
        var id = helper.Engine!.Data.AddStep(step);

        var result = helper.Engine.Data.SearchSteps(new(Id: id), StepStatus.Ready);

        result.Single().Name.Should().Be(helper.RndName);
    }

    [Test]
    public void RetryTimeout_what_is_suitable()
    {
        int max = 10;
        Console.WriteLine("i^2");
        for (int i = 1; i <= max; i++)
        {
            var t = TimeSpan.FromSeconds(i * i);
            Console.WriteLine($"{i}:: {t}   ");
        }

        Console.WriteLine("2 * i^2");
        for (int i = 1; i <= max; i++)
        {
            var t = TimeSpan.FromSeconds(2 * i * i);
            Console.WriteLine($"{i}:: {t}   ");
        }

        Console.WriteLine("i^3");
        for (int i = 1; i <= max; i++)
        {
            var t = TimeSpan.FromSeconds(i * i * i);
            Console.WriteLine($"{i}:: {t}   ");
        }

        Console.WriteLine("i^4");
        for (int i = 1; i <= max; i++)
        {
            var t = TimeSpan.FromSeconds(i * i * i * i);
            Console.WriteLine($"{i}:: {t}   ");
        }
    }

    [Test]
    public async Task When_taskrun_a_sync_task_that_throws_an_exception_Then_we_continue_and_pick_it_up()
    {
        var str = "some message";
        bool continued = false;
        Task t = Task
            .Run(() => { throw new Exception(str); })
            .ContinueWith(x =>
            {
                continued = true;
                x.Exception.GetType().Should().Be<AggregateException>();
                x.Exception.InnerException.Message.Should().Be(str);
            });
        await t;
        continued.Should().BeTrue();
    }


    [Test]
    public async Task When_taskrun_an_async_task_that_throws_an_exception_Then_we_continue_and_pick_it_up()
    {
        var str = "some message";
        bool continued = false;

        Task t = Task
        .Run(async () => { throw new Exception(str); })
            .ContinueWith(x =>
            {
                continued = true;
                x.IsFaulted.Should().BeTrue();
                x.Exception!.GetType().Should().Be<AggregateException>();
                x.Exception!.InnerException!.Message.Should().Be(str);
            });
        await t;
        continued.Should().BeTrue();
    }

    [Test]
    public async Task When_taskrun_an_async_task_with_await_that_throws_an_exception_Then_we_continue_and_pick_it_up()
    {
        bool continued = false;

        Task t = Task
        .Run(async () => { await SomeAsyncMethodThrowingException(); })
            .ContinueWith(x =>
            {
                continued = true;
                x.IsFaulted.Should().BeTrue();
                x.Exception!.GetType().Should().Be<AggregateException>();
                x.Exception!.InnerException!.Message.Should().Be("foo");
            });
        await t;
        continued.Should().BeTrue();
    }

    [Test]
    public async Task When_taskfactory_a_sync_task_that_throws_an_exception_Then_we_continue_and_pick_it_up()
    {
        var str = "some message";
        bool continued = false;

        Task t = Task.Factory.StartNew(
            () => { if (str.Length > 1) throw new Exception(str); },
                  helper.cts.Token,
                  TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                  TaskScheduler.Default)
            .ContinueWith(x =>
            {
                continued = true;
                x.IsFaulted.Should().BeTrue();
                x.Exception!.GetType().Should().Be<AggregateException>();
                x.Exception!.InnerException!.Message.Should().Be(str);
            });
        await t;
        continued.Should().BeTrue();
    }


    [Test]
    public async Task When_taskfactory_an_async_task_from_that_throws_an_exception_Then_WE_DO_NOT_CATCH_THE_ERROR()
    {
        bool continued = false;

        Task t = Task.Factory.StartNew(
            async () => { if ("some message".Length > 1) throw new Exception("some message"); return await Task.FromResult(1); },
                  helper.cts.Token,
                  TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                  TaskScheduler.Default)
            .ContinueWith( x =>
            {
                continued = false;
                x.IsFaulted.Should().BeFalse(); // notice no exception!
                x.Exception.Should().BeNull();
            });

        await t;
        continued.Should().BeFalse();  // notice await is not awaiting
    }

    static async Task SomeAsyncMethodThrowingException() => throw new Exception("foo");

    /// <summary>
    /// this is how we start tasks in the engine
    /// </summary>
    [Test]
    public async Task When_taskfactory_running_an_async_method_sync_that_throws_an_exception_Then_WE_DO_NOT_CATCH_THE_ERROR()
    {
        bool continued = false;

        Task t = Task.Factory.StartNew(SomeAsyncMethodThrowingException,
                  helper.cts.Token,
                  TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                  TaskScheduler.Default)
            .ContinueWith(x =>
            {
                continued = true;
                x.IsFaulted.Should().BeFalse(); // notice no exception!
                x.Exception.Should().BeNull();
            });
        await t;
        continued.Should().BeTrue();
    }

}
