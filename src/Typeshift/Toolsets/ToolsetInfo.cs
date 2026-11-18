namespace Typeshift;

/// <summary>
/// A toolset of your app as the runtime lists it, for a tool picker or a page that shows what your agents can offer.
/// More in the guide: <see href="https://weboost-at.github.io/Typeshift/guide/registration#the-catalog">The
/// catalog</see>.
/// </summary>
/// <param name="Type">The toolset's class.</param>
/// <param name="DisplayName">The name for people that its <c>WithDisplayName</c> gives, or null without one.</param>
/// <param name="Tools">Its tools, in the order it declares them.</param>
public sealed record ToolsetInfo(Type Type, string? DisplayName, IReadOnlyList<ToolInfo> Tools);
