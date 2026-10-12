using Typeshift.Toolsets;

namespace Typeshift.Samples.PackageConsumer;

/// <summary>
/// Greets people.
/// </summary>
public partial class GreetingToolset : Toolset
{
    /// <summary>
    /// Declares the greeting tool.
    /// </summary>
    public GreetingToolset() =>
        AddTool(Greet)
            .WithName("greet");

    /// <summary>
    /// Greets a person by name.
    /// </summary>
    /// <param name="name">Who to greet.</param>
    public static string Greet(string name) => $"Hello, {name}!";
}
