using Autofac;

namespace MicroWorkflow;

public class AutofacAdaptor : IWorkflowIocContainer
{
    readonly IComponentContext container;

    public AutofacAdaptor(IComponentContext container)
    {
        this.container = container;
    }

    public T GetInstance<T>() where T : notnull
    {
        var v = container.Resolve<T>()
            ?? throw new Exception($"Type {typeof(T)} is not registered");
        return v;
    }

    public IStepImplementation? GetNamedInstance(string statename)
    {
        if (!container.IsRegisteredWithName<IStepImplementation>(statename))
            return null;

        return container.ResolveNamed<IStepImplementation>(statename);
    }
}

