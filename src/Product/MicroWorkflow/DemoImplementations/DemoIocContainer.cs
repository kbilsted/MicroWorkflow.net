using System.Reflection;

namespace MicroWorkflow;

/// <summary>
/// a demo holder for instances that can be retrieved by the engine
/// </summary>
public class DemoIocContainer : IWorkflowIocContainer
{
    public Dictionary<string, object> Entries = new Dictionary<string, object>();

    public DemoIocContainer()
    {
    }

    public DemoIocContainer(params (string, IStepImplementation)[] stepHandlers)
    {
        stepHandlers.ToList().ForEach(x => { Entries.Add(x.Item1, x.Item2); });
    }

    public T GetInstance<T>()
    {
        return (T)Entries.First(x => x.Key == typeof(T).FullName).Value;
    }

    public IStepImplementation? GetNamedInstance(string stepName)
    {
        if (Entries.TryGetValue(stepName, out var x))
            return (IStepImplementation)x;

        return null;
    }

    public DemoIocContainer RegisterNamedSteps(Assembly assembly)
    {
        (Type implementationType, string stepName)[] registrations = ReflectionHelper.GetStepsFromAttribute(assembly).ToArray();

        foreach (var registration in registrations)
        {
            var instance = Activator.CreateInstance(registration.implementationType);
            Entries.Add(registration.stepName, instance!);
        }

        return this;
    }
}

