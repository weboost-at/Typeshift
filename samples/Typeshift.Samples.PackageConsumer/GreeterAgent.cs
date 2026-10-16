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
            .WithModel("any-model");

        UseToolset<GreetingToolset>();

        UseToolset<Typeshift.Toolsets.TodoToolset>();
    }
}
