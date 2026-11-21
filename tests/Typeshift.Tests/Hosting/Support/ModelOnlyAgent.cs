namespace Typeshift.Tests.Hosting.Support;

// Names a model and no provider, so its runs take the client registered for every agent
public partial class ModelOnlyAgent : Agent
{
    public const string Model = "model-only";

    public ModelOnlyAgent() =>
        WithChatOptions()
            .WithModel(Model)
            .Configurable(o => o.ModelId);
}

// Names a provider with one model and then another model alone, which keeps the provider
public partial class SwitchedModelAgent : Agent
{
    public const string ModelProvider = "switched-provider";

    public const string Model = "switched-model";

    public SwitchedModelAgent() =>
        WithChatOptions()
            .WithModel(ModelProvider, "first-model")
            .WithModel(Model);
}
