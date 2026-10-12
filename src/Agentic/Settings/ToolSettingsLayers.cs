namespace Agentic.Settings;

// The layers a tool's settings resolve from, lowest first, see docs/architecture.md
internal sealed record ToolSettingsLayers
{
    public IToolOverrides? Defaults { get; init; }

    public object? Agent { get; init; }

    public IToolOverrides? AgentOverrides { get; init; }

    public IToolOverrides? Run { get; init; }
}
