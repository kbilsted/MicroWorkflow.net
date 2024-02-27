using System.Reflection;
using Autofac;

namespace MicroWorkflow;

public class AutofacAdaptor : IWorkflowIocContainer
{
    readonly IComponentContext? container;
    readonly ContainerBuilder? builder;

    public AutofacAdaptor(IComponentContext container) => this.container = container ?? throw new ArgumentNullException(nameof(container));

    public AutofacAdaptor(ContainerBuilder builder) => this.builder = builder ?? throw new ArgumentNullException(nameof(builder));

    public T GetInstance<T>() where T : notnull => container!.Resolve<T>() ?? throw new Exception($"Type {typeof(T)} is not registered");

    public IStepImplementation? GetStep(string stepName)
    {
        if (!container!.IsRegisteredWithName<IStepImplementation>(stepName))
            return null;

        return container!.ResolveNamed<IStepImplementation>(stepName);
    }

    public void RegisterWorkflowStep(string stepName, Type implementationType)
        => builder!.RegisterType(implementationType).Named<IStepImplementation>(stepName);

    public void RegisterWorkflowStep(string stepName, IStepImplementation instance)
        => builder!.RegisterInstance(instance).Named<IStepImplementation>(stepName);
}

public static class AutofacExtensions
{
    public static void UseMicroWorkflow(this ContainerBuilder builder, WorkflowConfiguration config, params Assembly?[] assemblies)
    {
        builder.RegisterInstance(config);
        builder.RegisterInstance(config.LoggerConfiguration);

        builder.RegisterType<AutofacAdaptor>().As<IWorkflowIocContainer>();

        builder.RegisterType<WorkflowEngine>().AsSelf();

        assemblies = ReflectionHelper.FindRelevantAssemblies(assemblies);

        var registrar = new AutofacAdaptor(builder);
        foreach (var (stepName, implementationType ) in ReflectionHelper.FindStepsFromAttribute(assemblies!))
            registrar.RegisterWorkflowStep(stepName, implementationType);

        FindAndRegister<IWorkflowLogger>();
        FindAndRegister<IWorkflowStepPersister>();
        FindAndRegister<IWorkflowStepStateFormatter>();

        void FindAndRegister<T>() where T : notnull
        {
            var x = GetTypesInherit<T>(assemblies!);
            if (x != null)
                builder.RegisterType(x).As<T>();
        }
    }

    static Type? GetTypesInherit<T>(Assembly[] assembly)
        => assembly.SelectMany(x => x.GetTypes())
            .Where(x => x.IsClass && !x.IsAbstract && x.IsAssignableTo(typeof(T)))
            .FirstOrDefault();

    public static void RegisterWorkflowSteps(this ContainerBuilder builder, params Assembly?[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
            assemblies = AppDomain.CurrentDomain.GetAssemblies();

        var registrar = new AutofacAdaptor(builder);
        foreach (var (stepName, implementationType) in ReflectionHelper.FindStepsFromAttribute(assemblies!))
            registrar.RegisterWorkflowStep(stepName, implementationType);
    }

    public static void RegisterWorkflowSteps(this ContainerBuilder builder, params (string stepName, IStepImplementation implementationType)[] stepHandlers)
    {
        var registrar = new AutofacAdaptor(builder);
        stepHandlers.ToList().ForEach(x => registrar.RegisterWorkflowStep(x.stepName, x.implementationType));
    }
}

