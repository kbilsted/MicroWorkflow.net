namespace MicroWorkflow;

public class AttributeRegistrationTests
{
    private static readonly Dictionary<string, string?> StepResult = [];

    [Test]
    public void When_using_stepnameattribute_Then_stepimplementation_is_registered()
    {
        var testhelper = new TestHelper();
        testhelper.Steps = [new Step(StepA.Name) { FlowId = testhelper.FlowId }];
        testhelper.UseMax1Worker().StopWhenNoWork().BuildAndStart();

        StepResult[testhelper.FlowId].Should().Be(testhelper.FlowId);
        testhelper.AssertTableCounts(testhelper.FlowId, ready: 0, done: 2, failed: 0);
    }

    [StepName(Name)]
    internal class StepA : IStepImplementation
    {
        public const string Name = "v1/AttributeRegistrationTest/a";

        public async Task<ExecutionResult> ExecuteAsync(Step step)
        {
            return await Task.FromResult(step.Done(new Step(StepB.Name)));
        }
    }

    [StepName(Name)]
    internal class StepB : IStepImplementation
    {
        public const string Name = "v1/AttributeRegistrationTest/b";

        public async Task<ExecutionResult> ExecuteAsync(Step step)
        {
            StepResult.Add(step.FlowId!, step.FlowId);
            return await Task.FromResult(step.Done());
        }
    }
}

