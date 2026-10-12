namespace Agentic.Modules;

/// <summary>
/// Names the generated module of an assembly, so a host compilation finds it in its references.
/// </summary>
/// <param name="module">The module class that registers the assembly's toolsets and agents.</param>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class GeneratedModuleAttribute(Type module) : Attribute
{
    /// <summary>
    /// The module class that registers the assembly's toolsets and agents.
    /// </summary>
    public Type Module { get; } = module;
}
