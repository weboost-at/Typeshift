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
            .WithModel("any-provider", "any-model");

        UseToolset<GreetingToolset>();
    }
}
