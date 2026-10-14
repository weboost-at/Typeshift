namespace Typeshift.Generators;

// The names of the pipeline steps, so tests can check what an edit recomputed
internal static class TrackingNames
{
    public const string Toolsets = nameof(Toolsets);

    public const string Agents = nameof(Agents);

    public const string References = nameof(References);

    public const string AgentOutputs = nameof(AgentOutputs);

    public const string Module = nameof(Module);
}
