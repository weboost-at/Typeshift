namespace Typeshift;

/// <summary>
/// The base of a partial class that declares its tools in its constructor. The generator turns each tool into a
/// reflection-free <c>AIFunction</c>, its configurable settings into a <c>{Tool}Overrides</c> record, and its tools
/// into properties of <see cref="ToolList{TToolset}"/>.
/// </summary>
public abstract class Toolset
{
    /// <summary>
    /// A name for people, a tool picker say; null unless <see cref="WithDisplayName"/> sets it.
    /// </summary>
    public string? DisplayName { get; private set; }

    /// <summary>
    /// Names the toolset for people.
    /// </summary>
    /// <param name="displayName">The name.</param>
    protected void WithDisplayName(string displayName) => DisplayName = displayName;

    // D4: the generator reads the chain at compile time; at runtime it declares nothing
    /// <summary>
    /// Declares a tool backed by a static method. Its name is the method's in snake_case without an
    /// <c>Async</c> suffix (<c>CloseTicketAsync</c> is <c>close_ticket</c>), its description the method's
    /// <c>&lt;summary&gt;</c>.
    /// </summary>
    /// <param name="method">The method group: <c>AddTool(SearchTickets)</c>.</param>
    /// <returns>A chain that renames, describes, marks and configures the tool.</returns>
    protected ToolBuilder AddTool(Delegate method) => ToolBuilder.Instance;
}
