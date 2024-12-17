namespace MicroWorkflow;

/// <summary>
/// not real tests just a place for code used in the readme.md
/// </summary>
class DocumentationTests
{
    [StepName(Name)]
    class SendMemberEnrollmentToAcmeCompany(HttpClient client) : IStepImplementation
    {
        public const string Name = "v1/send-member-enrollment-to-acme-company";

        public async Task<ExecutionResult> ExecuteAsync(Step step)
        {
            var result = await client.PostAsync("...", null);
            result.EnsureSuccessStatusCode();
            return step.Done();
        }
    }

    [StepName(Name)]
    class SendMemberEnrollmentToAcmeCompanyLimitedRetry(HttpClient client) : IStepImplementation
    {
        public const string Name = "v1/send-member-enrollment-to-acme-company-limited-retry";

        public async Task<ExecutionResult> ExecuteAsync(Step step)
        {
            if (step.ExecutionCount >= 5)
                return step.Fail("too many retries");

            var result = await client.PostAsync("...", null);

            switch ((int)result.StatusCode)
            {
                case >= 200 and < 300:
                    return step.Done();

                case >= 400 and < 500:
                    return step.Fail("Wrong payload " + result.ToString());

                case >= 500:
                    return step.Rerun(description: $"Upstream error {result}");

                default: throw new NotImplementedException();
            }
        }
    }



    [StepName(Name)]
    class SendMemberEnrollmentToAcmeCompanyLimitedTimewindow() : IStepImplementation
    {
        public const string Name = "v1/send-member-enrollment-to-acme-company-limited-from-0700-to-2000";

        public async Task<ExecutionResult> ExecuteAsync(Step step)
        {
            // ensure window of 0700 - 2000
            var now = DateTime.Now;
            if (now.Hour < 7)
                return step.Rerun(scheduleTime: now.Date.AddHours(7));

            if (now.Hour >= 20)
                return step.Rerun(scheduleTime: now.Date.AddDays(1).AddHours(7));

            // ... stuff

            return await Task.FromResult(step.Done());
        }
    }


    [StepName(Name)]
    class ScheduledFetchDataOnceAnHour() : IStepImplementation
    {
        public const string Name = "v1/fetch-data-from-acme-once-an-hour";

        public async Task<ExecutionResult> ExecuteAsync(Step step)
        {
            if (!step.Singleton)
                throw new FailCurrentStepException("Must be a singleton step!");

            // ... fetch data

            return await Task.FromResult(step.Rerun(scheduleTime: step.ExecutionStartTime!.Value.AddHours(1)));
        }
    }


    class AddScheduler
    {
        public void ScheduleDataFetch(WorkflowEngine engine)
        {
            var step = new Step(ScheduledFetchDataOnceAnHour.Name)
            {
                Singleton = true,
                ScheduleTime = DateTime.Now.Date.AddHours(DateTime.Now.Hour)
            };

            engine.Data.AddStepIfNotExists(step, new SearchModel(Name: ScheduledFetchDataOnceAnHour.Name));
        }
    }
}
