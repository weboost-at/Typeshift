namespace Typeshift.Samples.PackageConsumer;

/// <summary>
/// Greets people.
/// </summary>
public partial class GreetingToolset : Toolset
{
    /// <summary>
    /// Declares the greeting tool.
    /// </summary>
    public GreetingToolset()
    {
        AddTool(Greet);
        AddTool(Welcome);
    }

    /// <summary>
    /// Greets a person by name.
    /// </summary>
    /// <param name="name">Who to greet.</param>
    /// <returns>The greeting.</returns>
    public static string Greet(string name) => $"Hello, {name}!";

    /// <summary>
    /// Welcomes a guest.
    /// </summary>
    /// <param name="guest">The guest who arrived.</param>
    /// <returns>The welcome.</returns>
    public static string Welcome(Guest guest) => $"Welcome, {guest.Name}!";
}

/// <summary>
/// A guest at the door.
/// </summary>
/// <param name="Name">The name the guest gave.</param>
public sealed record Guest(string Name);
