# GreenFeetWorkFlow .Net
<!--start-->
[![Stats](https://img.shields.io/badge/Code_lines-1,8_K-ff69b4.svg)]()
[![Stats](https://img.shields.io/badge/Test_lines-0-69ffb4.svg)]()
[![Stats](https://img.shields.io/badge/Doc_lines-133-ffb469.svg)]()
<!--end-->

An very fast, highly scalable and simple system for workflows, queues, outbox-pattern and job scheduling.



# 1. Design goals

Our goals and your reasons to try out GreenFeet Workflow

* **Simple data model.** It things break in production, it must be easy to figure what has happened and what to do to remedy the problem.
* **Simple implementation.** We use C# not some obscure workflow language. We know how to optimize, debug, and deploy code to test and production. We don't need another domain-specific language or graphical editors for flow composition.
* **Scalable run-time.** Support both vertical and horizontal scalling. You can add more threads to the workflow engine, or add more servers that collectively will perform step executions.
* **No external dependencies.** The core library has no external dependencies, you can use whatever database, logger, json/xml/binary serializer you want. And you decide yourself, the versions of libraries you want to use.
* **stand on the shoulders of giants.** We minimize the code base, and optimize the performance and usability by *not* reinventing what is already available. We use C#, IOC containers, logging frameworks and optionally simple SQL.



# 2. Core concepts in Greenfeet Workflow

The model revolves around the notion of a *step*. A step is in traditional workfow litterature referred to as an activity. 
There are, however, no notion of transitions or an overarching concept of a workflow in GreenFeet. Steps are tied together only by an `FlowId` field. 
And when steps are created during a step execution, thet creator id is recorded.

That we do not model and name transitions, but only the states, make everything
light weight. Conversely, it means that it is difficult to define really generic steps
that can be used in a number of different flows. 
We feel the trade-ofs are in our favor, but you be the judge in your projects.

Now let us zoom in on the concept of a *step*.

* A step has a *name* that identifies what code to execute when the steps executes
* A *state* which can be deserialed during execution and is serialized after execution. It is not uncommon that it simply holds a reference to data in other database tables.
* A *schedule date* that denote the earliest execution time of the step. There is no explicit ordering mechanism. You can control ordering to some degree by putting longer and longer delays when retrying a failing step. This technique is sometimes called exponential back-off, since the time between retries exponentially increase to ensure throughput of succesful jobs. The default retry delay is calculated as `delay = retries^3 seconds`. 
* A *singleton* flag denoting that only a single step with that name can exist in the ready queue. This is useful for monitoring steps or scenarios where multiple servers are in use.
* A step lives in either of 3 queues: 
    * *ready* (execution has not yet started, or the step has executed with errors and is automatically retried).
    * *failed* (execution is given up and not retried), 
    * *done* (succesful execution).
* During a step execution a step can spawn one or many new steps. Hence forming a chain or a graph of things to do. These steps execute after the current step. 
* A *step implementation* which is a code block with one or more an associated names. The code is executed whenever a step with the identical name is being processed. Multiple names are useful in a number of situations
    * When reusing code across versions, 
    * when refactoring step names in a running system, 
    * when logging, it provides better context information, for example, if multiple distinct flows uses the same step, rather than using some generic name, e.g. "send email", each flow can use a name providing the context of the flow, i.e. "v1/customer-onboarding/send-email" and "v1/customer-churn/send-email", making it easy to identify which flows are affected should the step start failing.
* There are a few more fields, they are all documented here https://github.com/kbilsted/GreenFeetWorkFlow/blob/master/src/Product/GreenFeetWorkFlow/Step.cs
* A ready step may be *activated* meaning that it changes scheduling time, and activation parameters may be given.
* Each step has a number of *tracking fields* such as create date, execution time, correlation id, flow id, created by id.


Another way to gain conceptual insights into the framework, we explain why GreenFeet workflow is a good implementation fit to many concepts.


### GreenFeet is a Workflow system
An execution unit is called a step. A step can spawn new steps, hence forming a linear chain or a fan-out graph. Even fan-in graphs is easily implemented as shown in the automated test suite.


### GreenFeet is a reusable Queue
You may not think of GreenFeet as a queue since the step execution is unordered. Queue's are asociated with FIFO - First In First Out. 
A consequence of FIFO is that when queue elements can fail and retry, the FIFO property will stop the entire queue. For most real life scenarios this is unacceptable, hence most
queues are in fact not FIFO.

Thus we can implement a queue as a a workflow with only one step. 


### GreenFeet is a Job scheduler
The system can act as a job scheduler. A step can be scheduled for a certain time and re-executed again at a certain time. To ensure only one instance exist, use the `Singleton` attribute.


### The outbox pattern
The *outbox pattern* is an implementation strategy you often read about when dealing with
events or distributed systems. It is a way to ensure that notifying other systems of a change happens in the same transaction
as the change itself. The implementation is simply to insert a row into a queue that notifies the other system. 

This is exactly a one-to-one match with a step in GreenFeet Workflow.



# 3. Getting started

Enough talking. Let's take a look at some code shall we.


### Simple console demo 

A fully working C# example in one file: https://github.com/kbilsted/GreenFeetWorkFlow/blob/master/src/Demos/GreenFeetWorkFlow.ConsoleDemo/Program.cs


### Webapi demo 

Now that you have understood the basics, lets make an example using a real IOC container and a database see https://github.com/kbilsted/GreenFeetWorkFlow/tree/master/src/Demos/GreenFeetWorkFlow.WebApiDemo


### IOC container

You can use any IOC container that supports named instances. We use Autofac. For more information see https://github.com/kbilsted/GreenFeetWorkFlow/tree/master/src/Product/GreenFeetWorkFlow.Ioc.Autofac


### Database

You likely want to persist workflows in a database. We currently use Microsoft SQL Server in production environments, but and SQL database should be easy to get working. For more information see https://github.com/kbilsted/GreenFeetWorkFlow/tree/master/src/Product/GreenFeetWorkFlow.AdoPersistence




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



