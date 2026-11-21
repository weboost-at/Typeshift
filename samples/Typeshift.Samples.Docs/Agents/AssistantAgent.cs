namespace Typeshift.Samples.Agents;

// begin-snippet: smallest-agent
internal partial class AssistantAgent : Agent
{
    public AssistantAgent() => WithInstructions("Answer the customer's question in one friendly sentence.");
}
// end-snippet
