﻿using Autofac;
using MicroWorkflow.DemoImplementation;

namespace MicroWorkflow;

public class RegisterWorkflow : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // log to in-memory storage
        builder.RegisterType<DemoInMemoryPersister>().As<IStepPersister>();

        // use a simple logger
        builder.RegisterType<DiagnosticsStepLogger>().As<IWorkflowLogger>();

        // use .net json
        builder.RegisterType<DotNetStepStateFormatterJson>().As<IWorkflowStepStateFormatter>();

        // register the engine for our hosted service
        builder.RegisterType<WorkflowEngine>().As<WorkflowEngine>();
        
        // configure autofac as the IOC container to use 
        builder.RegisterType<AutofacAdaptor>().As<IWorkflowIocContainer>();

        // find and register all step-implementations
        builder.RegisterStepImplementations(null, GetType().Assembly);
    }
}