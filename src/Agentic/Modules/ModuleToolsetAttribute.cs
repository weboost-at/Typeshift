namespace Agentic.Modules;

/// <summary>
/// Lists a toolset on its assembly's generated module.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ModuleToolsetAttribute(Type toolset) : Attribute
{
    /// <summary>
    /// The toolset class.
    /// </summary>
    public Type Toolset { get; } = toolset;
}
