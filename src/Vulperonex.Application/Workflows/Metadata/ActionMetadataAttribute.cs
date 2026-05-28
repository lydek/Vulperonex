namespace Vulperonex.Application.Workflows.Metadata;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ActionMetadataAttribute(string displayName, string description) : Attribute
{
    public string DisplayName { get; } = displayName;
    public string Description { get; } = description;
}

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class ActionParamAttribute(string label, string type, bool required = false, string? help = null) : Attribute
{
    public string Label { get; } = label;
    public string Type { get; } = type;
    public bool Required { get; } = required;
    public string? Help { get; } = help;
}
