#pragma warning disable TYPESHIFT001 // the model policy is experimental
using Typeshift.Samples.Knowledge;

namespace Typeshift.Samples.Agents;

internal partial class HelpCenterAgent : Agent
{
    public HelpCenterAgent()
    {
        WithInstructions("Answer from the help center's articles, and name the ones you used.");

        WithChatOptions()
            .WithProvider("anthropic")
            .WithModel("claude-sonnet-5-5");

        // begin-snippet: model-policy
        WithRunOptions()
            .WithModelPolicy(ModelPolicy
                .Handle(ModelFailure.Transient).Retry(2, delay: TimeSpan.FromSeconds(1))
                .Or(ModelFailure.Transient | ModelFailure.Rejected).FallbackTo("openai", "gpt-5-mini")
                .Timeout(TimeSpan.FromSeconds(30)));
        // end-snippet

        UseToolset<KnowledgeToolset>()
            .Exclude(t => t.DeleteArticle);
    }
}
