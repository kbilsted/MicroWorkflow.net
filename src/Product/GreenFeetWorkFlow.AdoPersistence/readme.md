# Microsoft SQL Server

This package provides integration with Microsoft SQL server to persist steps in the database.


## Getting started

To get started
* Create database scemas. You can use https://github.com/kbilsted/MinimaWorkflow.Net/blob/master/src/GreenFeetWorkFlow.AdoPersistence/createdb.sql directly or rename the tables to whatever you see fit
* Use an instance of `SqlServerPersister` and set the values `TableNameReady`, `TableNameFail`, `TableNameDone` if you rename the tables.

Since renaming is supported, it is possible to spin up multiple instances of the workflow engine.

