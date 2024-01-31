# GreenFeetWorkFlow .Net
<!--start-->
[![Stats](https://img.shields.io/badge/Code_lines-2,5_K-ff69b4.svg)]()
[![Stats](https://img.shields.io/badge/Test_lines-0-69ffb4.svg)]()
[![Stats](https://img.shields.io/badge/Doc_lines-453-ffb469.svg)]()
<!--end-->

An very fast, highly scalable (both horizontally and vertically), and simple system for workflows, queues, outbox-pattern and job scheduling.

You can easily embed it directly in your solutions or use it as a stand-alone workflow. 




# 1. Design goals

Reasons to try out GreenFeet Workflow

**Simplicity** 
* We model only the steps in a workflow, not the transitions between them. 
* This greatly simplify the datamodel, the versioning of a flow or a step, and enable you to use reusable code blocks for determining a transition.
* It is easy to embed it directly into your solutions to improve resiliance

**Steps are implemented in C# *not* in some obscure language** 
* hence the code is readable, deubable, testable - like the rest of your code base
* the code can use existing best practices for logging, IOC containers etc.
* you can use existing branching, and deployment strategies and processes
* You *do not* need a specual graphical editor for specifying flows

**The datamodel is simple - just three DB tables.** 
* If things break in production, it is easy for you to figure out what has happened and how to remedy the problem
* You can reason about the consequences of versioning the step implementations vs. simply change the existing flows.

**Scalable run-time.** 
* We Support both vertical and horizontal scalling. 
* You can add more threads to the workflow engine (vertical scaling)
* or add more servers that collectively will perform step executions (horizontal scaling)

**No external dependencies.** 
* The core library has no external dependencies, you can use whatever database, logger, json/xml/binary serializer you want and the versions of libraries you want to use.




# 2. Getting started

To define a workflow with the two steps  `FetchData` (which fetches some data), and `AnalyzeWords` (that analyzes the data), we implement interface `IStepImplementation` twice. 
To transition from one step to one (or several steps), use `Done()`. This tells the engine that the current step has finished sucesfully. You can supply one or more steps that will be executed in the future. 
This is how you control ordering of events.

There are no restrictions on the names of steps, but we found using a scheme similar to REST api's is beneficial. Hence we recommend you to use `{version}/{business domain}/{workflow name}/{workflow step}`. 

```
[StepName(Name)]
class FetchData : IStepImplementation
{
    public const string Name = "v1/demos/fetch-word-analyze-email/fetch";

    public async Task<ExecutionResult> ExecuteAsync(Step step)
    {
        ... 
        return step.Done()
                .With(new Step(AnalyzeWords.Name, state-for-step));
    }
}

[StepName(Name)]
class AnalyzeWords : IStepImplementation
{
    public const string Name = "v1/demos/fetch-wordanalyze-email/process";

    public async Task<ExecutionResult> ExecuteAsync(Step step)
    {
        ...        
        return step.Done();
    }
}
```

Below are some more elaborate exaples.

### Simple console demo 

A fully working C# example in one file: https://github.com/kbilsted/GreenFeetWorkFlow/blob/master/src/Demos/GreenFeetWorkFlow.ConsoleDemo/Program.cs


### Webapi demo 

Now that you have understood the basics, lets make an example using a real IOC container and a database see https://github.com/kbilsted/GreenFeetWorkFlow/tree/master/src/Demos/GreenFeetWorkFlow.WebApiDemo


### IOC container

You can use any IOC container that supports named instances. We use Autofac. For more information see https://github.com/kbilsted/GreenFeetWorkFlow/tree/master/src/Product/GreenFeetWorkFlow.Ioc.Autofac


### Database

You likely want to persist workflows in a database. We currently use Microsoft SQL Server in production environments, but and SQL database should be easy to get working. For more information see https://github.com/kbilsted/GreenFeetWorkFlow/tree/master/src/Product/GreenFeetWorkFlow.AdoPersistence



# 3. Core concepts in Greenfeet Workflow

The model revolves around the notion of a *step*. A step is in traditional workfow litterature referred to as an activity. Where activities live in a workflow. The workflow has identity and state and so forth. 
In GreenFeet Workflow, however, there is only a `FlowId` property. No modelling of transitions nor workflow state. It is often desireable to store state around your business entities, in fact it is highly encouraged that you keep doing this. 


A *step* has the following properties

* A step has a *name* that identifies what code to execute when the steps executes
* A step has a *state* which can be deserialed during execution and is serialized after execution. It is not uncommon that it simply holds a reference to data in other database tables.
* A *schedule date* that denote the earliest execution time of the step. 
* A *singleton* flag denoting that only a single step with that name can exist in the ready queue. This is useful for monitoring steps or scenarios where multiple servers are in use.
* A step lives in either of 3 queues: 
    * *ready* (execution has not yet started, or the step has executed with errors and is automatically retried).
    * *failed* (execution is given up and not retried), 
    * *done* (succesful execution).
* During a step execution a step can spawn one or many new steps. Hence forming a chain or a graph of things to do. These steps execute after the current step. 
* Each step has a number of *tracking fields* such as create date, execution time, correlation id, flow id, created by id.
* There are a few more fields, they are all documented here https://github.com/kbilsted/GreenFeetWorkFlow/blob/master/src/Product/GreenFeetWorkFlow/Step.cs


Orthogonal to the step data we have *step implementations*. 

* These are code blocks with names.
* An implementation may have multiple names, this is very useful in a number of situations
  * When reusing step implementations across versions or across multiple workflows
  * when refactoring step names 
  * It provides better loggin context information, for example, rather than using a generic name like "send email", we can use a descriptive name  "v1/customer-onboarding/send-email" and "v1/customer-churn/send-email", making it easy to identify which flows and business impact on failure..


Operations you can do on steps

* A ready step may be *activated* meaning that it changes scheduling time, and activation parameters may be given.
* A done/failed step may be re-executed, by making a copy of it and adding it to the ready queue.




# 4. Performance 

Simplicify is the focus of the code base. Performance is simply a side-effect of keeping things simple. 

On a 2020 mid-tier computer we execute 10.000/sec steps using a single Workflow engine with 8 workers and an un-optimized SQL Server instance. 

Your milage may wary, so I highly recommend you do your own measurements before jumping to any conclusions. 
You can take outset in some simple test scenarios at https://github.com/kbilsted/GreenFeetWorkFlow/blob/master/src/Demos/GreenFeetWorkFlow.Tests/PerformanceTests.cs




# 5. Flow versioning

Since each step may be regarded as being part of a flow, or as a single independent step, there is no notion of versions. However, you can use a version number in steps (similar to using version in REST api's). 
This enable you to create a new version with new steps that has a different implementation to the old. 
This way existing steps can operate on the old code, and new steps operate on the new code. 
If all steps need to execute on the new code, simply use multiple step names for the new implementation that match the two versions.



# 6. Retries and ordering 
The automatic retry of a step in case of a failure is key feature. You can control ordering to some degree by putting longer and longer delays when retrying a failing step. This technique is sometimes called exponential back-off, since the time between retries exponentially increase to ensure throughput of succesful jobs. The default retry delay is calculated as `delay = retries^3 seconds`. 

If you want to stop retrying either return a `step.Fail()` or `throw FailCurrentStepException`.

Step execution is only orderes by an earliest execution time. If you need to control that step "B" execute before step "C". Then from step "A" spawn a step "B", and in step "B" spawn a step "C".




# 7. GreenFeet Workflow and related concepts 
Another way to gain conceptual insights into the framework, we explain why GreenFeet workflow is a good implementation fit to many concepts.


### GreenFeet as a Queue
You may not think of GreenFeet as a queue since the step execution is unordered. Queue's are asociated with FIFO - First In First Out. 
A consequence of FIFO is that when queue elements can fail and retry, the FIFO property will stop the entire queue. For most real life scenarios this is unacceptable, hence most
queues are in fact not FIFO.

Thus we can implement a queue as a a workflow with only one step. 


### GreenFeet as a Job scheduler
The system can act as a job scheduler. A step can be scheduled for a certain time and re-executed again at a certain time. To ensure only one instance exist, use the `Singleton` attribute.


### GreenFeet as the 'outbox pattern'
The *outbox pattern* is an implementation strategy you often read about when dealing with
events or distributed systems. It is a way to ensure that notifying other systems of a change happens in the same transaction
as the change itself. The implementation is simply to insert a row into a queue that notifies the other system. 

This is exactly a one-to-one match with a step in GreenFeet Workflow.

