using Microsoft.Extensions.DependencyInjection;
using Typeshift.Samples.Agents;
using Typeshift.Samples.Hosting;
using Typeshift.Samples.Web;

namespace Typeshift.Samples.Scenarios;

// The hub that follows a chat from elsewhere, called as SignalR calls it: the owner's stream carries the chat's text,
// and another customer's chat streams nothing
internal static class ChatHub
{
    public static async Task RunAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var tracker = scope.ServiceProvider.GetRequiredService<ConversationTracker<int>>();
        var customer = scope.ServiceProvider.GetRequiredService<ICurrentCustomer>();
        var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>().WithConversationId(41);

        var hub = new ConversationHub(tracker, new SampleChats(), customer);
        using var leaving = new CancellationTokenSource();

        await using var watching = hub.Watch(41, leaving.Token).GetAsyncEnumerator();
        var first = watching.MoveNextAsync().AsTask();

        await foreach (var _ in support.RunStreamingAsync("Where is my refund?"))
        {
        }

        if (!await first.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System)
            || watching.Current != CannedChatClient.Answer)
            throw new InvalidOperationException("Expected the hub to stream the chat's answer to its owner.");

        await leaving.CancelAsync();

        await foreach (var _ in hub.Watch(99, CancellationToken.None))
            throw new InvalidOperationException("Expected the hub to stream nothing of another customer's chat.");
    }
}
