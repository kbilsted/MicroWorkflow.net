# A weather api demo

A standalone webapi demo that shows how to use the workflow engine as a task scheduler. Every 3 seconds a weather report is simulated to be fetched. Every time the API is called, a cached weather report is returned.

main classes to focus on are:

* `RegisterGreenFeetWorkFlow` - setting up the dependencies
* `WorkflowStarter` - what starts and runs the workflow engine
* `StepFetchWeatherForecast` - the repeating singleton step

