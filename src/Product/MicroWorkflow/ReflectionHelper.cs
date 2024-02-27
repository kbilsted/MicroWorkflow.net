using System.Reflection;

namespace MicroWorkflow;

public class ReflectionHelper
{
    /// <summary> Harvest all steps annotated with <see cref="StepNameAttribute"/> </summary>
    /// <exception cref="Exception">When duplicate step names are found</exception>
    public static IEnumerable<(Type implementationType, string stepName)> GetStepsFromAttribute(params Assembly[] assemblies)
    {
        var result = new Dictionary<string, (Type implementationType, string stepName)>();

        foreach (var step in assemblies.SelectMany(x => GetSteps(x)))
        {
            if (result.TryGetValue(step.stepName, out var existingStep))
                throw new Exception($"Duplicate step name (name:{step.stepName}, type: {step.implementationType}) matches (name:{existingStep.stepName}, type: {existingStep.implementationType})");

            result.Add(step.stepName, step);
        }

        return result.Values;
    }

    static IEnumerable<(Type implementationType, string stepName)> GetSteps(Assembly a)
    {
        var x = a.GetTypes()
            .Select(x => new { type = x, attrs = x.GetCustomAttributes<StepNameAttribute>() })
            .SelectMany(x => x.attrs, resultSelector: (x, a) => (x.type, a.Name));
        return x;
    }

    public static Assembly[] FindRelevantAssemblies(Assembly?[] assemblies)
    {
        if (assemblies != null && assemblies.Length > 0)
            return assemblies!;

        var ass = AppDomain.CurrentDomain.GetAssemblies()
            .Where(x => x.FullName != null && !x.FullName.StartsWith("System") && !x.FullName.StartsWith("Microsoft"))
            .ToArray();

        Assembly[] allAssembliesWithMicroWorkflowAsLast = new[] { Assembly.GetEntryAssembly() }
            .Concat(ass.Where(x => !x.FullName.StartsWith("MicroWorkflow")))
            .Concat(ass.Where(x => x.FullName.StartsWith("MicroWorkflow")))
            .Distinct()
            .ToArray()!;

        return allAssembliesWithMicroWorkflowAsLast;
    }
}
