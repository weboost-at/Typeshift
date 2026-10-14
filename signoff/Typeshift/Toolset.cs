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

    /// <summary>
    /// Declares a tool backed by a public static method. Its name is the method's in snake_case without an
    /// <c>Async</c> suffix (<c>CloseTicketAsync</c> is <c>close_ticket</c>), its description the method's
    /// <c>&lt;summary&gt;</c>.
    /// </summary>
    /// <param name="method">The method group: <c>AddTool(SearchTickets)</c>.</param>
    /// <returns>A chain that renames, describes, guards and configures the tool.</returns>
    protected ToolBuilder AddTool(Delegate method) => throw null!;
}

/// <summary>
/// The chain of one tool. The generator reads it at compile time; settings come last.
/// </summary>
public sealed class ToolBuilder
{
    internal ToolBuilder()
    {
    }

    /// <summary>
    /// Overrides the name derived from the method. The name keys the tool's stored settings, so pin it here before
    /// renaming a method whose settings are stored.
    /// </summary>
    /// <param name="name">A lower_snake_case constant, unique across everything the host sees.</param>
    /// <returns>The same chain.</returns>
    public ToolBuilder WithName(string name) => this;

    /// <summary>
    /// Overrides the description taken from the method's <c>&lt;summary&gt;</c>.
    /// </summary>
    /// <param name="description">A constant.</param>
    /// <returns>The same chain.</returns>
    public ToolBuilder WithDescription(string description) => this;

    /// <summary>
    /// Pauses a run that calls the tool until the host approves or rejects the call.
    /// </summary>
    /// <returns>The same chain.</returns>
    public ToolBuilder RequiresApproval() => this;

    /// <summary>
    /// Marks the tool as reading only, without side effects, so plan mode offers it.
    /// </summary>
    /// <returns>The same chain.</returns>
    public ToolBuilder ReadOnly() => this;

    /// <summary>
    /// Gives the tool a settings record whose members stay in code unless <c>Configurable</c> names them. The method
    /// receives the resolved settings through a parameter of their type, which the model never sees.
    /// </summary>
    /// <typeparam name="TSettings">A record; its parameterless constructor sets the code defaults.</typeparam>
    /// <returns>The allow list.</returns>
    public ToolBuilder<TSettings> WithSettings<TSettings>() where TSettings : class, new() => new();

    /// <summary>
    /// Gives the tool a settings record whose settable members may all change at runtime, but those
    /// <c>NotConfigurable</c> names.
    /// </summary>
    /// <typeparam name="TSettings">A record; its parameterless constructor sets the code defaults.</typeparam>
    /// <returns>The deny list.</returns>
    public ConfigurableSettingsBuilder<TSettings> WithConfigurableSettings<TSettings>()
        where TSettings : class, new() =>
        new();
}

/// <summary>
/// The allow list of a tool's configurable settings members.
/// </summary>
/// <typeparam name="TSettings">The tool's settings record.</typeparam>
public sealed class ToolBuilder<TSettings> where TSettings : class, new()
{
    internal ToolBuilder()
    {
    }

    /// <summary>
    /// Lets a member change at runtime, through the agent's stored settings or a run. The lambda never runs.
    /// </summary>
    /// <typeparam name="TMember">The member's type; one the generated records can store.</typeparam>
    /// <param name="member">The member, <c>s => s.Limit</c>.</param>
    /// <returns>The same list.</returns>
    public ToolBuilder<TSettings> Configurable<TMember>(Func<TSettings, TMember> member) => this;
}

/// <summary>
/// The deny list of a tool's configurable settings members.
/// </summary>
/// <typeparam name="TSettings">The tool's settings record.</typeparam>
public sealed class ConfigurableSettingsBuilder<TSettings> where TSettings : class, new()
{
    internal ConfigurableSettingsBuilder()
    {
    }

    /// <summary>
    /// Keeps a member in code. The lambda never runs.
    /// </summary>
    /// <typeparam name="TMember">The member's type.</typeparam>
    /// <param name="member">The member, <c>s => s.Timeout</c>.</param>
    /// <returns>The same list.</returns>
    public ConfigurableSettingsBuilder<TSettings> NotConfigurable<TMember>(Func<TSettings, TMember> member) => this;
}

/// <summary>
/// A tool's name, lower_snake_case.
/// </summary>
/// <param name="Value">The name.</param>
public readonly record struct ToolName(string Value);

/// <summary>
/// The tools of <typeparamref name="TToolset"/>, to pick from in <c>Include</c> and <c>Exclude</c>. The generator adds
/// one property per tool, <c>t.SearchTickets</c>; a tool of another toolset has none, so picking it does not compile.
/// </summary>
/// <typeparam name="TToolset">The toolset.</typeparam>
public sealed class ToolList<TToolset> where TToolset : Toolset
{
    internal ToolList()
    {
    }
}
