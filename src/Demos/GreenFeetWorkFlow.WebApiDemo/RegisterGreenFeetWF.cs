using Autofac;
using GreenFeetWorkflow.Ioc.Autofac;

namespace GreenFeetWorkflow.WebApiDemo;

public class RegisterGreenFeetWorkFlow : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<DemoInMemoryPersister>().As<IStepPersister>();
        builder.RegisterType<DiagnosticsStepLogger>().As<IWorkflowLogger>();
        builder.RegisterType<AutofacBinding>().As<IWorkflowIocContainer>();
        builder.RegisterType<WorkflowEngine>().As<WorkflowEngine>();
        builder.RegisterType<DotNetStepStateFormatterJson>().As<IStateFormatter>();
        builder.RegisterStepImplementations(GetType().Assembly);
    }
}