namespace Agentic.Tools;

// Scoped: every tool call gets its own scope, and the generated function sets the call's settings on it
internal sealed class ToolSettingsAccessor
{
    public object? Settings { get; set; }
}
