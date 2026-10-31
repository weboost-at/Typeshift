using Microsoft.AspNetCore.Http;
using Typeshift.Samples.Hosting;

namespace Typeshift.Samples.Web;

// begin-snippet: chat-owner-filter
internal sealed class ChatOwnerFilter(IChats chats, ICurrentCustomer customer) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var chatId = int.Parse((string)http.Request.RouteValues["chatId"]!);

        return await chats.BelongsToAsync(chatId, customer.Id, http.RequestAborted)
            ? await next(context)
            : TypedResults.NotFound();
    }
}
// end-snippet
