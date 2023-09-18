# Microsoft SQL Server

This package provides integration with Microsoft SQL server to persist steps in the database.


## Getting started

To get started

* Create database scemas and tables with https://github.com/kbilsted/MinimaWorkflow.Net/blob/master/src/GreenFeetWorkFlow.AdoPersistence/createdb.sql 
* Optionally you rename the schemas and tables to whatever you see fit. Then, on an instance of `SqlServerPersister` set the values `TableNameReady`, `TableNameFail`, `TableNameDone`.
* Register in your IOC container `SqlServerPersister` such that a new instance is created when the type is resolved. This is due to the fact that each instance controls its local transaction.
```
var logger = new DiagnosticsStepLogger();
builder.Register<IStepPersister>(c => new SqlServerPersister(ConnectionString, logger));
```

Due to the support of renaming, you can spin up multiple workflow engines within the same solution. This is useful when you want to create queues with different priorities, for example, a "high priority queue" and a "low priority queue".

