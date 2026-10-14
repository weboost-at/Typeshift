using Typeshift.Samples.Knowledge;

namespace Typeshift.Samples.Agents;

// begin-snippet: sub-agent
internal partial class ExploreAgent : Agent
{
    public ExploreAgent()
    {
        WithDescription("Explores the help center and answers one question about it, citing articles.");

        WithChatOptions().WithModel("fast-model");

        UseToolset<KnowledgeToolset>().Exclude(t => t.DeleteArticle);
    }

    protected override string GetInstructions() =>
        "Answer the question from the articles. Read only what you need; quote what you rely on.";
}
// end-snippet
