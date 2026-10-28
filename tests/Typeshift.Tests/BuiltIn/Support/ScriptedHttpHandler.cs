using System.Net;
using System.Text;

namespace Typeshift.Tests.BuiltIn.Support;

// Answers every request with one response, or never, as a server that hangs would
public sealed class ScriptedHttpHandler(HttpResponseMessage? response) : HttpMessageHandler
{
    public static ScriptedHttpHandler Page(string body, string mediaType) =>
        new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        });

    public static ScriptedHttpHandler Status(HttpStatusCode status) => new(new HttpResponseMessage(status));

    public static ScriptedHttpHandler Hanging() => new(null);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (response is null) return Never(ct);

        response.RequestMessage = request;
        return Task.FromResult(response);
    }

    private static Task<HttpResponseMessage> Never(CancellationToken ct)
    {
        var never = new TaskCompletionSource<HttpResponseMessage>();
        ct.Register(() => never.TrySetCanceled(ct));
        return never.Task;
    }
}
