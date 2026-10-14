using System.Text.Json.Serialization;

namespace Typeshift.Toolsets;

// D45: the built-in tools' arguments and results, for Native AOT
[JsonSerializable(typeof(IReadOnlyList<TodoItem>))]
[JsonSerializable(typeof(CommandResult))]
[JsonSerializable(typeof(IReadOnlyList<WebSearchResult>))]
[JsonSerializable(typeof(WebPage))]
[JsonSerializable(typeof(Uri))]
internal sealed partial class ToolsetsJson : JsonSerializerContext;
