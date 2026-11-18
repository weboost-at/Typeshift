using Microsoft.AspNetCore.Http;

namespace Typeshift.Samples.Web;

// begin-snippet: chat-busy-filter
internal sealed class ChatBusyFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ConversationBusyException)
        {
            // An answer to this chat is under way: a double click on send, a second tab or a retry
            return TypedResults.Conflict();
        }
    }
}
// end-snippet
