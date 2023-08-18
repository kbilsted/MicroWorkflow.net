using Autofac;

namespace GreenFeetWorkflow.Ioc.Autofac;

public class AutofacBinding : IWorkflowIocContainer
{
    IComponentContext container;

    public AutofacBinding(IComponentContext container)
    {
        this.container = container;
    }

    public AutofacBinding(ContainerBuilder builder) : this(builder.Build())
    { }

    public T GetInstance<T>() where T : notnull
    {
        return container.Resolve<T>();
    }

    public IStepImplementation? GetNamedInstance(string statename)
    {
        if (!container.IsRegisteredWithName<IStepImplementation>(statename))
            return null;

        return container.ResolveNamed<IStepImplementation>(statename);
    }
}

