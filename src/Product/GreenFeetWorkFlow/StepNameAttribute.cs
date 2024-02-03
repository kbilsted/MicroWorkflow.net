namespace GreenFeetWorkflow;

/// <summary>
/// use this attribute as an easy way to register stepnames for implementations
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class StepNameAttribute : Attribute
{
    public string Name { get; set; }
    public StepNameAttribute(string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        if (name.StartsWith(" ") || name.EndsWith(" "))
            throw new Exception($"{nameof(StepNameAttribute)} instance with Name '{name}' may not start or end with ' '.");

        Name = name;
    }
}
