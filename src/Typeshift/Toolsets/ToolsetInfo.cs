namespace Typeshift;

/// <summary>
/// A toolset of your app as the runtime lists it, for a tool picker or a page that shows what your agents can offer.
/// More in the guide: <see href="https://weboost-at.github.io/Typeshift/guide/registration#the-catalog">The
/// catalog</see>.
/// </summary>
public sealed record ToolsetInfo
{
    /// <summary>
    /// The toolset's class.
    /// </summary>
    public required Type Type { get; init; }

    /// <summary>
    /// The name for people that its <c>WithDisplayName</c> gives, or null without one.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Its tools, in the order it declares them.
    /// </summary>
    public IReadOnlyList<ToolInfo> Tools { get; init; } = [];
}
