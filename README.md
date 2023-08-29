# GreenFeetWorkFlow .Net
<!--start-->
[![Stats](https://img.shields.io/badge/Code_lines-1,8_K-ff69b4.svg)]()
[![Stats](https://img.shields.io/badge/Test_lines-0-69ffb4.svg)]()
[![Stats](https://img.shields.io/badge/Doc_lines-133-ffb469.svg)]()
<!--end-->

An very fast, highly scalable and simple workflow, queue and jobscheduling system.



## What is GreenFeet?

### GreenFeet is a reusable Workflow system!
An execution unit is called a step. A step can spawn new steps, hence forming a chain. This is similar to a workflow. There are, however, no notion of transitions or a overarching concept of a flow other than a simple ID. 

### GreenFeet is a reusable Queue
The system can also act as a queue. A queue is simply a workflow with only one step in it. Step execution is unordered, but so are all queues that support retries of failed jobs. 
A Steps' execution can be controlled only by a schedule time which define the earliest execution time. 

### GreenFeet is a Job scheduler
The system can act as a simple job scheduler, in that a step can re-execute after some delay or date. Singleton steps are also supported, ensuring only one instance can exist.




## Design goals

* *Simple data model.* When things break in production, it must be possible to figure what to do to remedy the problem.
* *Simple implementation.* The code should be easy to read and understand. We know how to code C# and how to deploy code in test and production. We don't need another domain-specific language or graphical editors for flow composition.
* *Scalable run-time.* Support both vertical and horizontal scalling. You can add more threads to the workflow engine, or add more computers that will participate
* *No external dependencies.* The core library has no external dependencies, you can use what ever database, logger, json/xml/binary whatever you want. And you decide yourself the versions of whatever libraries you want.
* *Utilize what is available.* We minimize the code base, and optimize the performance and usability by not reinventing what is already available. We use C#, IOC containers, logging frameworks and optionally simple SQL.





## Performance 
Simplicify is the focus of the code base. Performance is simply a side-effect of keeping things simple. 
On my 2020 mid-tier computer we execute 10.000/sec steps using a single computer, 8 threads and SQL Server. 




# TODOs

### nice to have's
* create singleton job that monitors the engine activity and add simple performance counters
* for testing tillad at der kun hentes et bestemt flowid 
* add a delay to worker start so when used in eg a webapi, you can debug the api without greensteps
* RuntimeData add CRUD step operations
    * done ready task - spawn new task to ensure we perform the operaton in case the tast is rerunning and is long to execute - worst case a direct call would time out waiting 
    * fail ready task - spawn new task to ensure we perform the operaton in case the tast is rerunning and is long to execute - worst case a direct call would time out waiting 
    *  activateWaitingReadyTask



### tests
* add test case på at man aktiverer et eksekverende step - som dermed er skrive-låst - skal nok anvende en 2s timeout


### Performance todo's
* Add steps to insert using prepared sql and possibly multiple values
* Hardcode column over GetOrdinal()
