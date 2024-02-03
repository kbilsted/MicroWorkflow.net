﻿using System.Reflection;

namespace GreenFeetWorkflow;

public class ReflectionHelper
{
    /// <summary> Harvest all steps annotated with <see cref="StepNameAttribute"/> </summary>
    /// <exception cref="Exception">When duplicate step names are found</exception>
    public static IEnumerable<(Type implementationType, string stepName)> GetStepsFromAttribute(params Assembly[] assemblies)
    {
        var result = new Dictionary<string, (Type implementationType, string stepName)>();

        foreach (var assembly in assemblies)
        {
            var steps = GetSteps(assembly);

            foreach (var step in steps)
            {
                if (result.TryGetValue(step.stepName, out var existingStep))
                    throw new Exception(
                        $"Duplicate step name (name:{step.stepName}, type: {step.implementationType}) matches (name:{existingStep.stepName}, type: {existingStep.implementationType})");

                result.Add(step.stepName, step);
            }
        }

        return result.Values;
    }

    static IEnumerable<(Type implementationType, string stepName)> GetSteps(Assembly a)
    {
        var x = a.GetTypes()
            .Select(x => new { type = x, attrs = x.GetCustomAttributes<StepNameAttribute>() })
            .SelectMany(x => x.attrs, resultSelector: (x, a) => (x.type, a.Name))
            .ToArray();

        return x;
    }
}
