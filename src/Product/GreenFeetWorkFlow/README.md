# GreenFeetWorkFlow .Net

This is the main workflow engine implementation. 




# TODOs

### nice to have's
* create singleton job that monitors the engine activity and add simple performance counters
* add a delay to worker start so when used in eg a webapi, you can debug the api without greensteps
* RuntimeData add CRUD step operations
    * done ready task - spawn new task to ensure we perform the operaton in case the tast is rerunning and is long to execute - worst case a direct call would time out waiting 
    * fail ready task - spawn new task to ensure we perform the operaton in case the tast is rerunning and is long to execute - worst case a direct call would time out waiting 
    *  activateWaitingReadyTask
* AddStepIfNotExist 
    *   method that first check the db - then inserts.. 
    * since there can be multiple instances of the engine running, we can still have a race condition, we still need to catch an duplicatekey exception that we need to create and use in the persistence layer. 
    * SearchModel needs be extended

### documentation
* Getting started guide
* how to set up priorities by using multiple instances of the queue - it is better than priority - since it won't starve low priority jobs. - disadvantage - if very few high priority and many lowpriority then use many workers for low priority. If equal amount of low and high priority use many workers in the high priority engine and few workers in the low priority engine. If the numbers fluctuate - use equal amount of workers in each.
*  explain why we dont store the raw type from step state objects. will be serialized eg into
 System.Collections.Generic.Dictionary`2[[System.String, System.Private.CoreLib, Version=6.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e],[System.Object, System.Private.CoreLib, Version=6.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]]
    or
   GreenFeetWorkflow.Tests+BuyInstructions
   we dont want to rely on corelib in v6 or that we use very specific private classes


### tests
* add test case på at man aktiverer et eksekverende step - som dermed er skrive-låst - skal nok anvende en 2s timeout


### Performance todo's
* Add steps to insert using prepared sql and possibly multiple values
* Hardcode column over GetOrdinal()
* delay should have a counter in each worker, if some threshold has been reached wait for a longer period - current implementation block performane of e.g few repeating jobs