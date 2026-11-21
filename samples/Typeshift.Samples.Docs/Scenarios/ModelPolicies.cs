#pragma warning disable TYPESHIFT001 // the model policy is experimental
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Scenarios;

// The model policy of models.md: the policy an agent declares tries a rate-limited call again, so the run answers and
// its conversation holds the question once, and a call site hands a run the policy with a longer timeout
internal static class ModelPolicies
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var helpCenter = scope.ServiceProvider.GetRequiredService<AgentHost<HelpCenterAgent>>().WithConversationId(31);

        var retried = await helpCenter.RunAsync(CannedChatClient.Overloaded);
        Expect(retried.Text == CannedChatClient.Answer, "a rate-limited call tried again to answer");

        var stored = (await helpCenter.LoadConversationAsync()).SelectMany(entry => entry.Messages);
        Expect(stored.Count(message => message.Role == ChatRole.User) == 1, "the question in the conversation once");

        // begin-snippet: call-site-model-policy
        var patient = helpCenter.Agent.ModelPolicy.Timeout(TimeSpan.FromMinutes(2));

        var answer = await helpCenter
            .WithModelPolicy(patient)
            .RunAsync("How do I change my plan?");
        // end-snippet

        Expect(answer.Text == CannedChatClient.Answer, "a run handed a policy at its call site to answer");
    }

    private static void Expect(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException($"Expected {what}.");
    }
}

#pragma warning restore TYPESHIFT001
