using Autofac;
using System.Reflection;

namespace MicroWorkflow;

public static class AutofacExtensions
{
    /// <summary> for unit testing - builds the autofac from the parameters </summary>
    public static void RegisterInstances(this ContainerBuilder builder, IWorkflowLogger? logger, params (string, IStepImplementation)[] stepHandlers)
    {
        stepHandlers.ToList().ForEach(x =>
        {
            if (logger != null && logger.InfoLoggingEnabled)
                logger.LogInfo($"Registering step '{x.Item1}' to type '{x.Item2.GetType()}", null, null);

            builder.RegisterInstance(x.Item2).Named<IStepImplementation>(x.Item1);
        });
    }

    public static void RegisterStepImplementation(this ContainerBuilder builder, IWorkflowLogger? logger, Type implementationType, string stepName)
    {
        if (logger != null && logger.InfoLoggingEnabled)
            logger.LogInfo($"Registering step '{stepName}' to type '{implementationType}", null, null);

        builder.RegisterType(implementationType).Named<IStepImplementation>(stepName);
    }

   
    /// <summary> Register all implementations that are anotated with the <see cref="StepNameAttribute"/> </summary>
    public static void RegisterStepImplementations(this ContainerBuilder builder, IWorkflowLogger? logger, params Assembly[] assemblies)
    {
        foreach (var (implementationType, stepName) in ReflectionHelper.GetStepsFromAttribute(assemblies))
            RegisterStepImplementation(builder, logger, implementationType, stepName);
    }
}