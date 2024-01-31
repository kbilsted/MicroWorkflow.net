SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Steps_Ready] (
    [Id]                        INT IDENTITY (1, 1) NOT NULL,
    [Name]                      NVARCHAR (255) NOT NULL,
    [Singleton]                 BIT NOT NULL,
    [FlowId]                    NVARCHAR (255) NOT NULL,
    [SearchKey]                 NVARCHAR (255) NULL,
    [ExecutionCount]            INT            NOT NULL,
    [ScheduleTime]              DATETIME2 (7)  NOT NULL,
    [State]                     NVARCHAR (MAX) NULL,
    [StateFormat]               NVARCHAR (MAX) NULL,
    [ActivationArgs]            NVARCHAR (MAX) NULL,
    [ExecutionStartTime]        DATETIME2 (7)  NULL,
    [ExecutionDurationMillis]   BIGINT         NULL,
    [ExecutedBy]                NVARCHAR (512) NULL,
    [CorrelationId]             NVARCHAR (255) NULL,
    [CreatedTime]               DATETIME2 (7)  NOT NULL,
    [CreatedByStepId]           INT NOT NULL,
    [Description]               NVARCHAR (MAX) NULL
);

GO
CREATE NONCLUSTERED        INDEX [IX_Steps_Ready_Name]            ON [dbo].[Steps_Ready]([Name] ASC);
CREATE UNIQUE NONCLUSTERED INDEX [IX_Steps_Ready_Singleton]       ON [dbo].[Steps_Ready]([Name],[Singleton] ASC) WHERE ([Singleton] = 1);
CREATE NONCLUSTERED        INDEX [IX_Steps_Ready_SearchKey]       ON [dbo].[Steps_Ready]([SearchKey]) ;

GO
ALTER TABLE [dbo].[Steps_Ready]
    ADD CONSTRAINT [PK_Steps_Ready] PRIMARY KEY CLUSTERED ([Id] ASC);

GO




CREATE TABLE [dbo].[Steps_Done] (
    [Id]                      INT            NOT NULL,
    [Name]                    NVARCHAR (255) NOT NULL,
    [Singleton]               BIT NOT NULL,
    [FlowId]                  NVARCHAR (255) NOT NULL,
    [SearchKey]               NVARCHAR (255) NULL,
    [ExecutionCount]          INT         NOT NULL,
    [ScheduleTime]            DATETIME2 (7)  NOT NULL,
    [State]                   NVARCHAR (MAX) NULL,
    [StateFormat]             NVARCHAR (MAX) NULL,
    [ActivationArgs]          NVARCHAR (MAX) NULL,
    [ExecutionStartTime]      DATETIME2 (7)  NULL,
    [ExecutionDurationMillis] BIGINT         NULL,
    [ExecutedBy]              NVARCHAR (512) NULL,
    [CorrelationId]           NVARCHAR (255) NULL,
    [CreatedTime]             DATETIME2 (7)  NOT NULL,
    [CreatedByStepId]         INT NOT NULL,
    [Description]             NVARCHAR (MAX) NULL
);

GO
CREATE CLUSTERED    INDEX [IX_Steps_Done_Id]             ON [dbo].[Steps_Done]([Id] ASC);
CREATE NONCLUSTERED INDEX [IX_Steps_Done_Name]           ON [dbo].[Steps_Done]([Name] ASC);
CREATE NONCLUSTERED INDEX [IX_Steps_Done_CorrelationId]  ON [dbo].[Steps_Done]([CorrelationId]);
CREATE NONCLUSTERED INDEX [IX_Steps_Done_FlowId]         ON [dbo].[Steps_Done]([FlowId]);
CREATE NONCLUSTERED INDEX [IX_Steps_Done_SearchKey]      ON [dbo].[Steps_Done]([SearchKey]) ;


CREATE TABLE [dbo].[Steps_Fail] (
    [Id]                      INT            NOT NULL,
    [Name]                    NVARCHAR (255) NOT NULL,
    [Singleton]               BIT NOT NULL,
    [FlowId]                  NVARCHAR (255) NOT NULL,
    [SearchKey]               NVARCHAR (255) NULL,
    [ExecutionCount]          INT         NOT NULL,
    [ScheduleTime]            DATETIME2 (7)  NOT NULL,
    [State]                   NVARCHAR (MAX) NULL,
    [StateFormat]             NVARCHAR (MAX) NULL,
    [ActivationArgs]          NVARCHAR (MAX) NULL,
    [ExecutionStartTime]      DATETIME2 (7)  NULL,
    [ExecutionDurationMillis] BIGINT         NULL,
    [ExecutedBy]              NVARCHAR (512) NULL,
    [CorrelationId]           NVARCHAR (255) NULL,
    [CreatedTime]             DATETIME2 (7)  NOT NULL,
    [CreatedByStepId]         INT NOT NULL,
    [Description]             NVARCHAR (MAX) NULL
);

GO
CREATE CLUSTERED    INDEX [IX_Steps_Fail_Id]               ON [dbo].[Steps_Fail]([Id] ASC);
CREATE NONCLUSTERED INDEX [IX_Steps_Fail_Name]             ON [dbo].[Steps_Fail]([Name] ASC);
CREATE NONCLUSTERED INDEX [IX_Steps_Fail_CorrelationId]    ON [dbo].[Steps_Fail]([CorrelationId]);
CREATE NONCLUSTERED INDEX [IX_Steps_Fail_FlowId]           ON [dbo].[Steps_Fail]([FlowId]);
CREATE NONCLUSTERED INDEX [IX_Steps_Fail_SearchKey]        ON [dbo].[Steps_Fail]([SearchKey]) ;
GO




CREATE VIEW [Steps] AS
SELECT 'ready' as 'ExecutionState', 
   r.Id,                     
   r.Name,                   
   r.Singleton,          
   r.FlowId,                 
   r.SearchKey,              
   r.ExecutionCount,         
   r.ScheduleTime,           
   [r].[State],         
   r.StateFormat,   
   r.ActivationArgs,
   r.ExecutionStartTime,     
   r.ExecutionDurationMillis,
   r.ExecutedBy,             
   r.CorrelationId,          
   r.CreatedTime,            
   r.CreatedByStepId,        
   r.Description           
FROM [Steps_Ready] r
UNION ALL
SELECT 'done' as 'ExecutionState', 
   d.Id,                     
   d.Name,                   
   d.Singleton,          
   d.FlowId,                 
   d.SearchKey,              
   d.ExecutionCount,         
   d.ScheduleTime,           
   d.[State],         
   d.StateFormat,   
   d.ActivationArgs,
   d.ExecutionStartTime,     
   d.ExecutionDurationMillis,
   d.ExecutedBy,             
   d.CorrelationId,          
   d.CreatedTime,            
   d.CreatedByStepId,        
   d.Description           
FROM [Steps_Done] d
UNION ALL
SELECT 'failed' as 'ExecutionState', 
   f.Id,                     
   f.Name,                   
   f.Singleton,          
   f.FlowId,                 
   f.SearchKey,              
   f.ExecutionCount,         
   f.ScheduleTime,           
   f.[State],         
   f.StateFormat,   
   f.ActivationArgs,
   f.ExecutionStartTime,     
   f.ExecutionDurationMillis,
   f.ExecutedBy,             
   f.CorrelationId,          
   f.CreatedTime,            
   f.CreatedByStepId,        
   f.Description          
FROM [Steps_Fail] f
GO



/* 
-- move rows to fail
begin transaction
insert into dbo.[Steps_Fail]
select * FROM dbo.[Steps_Ready]
WHERE executionCount>6

DELETE FROM dbo.[Steps_Ready]
WHERE executionCount>6
commit

drop view [Steps]
drop table [Steps_Ready]
drop table [Steps_Done]
drop table [Steps_Fail]
delete from [Steps_Ready]
delete from [Steps_Done]

SELECT TOP (100) *  FROM [adotest].[dbo].[Steps_Ready] (nolock)
select * from [Steps_Done]
select * from [Steps_Fail]

*/
