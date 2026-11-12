namespace Typeshift.Tests.Agents.Support;

// Sets its instructions in the constructor, with no GetInstructions override
public partial class InstructedAgent : Agent
{
    public const string Prompt = "Answer in one sentence.";

    public InstructedAgent()
    {
        WithInstructions(Prompt);

        WithChatOptions()
            .WithModel(ProbeAgent.ModelProvider, ProbeAgent.Model)
            .Configurable(o => o.Instructions);
    }
}
