namespace Typeshift;

/// <summary>
/// A tool of a toolset as the runtime lists it, with what a tool picker shows of it.
/// More in the guide: <see href="https://weboost-at.github.io/Typeshift/guide/registration#the-catalog">The
/// catalog</see>.
/// </summary>
/// <param name="Name">
/// The tool's name, lower_snake_case, as the model and its toolset's <c>ToolNames</c> hold it.
/// </param>
/// <param name="Description">
/// What the tool does, as the model reads it: its <c>WithDescription</c>, or the summary or <c>[Description]</c> of
/// its method; null when it has none.
/// </param>
/// <param name="IsReadOnly">Whether the tool only reads, marked with <c>ReadOnly()</c>.</param>
/// <param name="RequiresApproval">Whether its calls wait for approval, marked with <c>RequiresApproval()</c>.</param>
public sealed record ToolInfo(string Name, string? Description, bool IsReadOnly, bool RequiresApproval);
