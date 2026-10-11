namespace Agentic.Settings;

// The layers a tool's settings resolve from, lowest first, see docs/architecture.md
internal sealed record ToolSettingsLayers
{
    public static ToolSettingsLayers None { get; } = new();

    public StoredSettings? Defaults { get; init; }

    public object? Agent { get; init; }

    public StoredSettings? AgentOverrides { get; init; }

    public object? Run { get; init; }
}
