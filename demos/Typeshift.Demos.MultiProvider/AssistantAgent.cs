namespace Typeshift.Demos.MultiProvider;

internal partial class AssistantAgent : Agent
{
    public AssistantAgent()
    {
        WithDescription("Answers questions, telling the time where it helps.");

        // No model in code: the user's choice comes through the configurable ModelId, stored per user
        WithChatOptions()
            .WithTemperature(0.3f)
            .Configurable(o => o.ModelId)
            .Configurable(o => o.Temperature);

        UseToolset<ClockToolset>();

        ConfigureGetTime()
            .WithFormat("HH:mm");
    }

    protected override string GetInstructions() =>
        "You are a concise assistant. Use get_time when the user asks about the time, then answer in one sentence.";
}
