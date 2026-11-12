using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Web;

// begin-snippet: conversation-hub
internal sealed class ConversationHub(ConversationTracker<int> tracker, IChats chats, ICurrentCustomer customer) : Hub
{
    public async IAsyncEnumerable<string> Watch(int chatId, [EnumeratorCancellation] CancellationToken ct)
    {
        if (!await chats.BelongsToAsync(chatId, customer.Id, ct)) yield break;

        await foreach (var update in tracker.WatchAsync(chatId, ct))
        {
            if (update.Text is { Length: > 0 } text) yield return text;
        }
    }
}
// end-snippet
