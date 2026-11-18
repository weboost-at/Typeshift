using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Scenarios;

// A Typeshift agent as an Agent Framework AIAgent, over the client of the raw path
internal static class AgentFramework
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();
        var settingsId = 42;

        // begin-snippet: agent-framework
        var options = await support.WithSettingsId(settingsId).ResolveOptionsAsync();
        using var client = await support.CreateChatClientAsync(options);

        AIAgent agent = client.AsAIAgent(new ChatClientAgentOptions
        {
            Name = support.Agent.Name,
            Description = support.Agent.Description,
            ChatOptions = options,
            UseProvidedChatClientAsIs = true
        });

        var answer = await agent.RunAsync("Where is my refund?");
        // end-snippet

        Expect(answer.Text == CannedChatClient.Answer, "the agent to answer through Agent Framework");

        var streamed = "";
        await foreach (var update in agent.RunStreamingAsync("Where is my refund?"))
            streamed += update.Text;

        Expect(streamed == CannedChatClient.Answer, "the agent to stream through Agent Framework");

        await InConversationAsync(support, provider.GetRequiredService<IConversationStore<int>>());
    }

    // What the page says of a host with a conversation id: the runs continue it, the session keeps only its id, a
    // run with options of its own stores nothing, and a decision comes back to be answered on the host
    private static async Task InConversationAsync(AgentHost<SupportAgent> support, IConversationStore<int> store)
    {
        var chat = support.WithConversationId(51);
        var options = await chat.ResolveOptionsAsync();
        using var client = await chat.CreateChatClientAsync(options);
        var agent = client.AsAIAgent(new ChatClientAgentOptions
        {
            Name = support.Agent.Name,
            ChatOptions = options,
            UseProvidedChatClientAsIs = true
        });

        var session = await agent.CreateSessionAsync();
        await agent.RunAsync("Where is my refund?", session);
        var first = (await store.LoadConversationAsync(51, CancellationToken.None)).Count;
        await agent.RunAsync("And when does it arrive?", session);
        var second = (await store.LoadConversationAsync(51, CancellationToken.None)).Count;

        Expect(first > 0 && second > first, "both runs to continue and store the host's conversation");
        Expect(session is ChatClientAgentSession { ConversationId: "51" }, "the session to keep the conversation's id");

        var own = new ChatClientAgentRunOptions(new ChatOptions { Temperature = 0 });
        var answered = await agent.RunAsync("Where is my refund?", options: own);
        var after = (await store.LoadConversationAsync(51, CancellationToken.None)).Count;

        Expect(answered.Text.Length > 0 && after == second, "a run with options of its own to store nothing");

        var named = await NamedAsync(() => agent.RunAsync("Where is my refund?", session, own));

        Expect(named.Contains("IConversationStore<string>"), "the session's id to name a string conversation");

        var waiting = await agent.RunAsync("Close ticket 1234.");
        var approvals = waiting.AsChatResponse().Decisions.OfType<ApprovalDecision>().ToList();

        Expect(approvals.Count == 1, "the run to return the approval it waits for");

        var resumed = await chat.AnswerAsync([.. approvals.Select(approval => approval.Approve())]);

        Expect(resumed.Decisions.Count == 0, "the host to answer the decision by its id");
    }

    // The store of these scenarios keeps int ids, so a string conversation finds no store
    private static async Task<string> NamedAsync(Func<Task> run)
    {
        try
        {
            await run();
            return "";
        }
        catch (InvalidOperationException missing)
        {
            return missing.Message;
        }
    }

    private static void Expect(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException($"Expected {what}.");
    }
}
