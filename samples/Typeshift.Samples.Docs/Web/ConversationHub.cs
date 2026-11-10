using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Web;

// begin-snippet: conversation-hub
internal sealed class ConversationHub(ConversationTracker tracker, IChats chats, ICurrentCustomer customer) : Hub
{
    // The browser calls connection.stream("Watch", chatId) and gets the text as the agent writes it
    public async IAsyncEnumerable<string> Watch(int chatId, [EnumeratorCancellation] CancellationToken ct)
    {
        if (!await chats.BelongsToAsync(chatId, customer.Id, ct)) yield break;

        await foreach (var update in tracker.WatchAsync(chatId, ct))
            yield return update.Text;
    }
}
// end-snippet
