namespace Typeshift;

/// <summary>
/// An agent of your app as the runtime lists it, for a page that lists your agents or an endpoint that runs any of
/// them by its kind.
/// More in the guide: <see href="https://weboost-at.github.io/Typeshift/guide/registration#the-catalog">The
/// catalog</see>.
/// </summary>
public sealed record AgentInfo
{
    /// <summary>
    /// The agent's class.
    /// </summary>
    public required Type Type { get; init; }

    /// <summary>
    /// The agent's name: its <c>WithName</c>, or its class name in snake_case. Two agents may share one.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The kind its stored settings go by: its <c>WithStorageKind</c>, or its class name. No two agents share one, so
    /// <see cref="ITypeshiftRuntime.CreateHost(string)"/> takes it.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// What the agent does: its <c>WithDescription</c>, or the summary of its XML docs; null when it has none.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The tools of the agent a customer's settings may switch off, by name, each one a use's
    /// <c>Configurable(t => t.X)</c> picked; empty when the agent offers every tool in every run. A settings page shows
    /// a switch for each.
    /// </summary>
    public IReadOnlyList<string> SwitchableTools { get; init; } = [];
}
