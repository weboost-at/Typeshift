using Typeshift.Demos.Providers;

namespace Typeshift.Demos.MultiProvider;

internal partial class AssistantAgent : Agent
{
    public AssistantAgent()
    {
        WithDescription("Answers questions, telling the time where it helps.");

        // CLUNKY 2: the code must name a model, so it names the catalog's sentinel; the user's choice comes through
        // the configurable ModelId, stored per user
        WithChatOptions()
            .WithModel(ProviderCatalog.Default)
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
