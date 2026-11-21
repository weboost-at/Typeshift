namespace Typeshift;

/// <summary>
/// An agent of your app as the runtime lists it, for a page that lists your agents or an endpoint that runs any of
/// them by its kind.
/// More in the guide: <see href="https://weboost-at.github.io/Typeshift/guide/registration#the-catalog">The
/// catalog</see>.
/// </summary>
/// <param name="Type">The agent's class.</param>
/// <param name="Name">
/// The agent's name: its <c>WithName</c>, or its class name in snake_case. Two agents may share one.
/// </param>
/// <param name="Kind">
/// The kind its stored settings go by: its <c>WithStorageKind</c>, or its class name. No two agents share one, so
/// <see cref="ITypeshiftRuntime.CreateHost(string)"/> takes it.
/// </param>
/// <param name="Description">
/// What the agent does: its <c>WithDescription</c>, or the summary of its XML docs; null when it has none.
/// </param>
public sealed record AgentInfo(Type Type, string Name, string Kind, string? Description)
{
    /// <summary>
    /// The tools of the agent a customer's settings may switch off, by name, each one a use's
    /// <c>Configurable(t => t.X)</c> picked; empty when the agent offers every tool in every run. A settings page shows
    /// a switch for each.
    /// </summary>
    public IReadOnlyList<string> SwitchableTools { get; init; } = [];
}
