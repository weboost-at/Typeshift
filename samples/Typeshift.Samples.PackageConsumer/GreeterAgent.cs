namespace Typeshift.Samples.PackageConsumer;

/// <summary>
/// Greets whoever it talks to.
/// </summary>
public partial class GreeterAgent : Agent
{
    /// <summary>
    /// Declares the agent and its tools.
    /// </summary>
    public GreeterAgent()
    {
        WithChatOptions()
            .WithProvider("any-provider")
            .WithModel("any-model")
            .Configurable(o => o.Temperature);

        UseToolset<GreetingToolset>();

        // The compiler reports the deprecated toolset here, once, where this code names it
#pragma warning disable CS0618
        UseToolset<FarewellToolset>();
#pragma warning restore CS0618
    }
}
